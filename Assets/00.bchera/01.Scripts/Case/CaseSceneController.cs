using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CaseSceneController : MonoBehaviour
{
    private const string DatabaseResourcePath = "Case/StragglersEvents";

    [Header("예시 판정 스탯")]
    [SerializeField, Range(1, 20)] private int strength = 10;
    [SerializeField, Range(1, 20)] private int agility = 10;
    [SerializeField, Range(1, 20)] private int perception = 10;
    [SerializeField, Range(1, 20)] private int intelligence = 10;
    [SerializeField, Range(1, 20)] private int persuasion = 10;

    [Header("시작 설정")]
    [SerializeField] private string initialEventId;
    [SerializeField] private bool randomEventOnStart;
    [SerializeField] private bool enableEventBrowser;
    [SerializeField, Min(0f)] private float autoReturnDelay = 0.9f;

    private EventDatabase database;
    private EventEntry currentEvent;
    private ChoiceEntry selectedChoice;
    private int currentEventIndex;
    private bool isRolling;
    private Coroutine automaticReturnCoroutine;
    private string returnSceneName = CaseSceneRequest.DefaultReturnSceneName;
    private string sourceNodeId;

    private Font uiFont;
    private Text eventTitleText;
    private Text eventBodyText;
    private Text eventMetaText;
    private Text eventCounterText;
    private Text actionHeaderText;
    private RectTransform choiceGrid;
    private GameObject resolutionPanel;
    private Text resolutionChoiceText;
    private Text resolutionSubText;
    private Text dieText;
    private Text gradeText;
    private Text resultText;
    private Text effectText;
    private Button primaryButton;
    private Text primaryButtonText;
    private Button backButton;
    private Button previousButton;
    private Button nextButton;

    private readonly Color backdropColor = new Color32(12, 15, 18, 255);
    private readonly Color frameColor = new Color32(224, 227, 230, 255);
    private readonly Color storyColor = new Color32(241, 242, 243, 255);
    private readonly Color actionColor = new Color32(214, 218, 222, 255);
    private readonly Color blankImageColor = new Color32(47, 52, 58, 255);
    private readonly Color inkColor = new Color32(29, 32, 35, 255);
    private readonly Color mutedColor = new Color32(91, 98, 105, 255);
    private readonly Color lineColor = new Color32(144, 151, 158, 255);
    private readonly Color activeColor = new Color32(34, 48, 61, 255);

    private void Awake()
    {
        LoadDatabase();
        CreateEventSystem();
        BuildInterface();

        if (database == null || database.events == null || database.events.Length == 0)
        {
            eventTitleText.text = "사건 데이터를 불러오지 못했습니다";
            eventBodyText.text = $"Resources/{DatabaseResourcePath}.json 파일을 확인하십시오.";
            return;
        }

        var hasRequest = CaseSceneRequest.TryTake(
            out var requestedEventId,
            out returnSceneName,
            out sourceNodeId);

        var eventWasShown = hasRequest && TryShowEvent(requestedEventId);
        if (hasRequest && !eventWasShown)
        {
            Debug.LogWarning($"요청한 사건 ID를 찾지 못했습니다: {requestedEventId}");
            MapRunState.CancelPendingNode();
            sourceNodeId = null;
        }

        if (!eventWasShown && !string.IsNullOrWhiteSpace(initialEventId))
            eventWasShown = TryShowEvent(initialEventId);

        if (!eventWasShown)
        {
            currentEventIndex = randomEventOnStart ? UnityEngine.Random.Range(0, database.events.Length) : 0;
            ShowEvent(currentEventIndex);
        }
    }

    public void ShowEvent(string eventId)
    {
        if (!TryShowEvent(eventId))
            Debug.LogWarning($"사건 ID를 찾지 못했습니다: {eventId}");
    }

    private bool TryShowEvent(string eventId)
    {
        if (database?.events == null || string.IsNullOrWhiteSpace(eventId))
            return false;

        for (var index = 0; index < database.events.Length; index++)
        {
            if (!string.Equals(database.events[index].eventId, eventId, StringComparison.OrdinalIgnoreCase))
                continue;

            ShowEvent(index);
            return true;
        }

        return false;
    }

    public void ShowRandomEvent()
    {
        if (database?.events == null || database.events.Length == 0)
            return;
        ShowEvent(UnityEngine.Random.Range(0, database.events.Length));
    }

    private void LoadDatabase()
    {
        var source = Resources.Load<TextAsset>(DatabaseResourcePath);
        if (source == null)
        {
            Debug.LogError($"사건 데이터가 없습니다: Resources/{DatabaseResourcePath}.json");
            return;
        }

        database = JsonUtility.FromJson<EventDatabase>(source.text);
        if (database?.events == null || database.events.Length != 60)
            Debug.LogWarning($"사건 데이터 수가 예상과 다릅니다. 현재: {database?.events?.Length ?? 0}, 예상: 60");
    }

    private void CreateEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(transform, false);
    }

    private void BuildInterface()
    {
        uiFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Arial" }, 24);
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasObject = new GameObject("CaseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CreateImage("Backdrop", canvasObject.transform, backdropColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var frame = CreateImage("CaseFrame", canvasObject.transform, frameColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;

        BuildImageArea(frame);
        BuildStoryArea(frame);
        BuildActionArea(frame);
    }

    private void BuildImageArea(RectTransform frame)
    {
        var imageArea = CreateImage("BlankEventImage", frame, blankImageColor,
            new Vector2(0f, 0.58f), Vector2.one, Vector2.zero, Vector2.zero);

        CreateImage("TopShade", imageArea, new Color(0f, 0f, 0f, 0.58f),
            new Vector2(0f, 0.84f), Vector2.one, Vector2.zero, Vector2.zero);

        eventMetaText = CreateText("EventMeta", imageArea, "",
            22, Color.white, TextAnchor.UpperLeft,
            new Vector2(0f, 0.84f), new Vector2(0.72f, 1f), new Vector2(34f, 12f), new Vector2(-10f, -10f));

        eventCounterText = CreateText("EventCounter", imageArea, "",
            22, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperRight,
            new Vector2(0.72f, 0.84f), Vector2.one, new Vector2(0f, 12f), new Vector2(-34f, -10f));

        previousButton = CreateButton("PreviousEvent", imageArea, "이전 사건",
            new Vector2(0.73f, 0.02f), new Vector2(0.855f, 0.12f), Vector2.zero, Vector2.zero, 20);
        previousButton.onClick.AddListener(() => ShowEvent(WrapIndex(currentEventIndex - 1)));

        nextButton = CreateButton("NextEvent", imageArea, "다음 사건",
            new Vector2(0.865f, 0.02f), new Vector2(0.99f, 0.12f), Vector2.zero, Vector2.zero, 20);
        nextButton.onClick.AddListener(() => ShowEvent(WrapIndex(currentEventIndex + 1)));

        previousButton.gameObject.SetActive(enableEventBrowser);
        nextButton.gameObject.SetActive(enableEventBrowser);
    }

    private void BuildStoryArea(RectTransform frame)
    {
        var storyPanel = CreateImage("StoryPanel", frame, storyColor,
            new Vector2(0f, 0.32f), new Vector2(1f, 0.58f), Vector2.zero, Vector2.zero);

        eventTitleText = CreateText("EventTitle", storyPanel, "사건 제목",
            42, inkColor, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.64f), Vector2.one, new Vector2(44f, 0f), new Vector2(-44f, 0f));
        eventTitleText.fontStyle = FontStyle.Bold;

        eventBodyText = CreateText("EventBody", storyPanel, "사건 본문",
            25, mutedColor, TextAnchor.UpperLeft,
            new Vector2(0f, 0f), new Vector2(1f, 0.64f), new Vector2(46f, 12f), new Vector2(-46f, -4f));
        eventBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        eventBodyText.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private void BuildActionArea(RectTransform frame)
    {
        var actionPanel = CreateImage("ActionPanel", frame, actionColor,
            Vector2.zero, new Vector2(1f, 0.32f), Vector2.zero, Vector2.zero);

        actionHeaderText = CreateText("ActionHeader", actionPanel, "행동 선택",
            22, inkColor, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.84f), Vector2.one, new Vector2(30f, 0f), new Vector2(-30f, 0f));
        actionHeaderText.fontStyle = FontStyle.Bold;

        choiceGrid = CreateRect("ChoiceGrid", actionPanel,
            Vector2.zero, new Vector2(1f, 0.84f), new Vector2(28f, 22f), new Vector2(-28f, -8f));
        var grid = choiceGrid.gameObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(0, 0, 0, 0);
        grid.spacing = new Vector2(16f, 14f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.cellSize = new Vector2(924f, 112f);

        resolutionPanel = CreateRect("ResolutionPanel", actionPanel,
            Vector2.zero, new Vector2(1f, 0.84f), new Vector2(28f, 22f), new Vector2(-28f, -8f)).gameObject;
        BuildResolutionPanel(resolutionPanel.transform as RectTransform);
        resolutionPanel.SetActive(false);
    }

    private void BuildResolutionPanel(RectTransform parent)
    {
        var choiceBlock = CreateImage("ChoiceBlock", parent, storyColor,
            new Vector2(0f, 0f), new Vector2(0.31f, 1f), Vector2.zero, new Vector2(-8f, 0f));
        resolutionChoiceText = CreateText("ResolutionChoice", choiceBlock, "",
            24, inkColor, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.28f), Vector2.one, new Vector2(22f, 0f), new Vector2(-20f, -10f));
        resolutionChoiceText.fontStyle = FontStyle.Bold;
        resolutionSubText = CreateText("ResolutionSub", choiceBlock, "",
            19, mutedColor, TextAnchor.UpperLeft,
            Vector2.zero, new Vector2(1f, 0.3f), new Vector2(22f, 4f), new Vector2(-20f, 0f));

        var dieBlock = CreateImage("Die", parent, new Color32(235, 237, 239, 255),
            new Vector2(0.315f, 0f), new Vector2(0.425f, 1f), new Vector2(0f, 8f), new Vector2(-8f, -8f));
        dieText = CreateText("DieValue", dieBlock, "20", 52, inkColor, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var resultBlock = CreateImage("ResultBlock", parent, storyColor,
            new Vector2(0.43f, 0f), new Vector2(0.82f, 1f), Vector2.zero, new Vector2(-8f, 0f));
        gradeText = CreateText("Grade", resultBlock, "판정 준비", 24, inkColor, TextAnchor.UpperLeft,
            new Vector2(0f, 0.68f), Vector2.one, new Vector2(22f, 8f), new Vector2(-20f, -8f));
        gradeText.fontStyle = FontStyle.Bold;
        resultText = CreateText("ResultText", resultBlock, "", 19, mutedColor, TextAnchor.UpperLeft,
            new Vector2(0f, 0.25f), new Vector2(1f, 0.7f), new Vector2(22f, 2f), new Vector2(-20f, -2f));
        resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
        resultText.verticalOverflow = VerticalWrapMode.Truncate;
        effectText = CreateText("EffectText", resultBlock, "", 18, inkColor, TextAnchor.MiddleLeft,
            Vector2.zero, new Vector2(1f, 0.27f), new Vector2(22f, 0f), new Vector2(-20f, 0f));

        primaryButton = CreateButton("PrimaryAction", parent, "주사위 굴리기",
            new Vector2(0.83f, 0.52f), Vector2.one, Vector2.zero, Vector2.zero, 21);
        primaryButtonText = primaryButton.GetComponentInChildren<Text>();
        backButton = CreateButton("BackToChoices", parent, "다른 선택지",
            new Vector2(0.83f, 0f), new Vector2(1f, 0.46f), Vector2.zero, Vector2.zero, 21);
        backButton.onClick.AddListener(ShowChoices);
    }

    private void ShowEvent(int index)
    {
        if (database?.events == null || database.events.Length == 0)
            return;

        if (automaticReturnCoroutine != null)
        {
            StopCoroutine(automaticReturnCoroutine);
            automaticReturnCoroutine = null;
        }

        currentEventIndex = WrapIndex(index);
        currentEvent = database.events[currentEventIndex];
        selectedChoice = null;
        isRolling = false;

        eventMetaText.text = $"{currentEvent.eventId}  ·  {currentEvent.category}  ·  {currentEvent.riskReturn}";
        eventCounterText.text = $"{currentEventIndex + 1:00} / {database.events.Length:00}";
        eventTitleText.text = currentEvent.title;
        eventBodyText.text = currentEvent.description;
        BuildChoiceButtons();
        ShowChoices();
    }

    private void BuildChoiceButtons()
    {
        for (var childIndex = choiceGrid.childCount - 1; childIndex >= 0; childIndex--)
            Destroy(choiceGrid.GetChild(childIndex).gameObject);

        if (currentEvent?.choices == null)
            return;

        for (var index = 0; index < currentEvent.choices.Length; index++)
        {
            var choice = currentEvent.choices[index];
            var suffix = BuildChoiceSuffix(choice);
            var label = $"{choice.order}.  {choice.text}\n{suffix}";
            var button = CreateButton($"Choice_{choice.choiceId}", choiceGrid, label,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 21);
            var capturedChoice = choice;
            button.onClick.AddListener(() => SelectChoice(capturedChoice));
            var buttonText = button.GetComponentInChildren<Text>();
            buttonText.alignment = TextAnchor.MiddleLeft;
            buttonText.color = inkColor;
            var colors = button.colors;
            colors.normalColor = storyColor;
            colors.highlightedColor = new Color32(248, 249, 250, 255);
            colors.pressedColor = new Color32(199, 204, 209, 255);
            button.colors = colors;
        }
    }

    private string BuildChoiceSuffix(ChoiceEntry choice)
    {
        if (!string.IsNullOrWhiteSpace(choice.uiLabel))
        {
            if (!string.IsNullOrWhiteSpace(choice.cost))
                return $"{choice.uiLabel}  ·  비용: {choice.cost}";
            return choice.uiLabel;
        }
        return string.IsNullOrWhiteSpace(choice.cost) ? "[판정 없음]" : $"[판정 없음]  ·  비용: {choice.cost}";
    }

    private void SelectChoice(ChoiceEntry choice)
    {
        if (choice == null || isRolling)
            return;

        selectedChoice = choice;
        choiceGrid.gameObject.SetActive(false);
        resolutionPanel.SetActive(true);
        primaryButton.gameObject.SetActive(true);
        backButton.gameObject.SetActive(true);
        primaryButton.interactable = true;
        backButton.interactable = true;
        actionHeaderText.text = choice.type == "판정" ? "판정 준비" : "선택 결과";
        resolutionChoiceText.text = choice.text;

        primaryButton.onClick.RemoveAllListeners();
        if (choice.type == "판정")
        {
            var statValue = GetStatValue(choice.stat);
            resolutionSubText.text = $"{choice.stat} {statValue}  ·  d20";
            dieText.text = "20";
            gradeText.text = "판정 준비";
            gradeText.color = inkColor;
            resultText.text = $"주사위 값이 {statValue} 이하이면 성공권에 들어갑니다.";
            effectText.text = "대성공 / 성공 / 보통 성공 / 보통 실패 / 실패 / 대실패";
            primaryButtonText.text = "주사위 굴리기";
            primaryButton.onClick.AddListener(BeginRoll);
        }
        else
        {
            resolutionSubText.text = string.IsNullOrWhiteSpace(choice.cost) ? "판정 없음" : $"비용: {choice.cost}";
            dieText.text = "—";
            gradeText.text = choice.type == "전투" ? "전투 전환" : "사건 종료";
            gradeText.color = choice.type == "전투" ? new Color32(151, 59, 52, 255) : inkColor;
            resultText.text = choice.immediateResult;
            effectText.text = choice.endsEvent ? "선택 즉시 사건 종료" : "사건 계속";
            actionHeaderText.text = "선택 결과  ·  맵으로 복귀 중";
            primaryButton.gameObject.SetActive(false);
            backButton.gameObject.SetActive(false);
            BeginAutomaticReturn();
        }
    }

    private void BeginRoll()
    {
        if (selectedChoice == null || selectedChoice.outcomes == null || isRolling)
            return;
        StartCoroutine(RollAndResolve());
    }

    private IEnumerator RollAndResolve()
    {
        isRolling = true;
        primaryButton.interactable = false;
        backButton.interactable = false;
        var elapsed = 0f;
        while (elapsed < 0.75f)
        {
            dieText.text = UnityEngine.Random.Range(1, 21).ToString();
            elapsed += 0.06f;
            yield return new WaitForSecondsRealtime(0.06f);
        }

        var roll = UnityEngine.Random.Range(1, 21);
        var statValue = GetStatValue(selectedChoice.stat);
        var grade = Classify(statValue, roll);
        var outcome = selectedChoice.outcomes.Get(grade);
        var difference = statValue - roll;

        dieText.text = roll.ToString();
        actionHeaderText.text = "판정 결과  ·  맵으로 복귀 중";
        resolutionSubText.text = $"{selectedChoice.stat} {statValue}  ·  결과값 {(difference >= 0 ? "+" : string.Empty)}{difference}";
        gradeText.text = GradeLabel(grade);
        gradeText.color = IsFailure(grade) ? new Color32(151, 59, 52, 255) : new Color32(54, 113, 76, 255);
        resultText.text = outcome?.text ?? "결과 텍스트가 없습니다.";
        effectText.text = outcome?.effect ?? string.Empty;

        primaryButton.onClick.RemoveAllListeners();
        primaryButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(false);
        isRolling = false;
        BeginAutomaticReturn();
    }

    private void ShowChoices()
    {
        if (isRolling)
            return;
        selectedChoice = null;
        actionHeaderText.text = "행동 선택";
        choiceGrid.gameObject.SetActive(true);
        resolutionPanel.SetActive(false);
    }

    private void CompleteEventAndReturnToMap()
    {
        if (isRolling)
            return;

        if (!string.IsNullOrWhiteSpace(sourceNodeId))
            MapRunState.CompleteNode(sourceNodeId);

        var targetScene = string.IsNullOrWhiteSpace(returnSceneName)
            ? CaseSceneRequest.DefaultReturnSceneName
            : returnSceneName;

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError($"복귀 씬이 Build Settings에 없습니다: {targetScene}");
            resultText.text = $"복귀 씬을 열 수 없습니다: {targetScene}";
            effectText.text = "Build Settings의 씬 등록 상태를 확인하십시오.";
            primaryButton.gameObject.SetActive(true);
            primaryButton.interactable = true;
            primaryButtonText.text = "복귀 다시 시도";
            primaryButton.onClick.RemoveAllListeners();
            primaryButton.onClick.AddListener(CompleteEventAndReturnToMap);
            return;
        }

        SceneManager.LoadScene(targetScene);
    }

    private void BeginAutomaticReturn()
    {
        if (automaticReturnCoroutine != null)
            StopCoroutine(automaticReturnCoroutine);
        automaticReturnCoroutine = StartCoroutine(ReturnToMapAfterResult());
    }

    private IEnumerator ReturnToMapAfterResult()
    {
        yield return new WaitForSecondsRealtime(autoReturnDelay);
        automaticReturnCoroutine = null;
        CompleteEventAndReturnToMap();
    }

    private int WrapIndex(int index)
    {
        if (database?.events == null || database.events.Length == 0)
            return 0;
        if (index < 0)
            return database.events.Length - 1;
        if (index >= database.events.Length)
            return 0;
        return index;
    }

    private int GetStatValue(string stat)
    {
        switch (stat)
        {
            case "근력": return strength;
            case "민첩": return agility;
            case "인지": return perception;
            case "지능": return intelligence;
            case "설득력": return persuasion;
            default: return 10;
        }
    }

    private static ResultGrade Classify(int statValue, int roll)
    {
        if (roll == 1) return ResultGrade.CriticalSuccess;
        if (roll == 20) return ResultGrade.CriticalFailure;
        var difference = statValue - roll;
        if (difference >= 10) return ResultGrade.CriticalSuccess;
        if (difference >= 5) return ResultGrade.Success;
        if (difference >= 0) return ResultGrade.NormalSuccess;
        if (difference >= -4) return ResultGrade.NormalFailure;
        if (difference >= -9) return ResultGrade.Failure;
        return ResultGrade.CriticalFailure;
    }

    private static bool IsFailure(ResultGrade grade)
    {
        return grade == ResultGrade.NormalFailure || grade == ResultGrade.Failure || grade == ResultGrade.CriticalFailure;
    }

    private static string GradeLabel(ResultGrade grade)
    {
        switch (grade)
        {
            case ResultGrade.CriticalSuccess: return "대성공";
            case ResultGrade.Success: return "성공";
            case ResultGrade.NormalSuccess: return "보통 성공";
            case ResultGrade.NormalFailure: return "보통 실패";
            case ResultGrade.Failure: return "실패";
            default: return "대실패";
        }
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        var rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        return rect;
    }

    private RectTransform CreateImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private Text CreateText(string name, Transform parent, string value, int fontSize, Color color, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.text = value;
        text.raycastTarget = false;
        text.supportRichText = true;
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, int fontSize)
    {
        var rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = storyColor;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = activeColor;
        colors.highlightedColor = new Color32(54, 72, 88, 255);
        colors.pressedColor = new Color32(19, 29, 38, 255);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var text = CreateText("Label", rect, label, fontSize, Color.white, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(14f, 6f), new Vector2(-14f, -6f));
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return button;
    }

    private enum ResultGrade
    {
        CriticalSuccess,
        Success,
        NormalSuccess,
        NormalFailure,
        Failure,
        CriticalFailure,
    }

    [Serializable]
    private sealed class EventDatabase
    {
        public int version;
        public EventEntry[] events;
    }

    [Serializable]
    private sealed class EventEntry
    {
        public string eventId;
        public string title;
        public string category;
        public string description;
        public string riskReturn;
        public string imageKey;
        public ChoiceEntry[] choices;
    }

    [Serializable]
    private sealed class ChoiceEntry
    {
        public string choiceId;
        public string eventId;
        public int order;
        public string text;
        public string type;
        public string stat;
        public string uiLabel;
        public string cost;
        public string immediateResult;
        public bool endsEvent;
        public OutcomeSet outcomes;
    }

    [Serializable]
    private sealed class OutcomeSet
    {
        public Outcome criticalSuccess;
        public Outcome success;
        public Outcome normalSuccess;
        public Outcome normalFailure;
        public Outcome failure;
        public Outcome criticalFailure;

        public Outcome Get(ResultGrade grade)
        {
            switch (grade)
            {
                case ResultGrade.CriticalSuccess: return criticalSuccess;
                case ResultGrade.Success: return success;
                case ResultGrade.NormalSuccess: return normalSuccess;
                case ResultGrade.NormalFailure: return normalFailure;
                case ResultGrade.Failure: return failure;
                default: return criticalFailure;
            }
        }
    }

    [Serializable]
    private sealed class Outcome
    {
        public string text;
        public string effect;
    }
}

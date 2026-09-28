using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class InGameMapController : MonoBehaviour
{
    private const float MapWidth = 2920f;
    private const float MapHeight = 660f;
    private const string EventDatabaseResourcePath = "Case/StragglersEvents";

    private static readonly RegionPreset[] RegionPresets =
    {
        new RegionPreset("west_harbor", "서부 항구", "침수된 항만과 밀수 세력", "I", "×1.00", 0, 0, 0),
        new RegionPreset("gromov", "그로모프 군사 구역", "잔존군 검문소와 폐쇄 시설", "II", "×1.15", 12, 1, 0),
        new RegionPreset("scrapyard", "고철 지대", "약탈자 소굴과 폐차장", "III", "×1.30", 24, 2, 1),
        new RegionPreset("quarantine", "오염 격리 구역", "감염 지대와 붕괴 시설", "IV", "×1.50", 36, 3, 1),
        new RegionPreset("east_gate", "동부 최종 관문", "최종 봉쇄선과 탈출 지점", "V", "×1.80", 48, 4, 2)
    };

    [Header("표시용 자원")]
    [SerializeField] private int currentHealth = 214;
    [SerializeField] private int maxHealth = 268;
    [SerializeField] private int currentMental = 78;
    [SerializeField] private int maxMental = 100;
    [SerializeField] private float hunger = 6.5f;
    [SerializeField] private int currency = 820;

    private readonly List<MapNode> nodes = new List<MapNode>();
    private readonly Dictionary<string, MapNode> nodeById = new Dictionary<string, MapNode>();
    private readonly Dictionary<string, Button> buttonByNodeId = new Dictionary<string, Button>();
    private readonly List<MapEdgeView> edgeViews = new List<MapEdgeView>();
    private readonly Dictionary<string, EventSummary> eventSummaryById = new Dictionary<string, EventSummary>();

    private RegionPreset activeRegion;
    private Font uiFont;
    private ScrollRect mapScroll;
    private RectTransform mapContent;
    private MapNode selectedNode;
    private Text selectedTypeText;
    private Text selectedStatusText;
    private Text selectedTitleText;
    private Text selectedDescriptionText;
    private Text selectedRouteText;
    private Text selectedEventText;
    private Text selectedWarningText;
    private Text currentLocationText;
    private Button enterButton;
    private Text enterButtonText;

    private readonly Color backdropColor = new Color32(18, 21, 25, 255);
    private readonly Color panelColor = new Color32(31, 36, 42, 255);
    private readonly Color panelStrongColor = new Color32(42, 48, 55, 255);
    private readonly Color lineColor = new Color32(72, 80, 89, 255);
    private readonly Color inkColor = new Color32(241, 243, 245, 255);
    private readonly Color mutedColor = new Color32(170, 176, 184, 255);
    private readonly Color currentColor = new Color32(91, 145, 198, 255);
    private readonly Color positiveColor = new Color32(91, 157, 112, 255);
    private readonly Color neutralColor = new Color32(146, 153, 161, 255);
    private readonly Color negativeColor = new Color32(192, 92, 66, 255);
    private readonly Color shopColor = new Color32(204, 164, 72, 255);
    private readonly Color bossColor = new Color32(210, 80, 92, 255);
    private readonly Color specialColor = new Color32(145, 121, 196, 255);

    private void Awake()
    {
        MapRunState.EnsureStarted("start");
        MapRunState.CancelPendingNode();
        activeRegion = RegionPresets[Mathf.Clamp(MapRunState.CurrentRegionIndex, 0, RegionPresets.Length - 1)];
        LoadEventCatalog();
        CreateMapData();
        ValidateMapData();
        CreateEventSystem();
        BuildInterface();
        RefreshMapState();

        var current = GetNode(MapRunState.CurrentNodeId) ?? GetNode("start");
        SelectNode(current);
        StartCoroutine(FocusNodeNextFrame(current));
    }

    private void LoadEventCatalog()
    {
        var source = Resources.Load<TextAsset>(EventDatabaseResourcePath);
        if (source == null)
        {
            Debug.LogError($"맵 사건 데이터가 없습니다: Resources/{EventDatabaseResourcePath}.json");
            return;
        }

        var database = JsonUtility.FromJson<EventSummaryDatabase>(source.text);
        if (database?.events == null)
            return;

        foreach (var eventSummary in database.events)
        {
            if (eventSummary == null || string.IsNullOrWhiteSpace(eventSummary.eventId))
                continue;
            eventSummaryById[eventSummary.eventId] = eventSummary;
        }
    }

    private void CreateMapData()
    {
        AddNode("start", "지역 입구", "시작", "common", string.Empty,
            "그로모프 구역 진입 지점이다.", 0, 110f, 0f);
        AddNode("gate", "가짜 검문소", "전투", "common", "EVT_002",
            "첫 관문이다. 통과하면 세 갈래 경로가 열린다.", 1, 300f, 0f);

        AddNode("a2", "잠긴 의약품 창고", "긍정 사건", "north-a", "EVT_001",
            "보급을 기대할 수 있지만 잠금 해제 과정에 위험이 따른다.", 2, 510f, 220f);
        AddNode("b2", "무너진 지하 통로", "부정 사건", "center-a", "EVT_003",
            "짧고 위험한 중앙 통로다.", 3, 510f, 0f);
        AddNode("c2", "부상당한 밀수꾼", "중립 사건", "south-a", "EVT_004",
            "협상 결과에 따라 우회로의 가치가 달라진다.", 2, 510f, -220f);

        AddNode("a3", "이상한 발전기", "특수 사건", "north-a", "EVT_005",
            "시설을 복구하면 이후 구간에 도움을 받을 수 있다.", 2, 720f, 220f);
        AddNode("b3", "검은 밴", "전투", "center-a", "EVT_008",
            "중앙 도로를 가로막은 수상한 차량이다.", 3, 720f, 0f);
        AddNode("c3", "교량 통행 협상", "중립 사건", "south-a", "EVT_009",
            "남쪽 교량의 통행권을 확보해야 한다.", 2, 720f, -220f);

        AddNode("mergeA", "폐쇄된 군용 금고", "필수 사건", "common", "EVT_007",
            "첫 번째 합류 지점이다. 여기서 다시 두 갈래로 나뉜다.", 3, 930f, 0f);

        AddNode("u5", "수상한 장비 거래", "상점", "upper-b", "EVT_006",
            "비싼 대가를 치르면 장비를 확보할 수 있다.", 2, 1140f, 150f);
        AddNode("l5", "폭발 직전의 탄약고", "부정 사건", "lower-b", "EVT_012",
            "아래쪽 길은 빠르지만 폭발 위험이 크다.", 4, 1140f, -150f);
        AddNode("u6", "폐쇄된 연구실", "중립 사건", "upper-b", "EVT_010",
            "남겨진 연구 자료와 위험 요소를 조사한다.", 3, 1350f, 150f);
        AddNode("l6", "체포된 약탈자", "전투", "lower-b", "EVT_011",
            "포로가 된 약탈자를 어떻게 처리할지 결정한다.", 3, 1350f, -150f);
        AddNode("u7", "고장난 보급 단말", "긍정 사건", "upper-b", "EVT_013",
            "단말을 수리하면 자원을 얻을 가능성이 있다.", 2, 1560f, 150f);
        AddNode("l7", "버려진 장갑차", "전투", "lower-b", "EVT_015",
            "장갑차 주변의 적대 세력을 정리해야 한다.", 4, 1560f, -150f);

        AddNode("mergeB", "두 집단의 대치", "중간보스", "common", "EVT_016",
            "두 번째 합류 지점이다. 해결 뒤 세 경로 중 하나를 선택한다.", 4, 1770f, 0f);

        AddNode("a9", "암호화된 군용 단말", "특수 사건", "north-c", "EVT_017",
            "정보를 해독하면 후반부 위험을 예측할 수 있다.", 3, 1980f, 220f);
        AddNode("b9", "수상한 의사", "상점", "center-c", "EVT_018",
            "치료와 거래를 제안하는 의사를 만난다.", 2, 1980f, 0f);
        AddNode("c9", "전복된 금고 차량", "부정 사건", "south-c", "EVT_019",
            "재화가 남아 있지만 매복 가능성이 높다.", 4, 1980f, -220f);
        AddNode("a10", "침수된 기록보관소", "긍정 사건", "north-c", "EVT_021",
            "기록을 확보하면 탈출 경로에 관한 단서를 얻는다.", 3, 2190f, 220f);
        AddNode("b10", "무장세력의 협박", "전투", "center-c", "EVT_020",
            "정면 돌파 또는 협상이 필요한 중앙 방어선이다.", 4, 2190f, 0f);
        AddNode("c10", "무너진 호텔 금고실", "중립 사건", "south-c", "EVT_022",
            "우회로 끝의 금고실을 조사한다.", 3, 2190f, -220f);

        AddNode("mergeC", "연료 수송차", "필수 사건", "common", "EVT_023",
            "세 번째 합류 지점이다. 모든 길은 이곳을 지나 최종 구간으로 향한다.", 4, 2400f, 0f);
        AddNode("rest", "폐쇄된 라디오 타워", "보스 약화", "common", "EVT_025",
            "최종전 전에 통신망을 장악해 보스 전력을 약화할 수 있다.", 4, 2610f, 0f);
        AddNode("boss", "탈영병 바리케이드", "지역 보스", "common", "EVT_024",
            "지역의 마지막 관문이다. 돌파하면 다음 지역으로 이동한다.", 5, 2820f, 0f);

        Connect("start", "gate");
        Connect("gate", "a2", "b2", "c2");
        Connect("a2", "a3");
        Connect("b2", "b3");
        Connect("c2", "c3");
        Connect("a3", "mergeA");
        Connect("b3", "mergeA");
        Connect("c3", "mergeA");

        Connect("mergeA", "u5", "l5");
        Connect("u5", "u6");
        Connect("l5", "l6");
        Connect("u6", "u7");
        Connect("l6", "l7");
        Connect("u7", "mergeB");
        Connect("l7", "mergeB");

        Connect("mergeB", "a9", "b9", "c9");
        Connect("a9", "a10");
        Connect("b9", "b10");
        Connect("c9", "c10");
        Connect("a10", "mergeC");
        Connect("b10", "mergeC");
        Connect("c10", "mergeC");
        Connect("mergeC", "rest");
        Connect("rest", "boss");
    }

    private void ValidateMapData()
    {
        var shortestNodeCount = FindShortestNodeCount("start", "boss");
        var splitCount = 0;
        var incoming = new Dictionary<string, int>();
        foreach (var node in nodes)
        {
            if (node.nextIds.Count > 1)
                splitCount++;
            foreach (var nextId in node.nextIds)
                incoming[nextId] = incoming.TryGetValue(nextId, out var count) ? count + 1 : 1;
        }

        var mergeCount = 0;
        foreach (var pair in incoming)
        {
            if (pair.Value > 1)
                mergeCount++;
        }

        if (shortestNodeCount < 12)
            Debug.LogError($"맵 최단 경로가 너무 짧습니다: {shortestNodeCount}개 노드");
        if (splitCount < 2 || mergeCount < 2)
            Debug.LogError($"맵 분기/합류가 부족합니다. 분기 {splitCount}, 합류 {mergeCount}");

        Debug.Log($"{activeRegion.name} 고정 맵 검증: 최단 경로 {shortestNodeCount}개 노드, 분기 {splitCount}회, 합류 {mergeCount}회");
    }

    private int FindShortestNodeCount(string startId, string goalId)
    {
        var queue = new Queue<NodeDistance>();
        var visited = new HashSet<string>();
        queue.Enqueue(new NodeDistance(startId, 1));
        visited.Add(startId);

        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            if (item.nodeId == goalId)
                return item.distance;

            var node = GetNode(item.nodeId);
            if (node == null)
                continue;
            foreach (var nextId in node.nextIds)
            {
                if (visited.Add(nextId))
                    queue.Enqueue(new NodeDistance(nextId, item.distance + 1));
            }
        }

        return 0;
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

        var canvasObject = new GameObject("MapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CreateImage("Backdrop", canvasObject.transform, backdropColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuildHeader(canvasObject.transform);
        BuildMapPanel(canvasObject.transform);
        BuildDetailPanel(canvasObject.transform);
    }

    private void BuildHeader(Transform parent)
    {
        var header = CreateImage("Header", parent, panelColor,
            new Vector2(0f, 0.9f), Vector2.one, Vector2.zero, Vector2.zero);

        var kicker = CreateText("Kicker", header, $"분쟁지대 탈출 경로  ·  {activeRegion.theme}",
            18, mutedColor, TextAnchor.LowerLeft,
            new Vector2(0f, 0.52f), new Vector2(0.36f, 1f), new Vector2(30f, 0f), new Vector2(-10f, 0f));
        kicker.fontStyle = FontStyle.Bold;

        CreateText("Title", header, $"{activeRegion.name} 지도",
            34, inkColor, TextAnchor.UpperLeft,
            new Vector2(0f, 0f), new Vector2(0.36f, 0.58f), new Vector2(30f, 0f), new Vector2(-10f, 0f));

        var stageBlock = CreateImage("StageBlock", header, backdropColor,
            new Vector2(0.37f, 0.17f), new Vector2(0.59f, 0.83f), Vector2.zero, Vector2.zero);
        var previousRegionButton = CreateButton("PreviousRegion", stageBlock, "◀",
            new Vector2(0f, 0f), new Vector2(0.18f, 1f), Vector2.zero, Vector2.zero, 18);
        previousRegionButton.onClick.AddListener(() => ChangeRegion(-1));
        CreateText("Stage", stageBlock,
            $"STAGE {MapRunState.CurrentRegionIndex + 1} / {RegionPresets.Length}\n위험도 {activeRegion.danger}  ·  보상 {activeRegion.reward}",
            20, inkColor, TextAnchor.MiddleCenter, new Vector2(0.18f, 0f), new Vector2(0.82f, 1f), Vector2.zero, Vector2.zero);
        var nextRegionButton = CreateButton("NextRegion", stageBlock, "▶",
            new Vector2(0.82f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 18);
        nextRegionButton.onClick.AddListener(() => ChangeRegion(1));

        var resourceText = $"HP  {currentHealth} / {maxHealth}     정신  {currentMental} / {maxMental}\n허기  {hunger:0.0} / 10     아르덴  {currency}";
        CreateText("Resources", header, resourceText,
            20, inkColor, TextAnchor.MiddleRight,
            new Vector2(0.59f, 0f), Vector2.one, Vector2.zero, new Vector2(-30f, 0f));
    }

    private void BuildMapPanel(Transform parent)
    {
        var panel = CreateImage("MapPanel", parent, backdropColor,
            new Vector2(0f, 0f), new Vector2(0.78f, 0.9f), Vector2.zero, Vector2.zero);

        currentLocationText = CreateText("CurrentLocation", panel, "",
            20, mutedColor, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.93f), new Vector2(0.75f, 1f), new Vector2(24f, 0f), Vector2.zero);

        var resetButton = CreateButton("ResetMap", panel, "진행 초기화",
            new Vector2(0.85f, 0.935f), new Vector2(0.985f, 0.995f), Vector2.zero, Vector2.zero, 17);
        resetButton.onClick.AddListener(ResetMapProgress);

        var viewport = CreateImage("MapViewport", panel, new Color32(23, 27, 31, 255),
            new Vector2(0.015f, 0.075f), new Vector2(0.985f, 0.925f), Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();

        mapContent = CreateRect("MapContent", viewport);
        mapContent.anchorMin = new Vector2(0f, 0.5f);
        mapContent.anchorMax = new Vector2(0f, 0.5f);
        mapContent.pivot = new Vector2(0f, 0.5f);
        mapContent.sizeDelta = new Vector2(MapWidth, MapHeight);

        mapScroll = viewport.gameObject.AddComponent<ScrollRect>();
        mapScroll.viewport = viewport;
        mapScroll.content = mapContent;
        mapScroll.horizontal = true;
        mapScroll.vertical = false;
        mapScroll.inertia = true;
        mapScroll.decelerationRate = 0.12f;
        mapScroll.scrollSensitivity = 90f;
        mapScroll.movementType = ScrollRect.MovementType.Clamped;

        BuildMapConnections();
        BuildMapNodes();

        var leftButton = CreateButton("ScrollLeft", panel, "◀",
            new Vector2(0.02f, 0.012f), new Vector2(0.08f, 0.065f), Vector2.zero, Vector2.zero, 19);
        leftButton.onClick.AddListener(() => NudgeMap(-0.12f));
        var rightButton = CreateButton("ScrollRight", panel, "▶",
            new Vector2(0.09f, 0.012f), new Vector2(0.15f, 0.065f), Vector2.zero, Vector2.zero, 19);
        rightButton.onClick.AddListener(() => NudgeMap(0.12f));

        CreateText("Legend", panel,
            "● 전투   ● 긍정   ● 중립   ◆ 부정   ■ 상점   ▲ 특수·보스     파란 선: 진행 경로",
            16, mutedColor, TextAnchor.MiddleRight,
            new Vector2(0.18f, 0.005f), new Vector2(0.985f, 0.07f), Vector2.zero, Vector2.zero);
    }

    private void BuildMapConnections()
    {
        foreach (var from in nodes)
        {
            foreach (var toId in from.nextIds)
            {
                var to = GetNode(toId);
                if (to == null)
                    continue;
                var line = CreateLine(mapContent, from.position, to.position, lineColor, 6f);
                edgeViews.Add(new MapEdgeView(from.id, to.id, line));
            }
        }
    }

    private void BuildMapNodes()
    {
        foreach (var node in nodes)
        {
            var holder = CreateRect($"Node_{node.id}", mapContent);
            holder.anchorMin = new Vector2(0f, 0.5f);
            holder.anchorMax = new Vector2(0f, 0.5f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(164f, 126f);
            holder.anchoredPosition = node.position;

            var button = CreateButton("NodeButton", holder, ShortType(node.type),
                new Vector2(0.28f, 0.35f), new Vector2(0.72f, 0.92f), Vector2.zero, Vector2.zero, 16);
            var capturedNode = node;
            button.onClick.AddListener(() => SelectNode(capturedNode));
            buttonByNodeId[node.id] = button;

            var label = CreateText("NodeTitle", holder, node.title,
                16, inkColor, TextAnchor.UpperCenter,
                new Vector2(0f, 0f), new Vector2(1f, 0.34f), Vector2.zero, Vector2.zero);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }

    private void BuildDetailPanel(Transform parent)
    {
        var panel = CreateImage("DetailPanel", parent, panelColor,
            new Vector2(0.78f, 0f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);

        selectedTypeText = CreateText("Type", panel, "노드 유형",
            18, mutedColor, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.91f), new Vector2(0.64f, 0.98f), new Vector2(24f, 0f), Vector2.zero);
        selectedStatusText = CreateText("Status", panel, "잠김",
            17, currentColor, TextAnchor.MiddleRight,
            new Vector2(0.62f, 0.91f), new Vector2(1f, 0.98f), Vector2.zero, new Vector2(-24f, 0f));

        selectedTitleText = CreateText("Title", panel, "노드를 선택하세요",
            32, inkColor, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.79f), new Vector2(1f, 0.91f), new Vector2(24f, 0f), new Vector2(-24f, 0f));
        selectedTitleText.fontStyle = FontStyle.Bold;

        selectedDescriptionText = CreateText("Description", panel, "",
            21, inkColor, TextAnchor.UpperLeft,
            new Vector2(0f, 0.55f), new Vector2(1f, 0.79f), new Vector2(24f, 0f), new Vector2(-24f, 0f));
        selectedDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;

        var stats = CreateImage("NodeStats", panel, panelStrongColor,
            new Vector2(0.055f, 0.34f), new Vector2(0.945f, 0.54f), Vector2.zero, Vector2.zero);
        selectedRouteText = CreateText("Route", stats, "경로",
            18, mutedColor, TextAnchor.UpperLeft,
            new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(16f, 4f), new Vector2(-16f, 0f));
        selectedEventText = CreateText("Event", stats, "사건",
            18, inkColor, TextAnchor.LowerLeft,
            new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(16f, 0f), new Vector2(-16f, -4f));

        selectedWarningText = CreateText("Warning", panel, "",
            18, negativeColor, TextAnchor.UpperLeft,
            new Vector2(0f, 0.2f), new Vector2(1f, 0.33f), new Vector2(24f, 0f), new Vector2(-24f, 0f));
        selectedWarningText.horizontalOverflow = HorizontalWrapMode.Wrap;

        var inventoryButton = CreateButton("Inventory", panel, "아이템 / 장비",
            new Vector2(0.055f, 0.07f), new Vector2(0.46f, 0.17f), Vector2.zero, Vector2.zero, 19);
        inventoryButton.onClick.AddListener(() => selectedWarningText.text = "아이템/장비 UI 연결 지점입니다.");

        enterButton = CreateButton("EnterNode", panel, "노드 진입",
            new Vector2(0.50f, 0.07f), new Vector2(0.945f, 0.17f), Vector2.zero, Vector2.zero, 20);
        enterButtonText = enterButton.GetComponentInChildren<Text>();
        enterButton.onClick.AddListener(EnterSelectedNode);
    }

    private void RefreshMapState()
    {
        var current = GetNode(MapRunState.CurrentNodeId) ?? GetNode("start");
        currentLocationText.text = $"현재 위치: {current.title}   ·   다음 노드 {current.nextIds.Count}개 중 선택";

        foreach (var node in nodes)
        {
            if (!buttonByNodeId.TryGetValue(node.id, out var button))
                continue;

            var image = button.GetComponent<Image>();
            var text = button.GetComponentInChildren<Text>();
            var typeColor = NodeTypeColor(node.type);
            var isCurrent = node.id == current.id;
            var isCompleted = MapRunState.IsCompleted(node.id);
            var isAvailable = IsAvailable(node);

            if (isCurrent)
            {
                image.color = currentColor;
                text.color = Color.white;
            }
            else if (isCompleted)
            {
                image.color = new Color(typeColor.r * 0.6f, typeColor.g * 0.6f, typeColor.b * 0.6f, 1f);
                text.color = Color.white;
            }
            else if (isAvailable)
            {
                image.color = typeColor;
                text.color = Color.white;
            }
            else
            {
                image.color = new Color32(45, 50, 56, 255);
                text.color = new Color32(120, 126, 133, 255);
            }
        }

        foreach (var edge in edgeViews)
        {
            var fromCurrent = edge.fromId == current.id;
            var cleared = MapRunState.IsCompleted(edge.fromId) && MapRunState.IsCompleted(edge.toId);
            edge.image.color = fromCurrent || cleared ? currentColor : lineColor;
        }

        if (selectedNode != null)
            UpdateDetailPanel(selectedNode);
    }

    private void SelectNode(MapNode node)
    {
        if (node == null)
            return;

        selectedNode = node;
        UpdateDetailPanel(node);
    }

    private void UpdateDetailPanel(MapNode node)
    {
        var status = GetNodeStatus(node);
        selectedTypeText.text = $"{node.type}  ·  위험도 {ToRoman(node.difficulty)}";
        selectedStatusText.text = status;
        selectedStatusText.color = IsAvailable(node) ? positiveColor : node.id == MapRunState.CurrentNodeId ? currentColor : mutedColor;
        selectedTitleText.text = node.title;
        selectedDescriptionText.text = node.description;
        selectedRouteText.text = $"지역: {activeRegion.name}   ·   경로: {RouteLabel(node.route)}   ·   총 14개 노드";
        selectedEventText.text = string.IsNullOrWhiteSpace(node.eventId)
            ? "연결 사건: 없음"
            : $"연결 사건: {node.eventId}  ·  호출 시 해당 사건만 표시";

        var available = IsAvailable(node) && !string.IsNullOrWhiteSpace(node.eventId);
        enterButton.interactable = available;
        enterButtonText.text = available ? (node.type.Contains("보스") ? "보스 사건 진입" : "사건 진입") : "현재 진입 불가";

        if (node.id == MapRunState.CurrentNodeId)
            selectedWarningText.text = node.nextIds.Count == 0 ? "지역 경로를 완료했습니다." : "현재 위치입니다. 연결된 다음 노드를 선택하세요.";
        else if (MapRunState.IsCompleted(node.id))
            selectedWarningText.text = "이미 지나온 노드입니다.";
        else if (!IsAvailable(node))
            selectedWarningText.text = "선행 노드를 완료해야 진입할 수 있습니다.";
        else
            selectedWarningText.text = "진입하면 지정된 사건이 열립니다. 사건 종료 후 이 지도에 복귀합니다.";
    }

    private void EnterSelectedNode()
    {
        if (selectedNode == null || !IsAvailable(selectedNode) || string.IsNullOrWhiteSpace(selectedNode.eventId))
            return;

        MapRunState.BeginNode(selectedNode.id);
        CaseSceneRequest.OpenEvent(
            selectedNode.eventId,
            SceneManager.GetActiveScene().name,
            selectedNode.id);
    }

    private void ResetMapProgress()
    {
        MapRunState.Reset("start");
        selectedNode = GetNode("start");
        RefreshMapState();
        UpdateDetailPanel(selectedNode);
        StartCoroutine(FocusNodeNextFrame(selectedNode));
    }

    private void ChangeRegion(int direction)
    {
        MapRunState.MoveRegion(direction, RegionPresets.Length, "start");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private bool IsAvailable(MapNode node)
    {
        if (node == null || node.id == MapRunState.CurrentNodeId || MapRunState.IsCompleted(node.id))
            return false;

        var current = GetNode(MapRunState.CurrentNodeId);
        return current != null && current.nextIds.Contains(node.id);
    }

    private string GetNodeStatus(MapNode node)
    {
        if (node.id == MapRunState.CurrentNodeId)
            return "현재 위치";
        if (MapRunState.IsCompleted(node.id))
            return "완료";
        if (IsAvailable(node))
            return "선택 가능";
        return "잠김";
    }

    private IEnumerator FocusNodeNextFrame(MapNode node)
    {
        yield return null;
        if (node == null || mapScroll == null || mapScroll.viewport == null)
            yield break;

        Canvas.ForceUpdateCanvases();
        var viewportWidth = mapScroll.viewport.rect.width;
        var scrollableWidth = Mathf.Max(1f, MapWidth - viewportWidth);
        var target = Mathf.Clamp(node.position.x - viewportWidth * 0.5f, 0f, scrollableWidth);
        mapScroll.horizontalNormalizedPosition = target / scrollableWidth;
    }

    private void NudgeMap(float amount)
    {
        mapScroll.horizontalNormalizedPosition = Mathf.Clamp01(mapScroll.horizontalNormalizedPosition + amount);
    }

    private void AddNode(
        string id,
        string title,
        string type,
        string route,
        string eventId,
        string description,
        int difficulty,
        float x,
        float y)
    {
        var position = ApplyFixedRegionLayout(x, y);
        if (string.IsNullOrWhiteSpace(eventId))
        {
            title = $"{activeRegion.name} 입구";
            description = $"{activeRegion.name}의 고정 시작 지점이다.";
        }
        else
        {
            eventId = RemapEventId(eventId, activeRegion.eventShift);
            if (eventSummaryById.TryGetValue(eventId, out var summary))
                title = summary.title;
            description = $"{activeRegion.theme} 구간에 배치된 고정 노드다. {eventId} 사건이 발생한다.";
            difficulty = Mathf.Clamp(difficulty + activeRegion.difficultyOffset, 1, 5);
        }

        var node = new MapNode(id, title, type, route, eventId, description, difficulty, position);
        nodes.Add(node);
        nodeById.Add(id, node);
    }

    private Vector2 ApplyFixedRegionLayout(float x, float y)
    {
        switch (activeRegion.layoutVariant)
        {
            case 0:
                return new Vector2(x, y * 0.84f);
            case 1:
                return new Vector2(x, y);
            case 2:
                return new Vector2(x, -y * 1.08f + CommonLaneOffset(x, 34f));
            case 3:
                return new Vector2(x, y * 1.12f + CommonLaneOffset(x, -42f));
            case 4:
                return new Vector2(x, -y * 0.96f + CommonLaneOffset(x, 58f));
            default:
                return new Vector2(x, y);
        }
    }

    private static float CommonLaneOffset(float x, float amount)
    {
        if (x < 850f || x > 2650f)
            return 0f;
        var phase = Mathf.FloorToInt(x / 400f) % 2 == 0 ? 1f : -1f;
        return phase * amount;
    }

    private static string RemapEventId(string baseEventId, int shift)
    {
        if (string.IsNullOrWhiteSpace(baseEventId) || baseEventId.Length < 4 ||
            !int.TryParse(baseEventId.Substring(4), out var number))
            return baseEventId;

        var remapped = ((number - 1 + shift) % 60) + 1;
        return $"EVT_{remapped:000}";
    }

    private void Connect(string fromId, params string[] toIds)
    {
        var from = GetNode(fromId);
        if (from == null)
            return;
        from.nextIds.AddRange(toIds);
    }

    private MapNode GetNode(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && nodeById.TryGetValue(id, out var node) ? node : null;
    }

    private Color NodeTypeColor(string type)
    {
        if (type.Contains("긍정")) return positiveColor;
        if (type.Contains("부정")) return negativeColor;
        if (type.Contains("상점")) return shopColor;
        if (type.Contains("보스")) return bossColor;
        if (type.Contains("특수") || type.Contains("약화")) return specialColor;
        if (type.Contains("전투") || type.Contains("필수")) return negativeColor;
        return neutralColor;
    }

    private static string ShortType(string type)
    {
        if (type.Contains("보스")) return "BOSS";
        if (type.Contains("전투")) return "전투";
        if (type.Contains("상점")) return "상점";
        if (type.Contains("긍정")) return "긍정";
        if (type.Contains("부정")) return "부정";
        if (type.Contains("특수") || type.Contains("약화")) return "특수";
        if (type.Contains("필수")) return "필수";
        if (type.Contains("시작")) return "START";
        return "사건";
    }

    private static string RouteLabel(string route)
    {
        switch (route)
        {
            case "north-a": return "초반 북부 보급로";
            case "center-a": return "초반 중앙 지름길";
            case "south-a": return "초반 남부 협상로";
            case "upper-b": return "중반 상단 안전로";
            case "lower-b": return "중반 하단 고위험로";
            case "north-c": return "후반 정보 경로";
            case "center-c": return "후반 정면 경로";
            case "south-c": return "후반 재화 경로";
            default: return "공통 합류 구간";
        }
    }

    private static string ToRoman(int value)
    {
        switch (value)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            default: return "-";
        }
    }

    private RectTransform CreateLine(RectTransform parent, Vector2 start, Vector2 end, Color color, float thickness)
    {
        var line = CreateRect("RouteLine", parent);
        var image = line.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        var delta = end - start;
        line.anchorMin = new Vector2(0f, 0.5f);
        line.anchorMax = new Vector2(0f, 0.5f);
        line.pivot = new Vector2(0.5f, 0.5f);
        line.sizeDelta = new Vector2(delta.magnitude, thickness);
        line.anchoredPosition = (start + end) * 0.5f;
        line.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        return line;
    }

    private RectTransform CreateImage(
        string name,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        var rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    private Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        Color color,
        TextAnchor alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        var rect = CreateRect(name, parent);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var text = rect.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        int fontSize)
    {
        var rect = CreateImage(name, parent, panelStrongColor, anchorMin, anchorMax, offsetMin, offsetMax);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(225, 230, 235, 255);
        colors.pressedColor = new Color32(180, 188, 197, 255);
        colors.disabledColor = new Color32(75, 81, 88, 180);
        button.colors = colors;

        CreateText("Label", rect, label, fontSize, inkColor, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(6f, 4f), new Vector2(-6f, -4f));
        return button;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        var rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private sealed class RegionPreset
    {
        public readonly string id;
        public readonly string name;
        public readonly string theme;
        public readonly string danger;
        public readonly string reward;
        public readonly int eventShift;
        public readonly int layoutVariant;
        public readonly int difficultyOffset;

        public RegionPreset(
            string id,
            string name,
            string theme,
            string danger,
            string reward,
            int eventShift,
            int layoutVariant,
            int difficultyOffset)
        {
            this.id = id;
            this.name = name;
            this.theme = theme;
            this.danger = danger;
            this.reward = reward;
            this.eventShift = eventShift;
            this.layoutVariant = layoutVariant;
            this.difficultyOffset = difficultyOffset;
        }
    }

    [Serializable]
    private sealed class EventSummaryDatabase
    {
        public EventSummary[] events;
    }

    [Serializable]
    private sealed class EventSummary
    {
        public string eventId;
        public string title;
    }

    [Serializable]
    private sealed class MapNode
    {
        public readonly string id;
        public readonly string title;
        public readonly string type;
        public readonly string route;
        public readonly string eventId;
        public readonly string description;
        public readonly int difficulty;
        public readonly Vector2 position;
        public readonly List<string> nextIds = new List<string>();

        public MapNode(
            string id,
            string title,
            string type,
            string route,
            string eventId,
            string description,
            int difficulty,
            Vector2 position)
        {
            this.id = id;
            this.title = title;
            this.type = type;
            this.route = route;
            this.eventId = eventId;
            this.description = description;
            this.difficulty = difficulty;
            this.position = position;
        }
    }

    private sealed class MapEdgeView
    {
        public readonly string fromId;
        public readonly string toId;
        public readonly Image image;

        public MapEdgeView(string fromId, string toId, RectTransform line)
        {
            this.fromId = fromId;
            this.toId = toId;
            image = line.GetComponent<Image>();
        }
    }

    private readonly struct NodeDistance
    {
        public readonly string nodeId;
        public readonly int distance;

        public NodeDistance(string nodeId, int distance)
        {
            this.nodeId = nodeId;
            this.distance = distance;
        }
    }
}

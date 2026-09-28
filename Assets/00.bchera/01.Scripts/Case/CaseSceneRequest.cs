using UnityEngine;
using UnityEngine.SceneManagement;

public static class CaseSceneRequest
{
    public const string CaseSceneName = "case";
    public const string DefaultReturnSceneName = "InGameMap";

    private static string requestedEventId;
    private static string returnSceneName;
    private static string sourceNodeId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        Clear();
    }

    public static void OpenEvent(
        string eventId,
        string returnToScene = DefaultReturnSceneName,
        string mapNodeId = null)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            Debug.LogError("사건 ID가 비어 있어 case 씬을 열지 않았습니다.");
            return;
        }

        RequestEvent(eventId, returnToScene, mapNodeId);

        if (!Application.CanStreamedLevelBeLoaded(CaseSceneName))
        {
            Debug.LogError($"사건 씬이 Build Settings에 없습니다: {CaseSceneName}");
            Clear();
            MapRunState.CancelPendingNode();
            return;
        }

        SceneManager.LoadScene(CaseSceneName);
    }

    public static void RequestEvent(
        string eventId,
        string returnToScene = DefaultReturnSceneName,
        string mapNodeId = null)
    {
        requestedEventId = eventId?.Trim();
        returnSceneName = string.IsNullOrWhiteSpace(returnToScene)
            ? DefaultReturnSceneName
            : returnToScene.Trim();
        sourceNodeId = mapNodeId?.Trim();
    }

    public static bool TryTake(out string eventId, out string returnToScene, out string mapNodeId)
    {
        eventId = requestedEventId;
        returnToScene = string.IsNullOrWhiteSpace(returnSceneName)
            ? DefaultReturnSceneName
            : returnSceneName;
        mapNodeId = sourceNodeId;

        var hasRequest = !string.IsNullOrWhiteSpace(eventId);
        Clear();
        return hasRequest;
    }

    public static void Clear()
    {
        requestedEventId = null;
        returnSceneName = null;
        sourceNodeId = null;
    }
}

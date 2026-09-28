using System.Collections.Generic;
using UnityEngine;

public static class MapRunState
{
    private const string DefaultStartNodeId = "start";
    private static readonly HashSet<string> CompletedNodeIds = new HashSet<string>();

    public static int CurrentRegionIndex { get; private set; }
    public static string CurrentNodeId { get; private set; }
    public static string PendingNodeId { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        CurrentRegionIndex = 0;
        Reset(DefaultStartNodeId);
    }

    public static void EnsureStarted(string startNodeId = DefaultStartNodeId)
    {
        if (string.IsNullOrWhiteSpace(CurrentNodeId))
            Reset(startNodeId);
    }

    public static void BeginNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        PendingNodeId = nodeId;
    }

    public static void CompleteNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        EnsureStarted();
        CompletedNodeIds.Add(CurrentNodeId);
        CompletedNodeIds.Add(nodeId);
        CurrentNodeId = nodeId;
        PendingNodeId = null;
    }

    public static void CancelPendingNode()
    {
        PendingNodeId = null;
    }

    public static bool IsCompleted(string nodeId)
    {
        return !string.IsNullOrWhiteSpace(nodeId) && CompletedNodeIds.Contains(nodeId);
    }

    public static void Reset(string startNodeId = DefaultStartNodeId)
    {
        CompletedNodeIds.Clear();
        CurrentNodeId = string.IsNullOrWhiteSpace(startNodeId) ? DefaultStartNodeId : startNodeId;
        CompletedNodeIds.Add(CurrentNodeId);
        PendingNodeId = null;
    }

    public static void SetRegion(int regionIndex, int regionCount, string startNodeId = DefaultStartNodeId)
    {
        if (regionCount <= 0)
            regionCount = 1;

        CurrentRegionIndex = Mathf.Clamp(regionIndex, 0, regionCount - 1);
        Reset(startNodeId);
    }

    public static void MoveRegion(int direction, int regionCount, string startNodeId = DefaultStartNodeId)
    {
        if (regionCount <= 0)
            regionCount = 1;

        var nextIndex = (CurrentRegionIndex + direction) % regionCount;
        if (nextIndex < 0)
            nextIndex += regionCount;
        SetRegion(nextIndex, regionCount, startNodeId);
    }
}

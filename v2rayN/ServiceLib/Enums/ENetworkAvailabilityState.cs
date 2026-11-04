namespace ServiceLib.Enums;

public enum ENetworkAvailabilityState
{
    Checking,
    Available,
    Repairing,
    Repaired,
    NoAvailableNode,
    LocalNetworkUnavailable,
    Idle,
    LocalNodesUnavailable,
    SetupFailed
}

namespace ServiceLib.Models.Dto;

public sealed record NetworkAvailabilityInfo(
    ENetworkAvailabilityState State,
    string Message,
    string Detail = "",
    long? MonitorRevision = null);

namespace ServiceLib.Models.Dto;

public sealed record SubscriptionLatencyTestResult(
    IReadOnlyList<string> SubscriptionIds,
    int TestedCount,
    ProfileItemModel? BestProfile,
    IReadOnlySet<string> AvailableSubscriptionIds);

namespace TestShieldAI.Api.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool Enabled { get; set; }

    public bool NotifyOnSafe { get; set; }

    public bool NotifyOnReview { get; set; } = true;

    public bool NotifyOnBlock { get; set; } = true;

    public int MaxFindings { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 10;

    public string DashboardBaseUrl { get; set; } = "";
}

namespace TestShieldAI.Api.Notifications;

public sealed class NoOpRunCompletedNotifier : IRunCompletedNotifier
{
    public Task NotifyAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

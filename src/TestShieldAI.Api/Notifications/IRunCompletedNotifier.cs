namespace TestShieldAI.Api.Notifications;

public interface IRunCompletedNotifier
{
    Task NotifyAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken);
}

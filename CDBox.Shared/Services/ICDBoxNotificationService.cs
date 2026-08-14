namespace CDBox.Shared.Services
{
    public enum CDBoxNotificationLevel
    {
        Information,
        Success,
        Warning,
        Error
    }

    public interface ICDBoxNotificationService
    {
        void Show(string title, string message, CDBoxNotificationLevel level);
    }
}

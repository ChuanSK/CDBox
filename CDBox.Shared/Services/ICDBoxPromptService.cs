using System;

namespace CDBox.Shared.Services
{
    public interface ICDBoxPromptService
    {
        IDisposable Begin(string title, string message);
    }
}

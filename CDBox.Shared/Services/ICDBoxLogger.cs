using System;

namespace CDBox.Shared.Services
{
    public interface ICDBoxLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception exception);
    }
}

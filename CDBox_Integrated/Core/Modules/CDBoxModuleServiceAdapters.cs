using System;
using CDBox.Shared.Services;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.Core.Modules
{
    internal sealed class CDBoxModuleLoggerAdapter : ICDBoxLogger
    {
        private readonly string _prefix;

        public CDBoxModuleLoggerAdapter(string moduleId)
        {
            _prefix = "[" + (string.IsNullOrWhiteSpace(moduleId)
                ? "Module" : moduleId.Trim()) + "] ";
        }

        public void Info(string message)
        {
            CDBoxStudioLogger.Info(_prefix + (message ?? string.Empty));
        }

        public void Warn(string message)
        {
            CDBoxStudioLogger.Warn(_prefix + (message ?? string.Empty));
        }

        public void Error(string message, Exception exception)
        {
            CDBoxStudioLogger.Error(
                _prefix + (message ?? string.Empty), exception);
        }
    }

    internal sealed class CDBoxModuleNotificationAdapter
        : ICDBoxNotificationService
    {
        public void Show(
            string title,
            string message,
            CDBoxNotificationLevel level)
        {
            try
            {
                CDBoxNotificationService.Notify(
                    title,
                    message,
                    Map(level));
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error(
                    "业务模块通知显示失败：" + (title ?? string.Empty),
                    ex);
            }
        }

        private static CDBoxNotificationKind Map(
            CDBoxNotificationLevel level)
        {
            if (level == CDBoxNotificationLevel.Success)
                return CDBoxNotificationKind.Success;
            if (level == CDBoxNotificationLevel.Warning)
                return CDBoxNotificationKind.Warning;
            if (level == CDBoxNotificationLevel.Error)
                return CDBoxNotificationKind.Error;
            return CDBoxNotificationKind.Information;
        }
    }
}

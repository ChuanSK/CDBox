using System;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using TCPipeAutoDraw.Core.Colors;
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

    internal sealed class CDBoxModulePromptAdapter : ICDBoxPromptService
    {
        public IDisposable Begin(string title, string message)
        {
            return CDBoxNotificationService.BeginCommandPrompt(
                title, message);
        }
    }

    internal sealed class CDBoxModuleColorPickerAdapter
        : ICDBoxColorPickerService
    {
        public bool TryPick(CDBoxModuleColor initial,
            out CDBoxModuleColor selected, bool allowByLayer,
            bool allowByBlock)
        {
            CDBoxColor picked;
            bool accepted = CDBoxStudioColorPickerWindow.TryPick(
                ToHost(initial), out picked, allowByLayer, allowByBlock,
                true, true, true);
            selected = accepted ? FromHost(picked) : null;
            return accepted;
        }

        private static CDBoxColor ToHost(CDBoxModuleColor value)
        {
            value = value ?? CDBoxModuleColor.FromIndex(7);
            return new CDBoxColor
            {
                Type = (CDBoxColorType)(int)value.Type,
                Index = value.Index,
                R = value.R,
                G = value.G,
                B = value.B,
                BookName = value.BookName ?? string.Empty,
                ColorName = value.ColorName ?? string.Empty,
                DisplayName = value.DisplayName ?? string.Empty
            };
        }

        private static CDBoxModuleColor FromHost(CDBoxColor value)
        {
            value = value ?? CDBoxColor.FromIndex(7);
            return new CDBoxModuleColor
            {
                Type = (CDBoxModuleColorType)(int)value.Type,
                Index = value.Index,
                R = value.R,
                G = value.G,
                B = value.B,
                BookName = value.BookName ?? string.Empty,
                ColorName = value.ColorName ?? string.Empty,
                DisplayName = value.DisplayName ?? string.Empty
            };
        }
    }
}

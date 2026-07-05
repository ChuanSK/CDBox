using System;

namespace TCPipeAutoDraw.UI.Studio
{
    internal enum CDBoxStudioActionKind
    {
        Module,
        Command
    }

    internal sealed class CDBoxStudioAction
    {
        public CDBoxStudioAction(string id, string title, string category, string description, string commandName, string badgeText, CDBoxStudioActionKind kind, bool enabled, bool restoreStudioAfterRun, Action runAction)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("动作 Id 不能为空。", "id");
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("动作标题不能为空。", "title");

            Id = id;
            Title = title;
            Category = string.IsNullOrWhiteSpace(category) ? "其他" : category.Trim();
            Description = description ?? string.Empty;
            CommandName = commandName ?? string.Empty;
            BadgeText = badgeText ?? string.Empty;
            Kind = kind;
            Enabled = enabled;
            RestoreStudioAfterRun = restoreStudioAfterRun;
            RunAction = runAction;
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Category { get; private set; }
        public string Description { get; private set; }
        public string CommandName { get; private set; }
        public string BadgeText { get; private set; }
        public CDBoxStudioActionKind Kind { get; private set; }
        public bool Enabled { get; private set; }
        public bool RestoreStudioAfterRun { get; private set; }
        public Action RunAction { get; private set; }

        public void Run()
        {
            if (!Enabled || RunAction == null) return;
            RunAction();
        }
    }
}

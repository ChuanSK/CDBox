using System;
using CDBox.Shared.Modules;
using CDBox.Shared.Services;

namespace CDBox.CoreTests
{
    public sealed class FailingWorkspaceModule : ICDBoxWorkspaceModule
    {
        public string Id { get { return "failing-test-module"; } }
        public string Name { get { return "Failing Test Module"; } }
        public string Version { get { return "0.0.0"; } }

        public void Initialize(ICDBoxServices services)
        {
            throw new InvalidOperationException(
                "Expected module initialization failure.");
        }

        public void OpenWorkspace()
        {
        }

        public void Shutdown()
        {
        }
    }
}

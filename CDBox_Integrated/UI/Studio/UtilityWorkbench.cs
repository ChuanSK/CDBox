using System;
using System.IO;

namespace TCPipeAutoDraw.UI.Studio
{
    // Embedded in the base component so standalone tools share the installed theme
    // without requiring loose web assets or business-module presentation code.
    public static class UtilityWorkbench
    {
        public static string Apply(string document) { return CommonWorkbench.Apply(document); }

        public static string Read(string name)
        {
            using (var stream = typeof(UtilityWorkbench).Assembly.GetManifestResourceStream("CDBox.Workbench." + name))
            {
                if (stream == null) throw new InvalidOperationException("Missing workbench resource: " + name);
                using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
            }
        }
    }
}

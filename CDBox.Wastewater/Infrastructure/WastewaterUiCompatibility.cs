using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Xml.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using CDBox.Shared.UI;
using CDBox.Wastewater.Module;
using TCPipeAutoDraw.Core.Colors;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioSettings
    {
        public string Theme { get; set; }
        public bool AnimationsEnabled { get; set; }
        public double AnnotationHudNormalOpacity { get; set; }
        public double AnnotationHudHoverOpacity { get; set; }
        public bool AnnotationHudGlowEnabled { get; set; }
        public double AnnotationHudGlowIntensity { get; set; }
        public bool DoubleClickOpenEnabled { get; set; }

        public CDBoxStudioSettings()
        {
            Theme = "light";
            AnimationsEnabled = true;
            AnnotationHudNormalOpacity = 0.68;
            AnnotationHudHoverOpacity = 1.0;
            AnnotationHudGlowEnabled = true;
            AnnotationHudGlowIntensity = 0.28;
            DoubleClickOpenEnabled = true;
        }

        public void Normalize()
        {
            string theme = (Theme ?? string.Empty).Trim().ToLowerInvariant();
            Theme = theme == "dark" || theme == "fresh"
                ? theme : "light";
            AnnotationHudNormalOpacity = Math.Max(0.1,
                Math.Min(1.0, AnnotationHudNormalOpacity));
            AnnotationHudHoverOpacity = Math.Max(
                AnnotationHudNormalOpacity,
                Math.Min(1.0, AnnotationHudHoverOpacity));
            AnnotationHudGlowIntensity = Math.Max(0.0,
                Math.Min(1.0, AnnotationHudGlowIntensity));
        }
    }

    internal static class CDBoxStudioSettingsStore
    {
        public static CDBoxStudioSettings Load()
        {
            var settings = new CDBoxStudioSettings();
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData), "CDBox",
                    "Studio", "studio-settings.xml");
                if (!File.Exists(path)) return settings;
                XElement root = XDocument.Load(path).Root;
                if (root == null) return settings;
                settings.Theme = ((string)root.Element("Theme")
                    ?? settings.Theme).Trim();
                settings.AnimationsEnabled = Bool(root,
                    "AnimationsEnabled", settings.AnimationsEnabled);
                settings.AnnotationHudNormalOpacity = Number(root,
                    "AnnotationHudNormalOpacity",
                    settings.AnnotationHudNormalOpacity);
                settings.AnnotationHudHoverOpacity = Number(root,
                    "AnnotationHudHoverOpacity",
                    settings.AnnotationHudHoverOpacity);
                settings.AnnotationHudGlowEnabled = Bool(root,
                    "AnnotationHudGlowEnabled",
                    settings.AnnotationHudGlowEnabled);
                settings.AnnotationHudGlowIntensity = Number(root,
                    "AnnotationHudGlowIntensity",
                    settings.AnnotationHudGlowIntensity);
                settings.DoubleClickOpenEnabled = Bool(root,
                    "DoubleClickOpenEnabled",
                    settings.DoubleClickOpenEnabled);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("污水标注界面设置读取失败。", ex);
            }
            return settings;
        }

        private static bool Bool(XElement root, string name, bool fallback)
        {
            bool value;
            return bool.TryParse((string)root.Element(name), out value)
                ? value : fallback;
        }

        private static double Number(XElement root, string name,
            double fallback)
        {
            double value;
            return double.TryParse((string)root.Element(name),
                NumberStyles.Float, CultureInfo.InvariantCulture,
                out value) ? value : fallback;
        }
    }

    internal static class CDBoxStudioLogger
    {
        public static void Error(string message, Exception exception)
        {
            if (WastewaterRuntimeServices.Logger != null)
                WastewaterRuntimeServices.Logger.Error(message, exception);
        }
    }
}

namespace TCPipeAutoDraw.UI.Controls
{
    internal static class ColorPickerWindow
    {
        public static bool TryPick(CDBoxColor initial,
            out CDBoxColor selected, bool allowByLayer = true,
            bool allowByBlock = true, bool allowTrueColor = true,
            bool allowColorBook = true, bool allowStandard = true,
            Window owner = null)
        {
            ICDBoxColorPickerService picker =
                WastewaterRuntimeServices.ColorPicker;
            if (picker == null)
            {
                selected = initial == null ? CDBoxColor.FromIndex(7)
                    : initial.Clone();
                return false;
            }
            CDBoxModuleColor result;
            bool accepted = picker.TryPick(ToModule(initial), out result,
                allowByLayer, allowByBlock);
            selected = accepted ? FromModule(result)
                : (initial == null ? CDBoxColor.FromIndex(7)
                    : initial.Clone());
            return accepted;
        }

        private static CDBoxModuleColor ToModule(CDBoxColor value)
        {
            value = value ?? CDBoxColor.FromIndex(7);
            return new CDBoxModuleColor
            {
                Type = (CDBoxModuleColorType)(int)value.Type,
                Index = value.Index, R = value.R, G = value.G, B = value.B,
                BookName = value.BookName ?? string.Empty,
                ColorName = value.ColorName ?? string.Empty,
                DisplayName = value.DisplayName ?? string.Empty
            };
        }

        private static CDBoxColor FromModule(CDBoxModuleColor value)
        {
            value = value ?? CDBoxModuleColor.FromIndex(7);
            return new CDBoxColor
            {
                Type = (CDBoxColorType)(int)value.Type,
                Index = value.Index, R = value.R, G = value.G, B = value.B,
                BookName = value.BookName ?? string.Empty,
                ColorName = value.ColorName ?? string.Empty,
                DisplayName = value.DisplayName ?? string.Empty
            };
        }
    }
}

namespace TCPipeAutoDraw.UI
{
    internal sealed class AcadMainWindow : IWin32Window
    {
        public IntPtr Handle
        {
            get { return Process.GetCurrentProcess().MainWindowHandle; }
        }
    }

    internal static class CDBoxMessageBox
    {
        public static DialogResult Show(IWin32Window owner, string text,
            string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return System.Windows.Forms.MessageBox.Show(owner, text,
                caption, buttons, icon);
        }

        public static DialogResult Show(string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return System.Windows.Forms.MessageBox.Show(text, caption,
                buttons, icon);
        }

        public static DialogResult Show(string text, string caption,
            MessageBoxButtons buttons)
        {
            return System.Windows.Forms.MessageBox.Show(text, caption,
                buttons);
        }
    }

    public sealed class CDBoxProgressSession : IDisposable
    {
        private readonly IDisposable _prompt;
        private readonly Document _document;

        private CDBoxProgressSession(Document document, IDisposable prompt)
        {
            _document = document;
            _prompt = prompt;
        }

        public static CDBoxProgressSession Start(Document document,
            string title, string message, string source = null,
            string mergeKey = null, bool indeterminate = true)
        {
            IDisposable prompt = WastewaterRuntimeServices.Prompts == null
                ? null : WastewaterRuntimeServices.Prompts.Begin(title,
                    message);
            return new CDBoxProgressSession(document, prompt);
        }

        public void Report(int current, int total, string message)
        {
            if (current == 0 || current == total) Write(message);
        }

        public void ReportMarquee(string message) { Write(message); }
        public void Complete(string message) { Write(message); }
        public void Fail(string message) { Write(message); }
        public void Cancel(string message) { Write(message); }

        private void Write(string message)
        {
            try
            {
                if (_document != null && _document.Editor != null
                    && !string.IsNullOrWhiteSpace(message))
                    _document.Editor.WriteMessage("\n[CDBox] " + message);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_prompt != null) _prompt.Dispose();
        }
    }
}

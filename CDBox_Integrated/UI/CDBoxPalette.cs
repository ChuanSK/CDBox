using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.Windows;
using TCPipeAutoDraw.Core.Modules;

namespace TCPipeAutoDraw.UI
{
    internal static class CDBoxPalette
    {
        private static readonly Guid PaletteGuid = new Guid("6A9A2CB6-5845-4D7E-9874-59F6D4F4A2E1");
        private static PaletteSet _palette;
        private static CDBoxSidebarControl _control;
        private static Panel _hostPanel;

        public static bool IsVisible
        {
            get { return _palette != null && _palette.Visible; }
        }

        public static void Show(IEnumerable<ITCModule> modules)
        {
            EnsureCreated(modules);
            _palette.Visible = true;
        }

        public static void Hide()
        {
            if (_palette != null) _palette.Visible = false;
        }

        public static void Toggle(IEnumerable<ITCModule> modules)
        {
            EnsureCreated(modules);
            _palette.Visible = !_palette.Visible;
        }

        public static void RefreshModules(IEnumerable<ITCModule> modules)
        {
            EnsureCreated(modules);
            if (_control != null) _control.SetModules(modules);
        }

        private static void EnsureCreated(IEnumerable<ITCModule> modules)
        {
            if (_palette == null)
            {
                _palette = new PaletteSet("CDBox", PaletteGuid);
                _palette.Style = PaletteSetStyles.ShowAutoHideButton
                    | PaletteSetStyles.ShowCloseButton
                    | PaletteSetStyles.ShowPropertiesMenu;
                _palette.DockEnabled = DockSides.Left | DockSides.Right;
                _palette.MinimumSize = new Size(220, 360);
                _palette.Size = new Size(260, 560);

                _hostPanel = new Panel();
                _hostPanel.Dock = DockStyle.Fill;
                _hostPanel.Margin = Padding.Empty;
                _hostPanel.Padding = Padding.Empty;
                _hostPanel.BackColor = Color.FromArgb(54, 54, 54);

                _control = new CDBoxSidebarControl();
                _control.SetModules(modules);
                _hostPanel.Controls.Add(_control);
                _palette.Add("工具箱", _hostPanel);
            }
            else if (_control != null)
            {
                _control.SetModules(modules);
            }
        }
    }
}

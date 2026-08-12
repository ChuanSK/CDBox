using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Forms;
using TCPipeAutoDraw.Core.FloatingCenter;

namespace TCPipeAutoDraw.UI.FloatingCenter
{
    internal static class FloatingCenterScreenMetrics
    {
        public static IList<FloatingWorkArea> GetWorkAreas(Window window)
        {
            double scaleX;
            double scaleY;
            GetScale(window, out scaleX, out scaleY);
            var areas = new List<FloatingWorkArea>();
            foreach (Screen screen in Screen.AllScreens)
            {
                System.Drawing.Rectangle value = screen.WorkingArea;
                areas.Add(new FloatingWorkArea
                {
                    DeviceName = screen.DeviceName,
                    IsPrimary = screen.Primary,
                    Left = value.Left / scaleX,
                    Top = value.Top / scaleY,
                    Width = value.Width / scaleX,
                    Height = value.Height / scaleY
                });
            }
            return areas;
        }

        public static FloatingWorkArea GetCurrentWorkArea(Window window)
        {
            double scaleX;
            double scaleY;
            GetScale(window, out scaleX, out scaleY);
            IntPtr handle = window == null ? IntPtr.Zero :
                new WindowInteropHelper(window).Handle;
            Screen screen = handle == IntPtr.Zero
                ? Screen.PrimaryScreen : Screen.FromHandle(handle);
            System.Drawing.Rectangle value = screen.WorkingArea;
            return new FloatingWorkArea
            {
                DeviceName = screen.DeviceName,
                IsPrimary = screen.Primary,
                Left = value.Left / scaleX,
                Top = value.Top / scaleY,
                Width = value.Width / scaleX,
                Height = value.Height / scaleY
            };
        }

        private static void GetScale(Window window, out double scaleX,
            out double scaleY)
        {
            scaleX = 1.0;
            scaleY = 1.0;
            try
            {
                DpiScale dpi = window == null
                    ? new DpiScale(1.0, 1.0)
                    : VisualTreeHelper.GetDpi(window);
                if (dpi.DpiScaleX > 0.0) scaleX = dpi.DpiScaleX;
                if (dpi.DpiScaleY > 0.0) scaleY = dpi.DpiScaleY;
            }
            catch { }
        }
    }
}

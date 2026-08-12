using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TCPipeAutoDraw.UI.FloatingCenter
{
    internal static class FloatingWindowInterop
    {
        private const int ExtendedStyle = -20;
        private const long NoActivate = 0x08000000L;
        private const long Transparent = 0x00000020L;

        public static void SetNoActivate(Window window)
        {
            SetStyle(window, NoActivate, true);
        }

        public static void SetClickThrough(Window window, bool enabled)
        {
            SetStyle(window, Transparent, enabled);
        }

        private static void SetStyle(Window window, long flag, bool enabled)
        {
            if (window == null) return;
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero) return;
                IntPtr current = GetWindowLongPtr(handle, ExtendedStyle);
                long style = current.ToInt64();
                style = enabled ? style | flag : style & ~flag;
                SetWindowLongPtr(handle, ExtendedStyle, new IntPtr(style));
            }
            catch { }
        }

        private static IntPtr GetWindowLongPtr(IntPtr handle, int index)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(handle, index)
                : new IntPtr(GetWindowLong32(handle, index));
        }

        private static IntPtr SetWindowLongPtr(IntPtr handle, int index,
            IntPtr value)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(handle, index, value)
                : new IntPtr(SetWindowLong32(handle, index,
                    value.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr handle, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr handle,
            int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr handle, int index,
            int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr handle,
            int index, IntPtr value);
    }
}

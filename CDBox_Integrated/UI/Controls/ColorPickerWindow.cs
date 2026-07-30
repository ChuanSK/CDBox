using System;
using System.Windows;
using System.Windows.Interop;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.UI.Controls
{
    /// <summary>
    /// Compatibility entry point retained for existing modules. The actual
    /// interface is hosted by the shared WebView2 page window.
    /// </summary>
    internal static class ColorPickerWindow
    {
        public static bool TryPick(CDBoxColor initial, out CDBoxColor selected,
            bool allowByLayer = true, bool allowByBlock = true,
            bool allowTrueColor = true, bool allowColorBook = true,
            bool allowStandard = true, Window owner = null)
        {
            System.Windows.Forms.IWin32Window nativeOwner = owner == null
                ? null : new NativeWindowOwner(new WindowInteropHelper(owner).Handle);
            return CDBoxStudioColorPickerWindow.TryPick(initial, out selected,
                allowByLayer, allowByBlock, allowTrueColor, allowColorBook,
                allowStandard, nativeOwner);
        }

        private sealed class NativeWindowOwner : System.Windows.Forms.IWin32Window
        {
            public NativeWindowOwner(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; private set; }
        }
    }
}

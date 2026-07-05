namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioRouteResult
    {
        public CDBoxStudioAction ActionToRun { get; set; }
        public bool RefreshPage { get; set; }
        public bool Handled { get; set; }
        public string ToastMessage { get; set; }
        public string ToastKind { get; set; }
        public string WindowTitleSuffix { get; set; }
    }
}

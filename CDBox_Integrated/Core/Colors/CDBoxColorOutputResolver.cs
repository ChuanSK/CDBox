namespace TCPipeAutoDraw.Core.Colors
{
    public static class CDBoxColorOutputResolver
    {
        public static CDBoxColor Resolve(CDBoxColor selected, CDBoxColor original,
            CDBoxColorOutputMode mode)
        {
            selected = (selected ?? original ?? CDBoxColor.FromIndex(7)).Clone();
            original = original == null ? null : original.Clone();
            if (selected.Type == CDBoxColorType.ByLayer || selected.Type == CDBoxColorType.ByBlock) return selected;

            switch (mode)
            {
                case CDBoxColorOutputMode.PreferIndexColor:
                    return CDBoxColor.FromIndex(CDBoxColorConverter.RgbToNearestAci(selected.R, selected.G, selected.B));
                case CDBoxColorOutputMode.PreferTrueColor:
                    return CDBoxColor.FromRgb(selected.R, selected.G, selected.B);
                case CDBoxColorOutputMode.CDBoxStandard:
                    return CDBoxStandardColorService.FindNearest(selected.R, selected.G, selected.B);
                default:
                    if (original == null) return selected;
                    if (original.Type == CDBoxColorType.IndexColor)
                        return CDBoxColor.FromIndex(CDBoxColorConverter.RgbToNearestAci(selected.R, selected.G, selected.B));
                    if (original.Type == CDBoxColorType.TrueColor || original.Type == CDBoxColorType.CDBoxStandard)
                        return CDBoxColor.FromRgb(selected.R, selected.G, selected.B);
                    if (original.Type == CDBoxColorType.ColorBook && selected.Type != CDBoxColorType.ColorBook)
                        return CDBoxColor.FromRgb(selected.R, selected.G, selected.B);
                    return selected;
            }
        }
    }
}

namespace CDBox.Shared.UI
{
    public enum CDBoxModuleColorType
    {
        ByLayer,
        ByBlock,
        IndexColor,
        TrueColor,
        ColorBook,
        CDBoxStandard
    }

    public sealed class CDBoxModuleColor
    {
        public CDBoxModuleColor()
        {
            Type = CDBoxModuleColorType.IndexColor;
            Index = 7;
            R = 255;
            G = 255;
            B = 255;
            BookName = string.Empty;
            ColorName = string.Empty;
            DisplayName = string.Empty;
        }

        public CDBoxModuleColorType Type { get; set; }
        public int Index { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public string BookName { get; set; }
        public string ColorName { get; set; }
        public string DisplayName { get; set; }

        public CDBoxModuleColor Clone()
        {
            return new CDBoxModuleColor
            {
                Type = Type,
                Index = Index,
                R = R,
                G = G,
                B = B,
                BookName = BookName ?? string.Empty,
                ColorName = ColorName ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty
            };
        }

        public static CDBoxModuleColor FromIndex(int index)
        {
            return new CDBoxModuleColor
            {
                Type = CDBoxModuleColorType.IndexColor,
                Index = index < 1 || index > 255 ? 7 : index,
                DisplayName = "ACI " + (index < 1 || index > 255 ? 7 : index)
            };
        }
    }

    public interface ICDBoxColorPickerService
    {
        bool TryPick(CDBoxModuleColor initial,
            out CDBoxModuleColor selected,
            bool allowByLayer,
            bool allowByBlock);
    }
}

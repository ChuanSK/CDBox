using System.Collections.Generic;

namespace CDBox.RealEstate.Settings
{
    public sealed class BuildingAnnotationCadCatalog
    {
        public BuildingAnnotationCadCatalog()
        {
            TextStyles = new List<string>();
            Linetypes = new List<string>();
        }

        public IList<string> TextStyles { get; private set; }
        public IList<string> Linetypes { get; private set; }
    }
}

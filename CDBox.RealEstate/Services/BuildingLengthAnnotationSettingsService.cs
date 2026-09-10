using System;
using CDBox.RealEstate.Cad;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.Services
{
    internal sealed class BuildingLengthAnnotationSettingsService
    {
        private readonly ICDBoxPageService _pages;
        private readonly ICDBoxColorPickerService _colors;
        private readonly BuildingLengthAnnotationCadService _cad;

        public BuildingLengthAnnotationSettingsService(
            ICDBoxPageService pages,
            ICDBoxColorPickerService colors,
            BuildingLengthAnnotationCadService cad)
        {
            _pages = pages ?? throw new ArgumentNullException("pages");
            _colors = colors ?? throw new ArgumentNullException("colors");
            _cad = cad ?? throw new ArgumentNullException("cad");
        }

        public void Open()
        {
            _pages.Show(CDBoxUiGateway.Call<CDBoxPageDefinition>(
                "realestate.building-settings", "Create",
                new Func<CDBox.RealEstate.Settings.BuildingAnnotationCadCatalog>(_cad.ReadCatalog), _colors));
        }
    }
}

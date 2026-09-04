using CDBox.Shared.Services;
using CDBox.Shared.UI;
using CDBox.Shared.Wastewater.Cad;
using CDBox.Shared.Modules;
using System;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace CDBox.Wastewater.Module
{
    internal static class WastewaterRuntimeServices
    {
        public static ICDBoxLayerClassificationService LayerClassification
        {
            get;
            set;
        }

        public static ICDBoxPromptService Prompts { get; set; }
        public static ICDBoxLogger Logger { get; set; }
        public static ICDBoxColorPickerService ColorPicker { get; set; }
        public static IWastewaterCadDataService CadData { get; set; }
        public static IQuantityDashboardMonitorService DashboardMonitor
        {
            get;
            set;
        }
        public static Action<string, string> OpenAttributeEditor { get; set; }
        public static Action OpenLongitudinalSettings { get; set; }
        public static IQuantityAttributeEditorCadService AttributeCad
        {
            get;
            set;
        }
    }
}

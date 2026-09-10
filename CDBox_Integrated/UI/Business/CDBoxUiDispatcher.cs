using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using CDBox.Shared.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>
    /// 基础 UI 唯一入口。只解析被调用的白名单类型；基础启动和其他业务
    /// 的页面不扫描或加载未安装业务的类型。
    /// </summary>
    internal static class CDBoxUiDispatcher
    {
        private static readonly Dictionary<string, string> Types = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "common.home", "CDBox.Common.UI.CommonHomePage" },
            { "common.short-code", "CDBox.Common.UI.ShortCodeRecognitionSettingsPage" },
            { "common.frame", "CDBox.Common.UI.FrameSettingsPage" },
            { "common.excel", "CDBox.Common.UI.ExcelToCadPageController" },
            { "common.choice", "TCPipeAutoDraw.UI.Studio.CDBoxStudioFrameChoiceWindow" },
            { "common.notice", "TCPipeAutoDraw.UI.Studio.CDBoxStudioFrameNoticeWindow" },
            { "realestate.building-settings", "CDBox.RealEstate.UI.BuildingLengthAnnotationSettingsPage" },
            { "realestate.building-capture", "CDBox.RealEstate.UI.BuildingCapturePage" },
            { "realestate.parcel", "CDBox.RealEstate.UI.ParcelSurveyEditorPage" },
            { "realestate.boundary-dialogs", "CDBox.RealEstate.UI.ParcelBoundaryCadDialogs" },
            { "wastewater.attributes", "CDBox.Wastewater.UI.WastewaterQuantityAttributeEditorController" },
            { "wastewater.dashboard", "CDBox.Wastewater.UI.WastewaterQuantityDashboardController" },
            { "wastewater.annotations", "CDBox.Wastewater.UI.WastewaterAnnotationSettingsPage" },
            { "wastewater.sections", "CDBox.Wastewater.UI.WastewaterSectionDrawingPageController" },
            { "wastewater.longitudinal", "CDBox.Wastewater.UI.WastewaterLongitudinalProfileSettingsController" },
            { "wastewater.annotation-interaction", "TCPipeAutoDraw.Modules.PipeLengthAnnotation.PipeLengthAnnotationInteractionService" },
            { "wastewater.overlap", "TCPipeAutoDraw.Modules.PipeLengthAnnotation.OverlappingPipeSelectionService" },
            { "base.colors", "TCPipeAutoDraw.Core.Colors.CDBoxColorService" },
            { "base.dialogs", "TCPipeAutoDraw.UI.Studio.CDBoxCommonDialogs" }
        };

        public static void Initialize() { CDBoxUiGateway.Register(Dispatch); }

        private static object Dispatch(string id, object instance, string operation, object[] arguments)
        {
            string typeName;
            if (id == null || !Types.TryGetValue(id, out typeName))
                throw new InvalidOperationException("未注册的基础 UI 服务：" + id);
            Type type = typeof(CDBoxUiDispatcher).Assembly.GetType(typeName, true);
            try
            {
                if (operation == ".ctor")
                    return Activator.CreateInstance(type,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, arguments, CultureInfo.InvariantCulture);
                return type.InvokeMember(operation,
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.NonPublic
                    | (instance == null ? BindingFlags.Static : BindingFlags.Instance),
                    null, instance, arguments, CultureInfo.InvariantCulture);
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
                throw;
            }
        }
    }
}

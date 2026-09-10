using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using CDBox.RealEstate.Settings;
using CDBox.Shared.UI;
using TCPipeAutoDraw.UI.Studio;

namespace CDBox.RealEstate.UI
{
    public static class BuildingLengthAnnotationSettingsPage
    {
        public const string PageId = "realestate-building-length-settings";
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static CDBoxPageDefinition Create(
            Func<BuildingAnnotationCadCatalog> catalogFactory,
            ICDBoxColorPickerService colorPicker)
        {
            if (catalogFactory == null)
                throw new ArgumentNullException("catalogFactory");
            if (colorPicker == null)
                throw new ArgumentNullException("colorPicker");

            return new CDBoxPageDefinition(PageId,
                "建筑物边长注记设置",
                delegate
                {
                    return BuildHtml(BuildingLengthAnnotationSettingsStore.Load(),
                        catalogFactory());
                },
                delegate(CDBoxPageRouteRequest request)
                {
                    return Route(request, colorPicker);
                })
            {
                Width = 940,
                Height = 720,
                MinimumWidth = 640,
                MinimumHeight = 600
            };
        }

        public static string BuildHtml(
            BuildingLengthAnnotationSettings settings,
            BuildingAnnotationCadCatalog catalog)
        {
            settings = settings ?? new BuildingLengthAnnotationSettings();
            settings.Normalize();
            catalog = catalog ?? new BuildingAnnotationCadCatalog();
            EnsureOption(catalog.TextStyles, settings.TextStyleName);
            EnsureOption(catalog.Linetypes, settings.AuxiliaryLinetypeName);
            if (catalog.TextStyles.Count == 0) catalog.TextStyles.Add("Standard");
            if (catalog.Linetypes.Count == 0) catalog.Linetypes.Add("Continuous");

            var context = new PageContext
            {
                Settings = settings,
                TextStyles = catalog.TextStyles.OrderBy(x => x,
                    StringComparer.CurrentCultureIgnoreCase).ToList(),
                Linetypes = catalog.Linetypes.OrderBy(x => x,
                    StringComparer.CurrentCultureIgnoreCase).ToList(),
                Lineweights = LineweightOptions()
            };

            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>建筑物边长注记设置</title><style>")
                .Append(UtilityWorkbench.Read("building-settings.css"))
                .Append("</style></head><body>")
                .Append(UtilityWorkbench.Read("building-settings.html"))
                .Append("<script>window.CDBoxBuildingSettingsContext=")
                .Append(Serializer.Serialize(context)).Append(";</script><script>")
                .Append(@"
(function(){
var ctx=window.CDBoxBuildingSettingsContext||{},draft=clone(ctx.Settings||{});
function clone(v){return JSON.parse(JSON.stringify(v||{}));}
function post(n,a){try{chrome.webview.postMessage('studio|'+n+'|'+encodeURIComponent(a||''));}catch(e){}}
function esc(v){return String(v==null?'':v).replace(/[&<>']/g,function(c){if(c==='&')return '&amp;';if(c==='<')return '&lt;';if(c==='>')return '&gt;';return '&#39;';});}
function option(select,items,value){select.innerHTML=(items||[]).map(function(x){var v=typeof x==='string'?x:x.Value,t=typeof x==='string'?x:x.Label;return `<option value='${esc(v)}'${String(v)===String(value)?' selected':''}>${esc(t)}</option>`;}).join('');}
function name(c){c=c||{};if(c.Type===0)return '随层';if(c.Type===1)return '随块';if(c.DisplayName)return c.DisplayName;if(c.Type===2)return 'ACI '+c.Index;if(c.Type===4)return (c.BookName?c.BookName+' · ':'')+(c.ColorName||'配色系统');return 'RGB '+c.R+', '+c.G+', '+c.B;}
function hex(c){c=c||{};return '#'+[c.R||0,c.G||0,c.B||0].map(function(x){return Math.max(0,Math.min(255,Number(x)||0)).toString(16).padStart(2,'0');}).join('').toUpperCase();}
function colorButton(id,c){var b=document.getElementById(id);b.innerHTML=`<i class='swatch' style='background:${hex(c)}'></i><span><strong>${esc(name(c))}</strong><small>${esc(hex(c)+' · RGB '+(c.R||0)+', '+(c.G||0)+', '+(c.B||0))}</small></span><span class='arrow'>选择 ›</span>`;}
function read(){draft.TextHeight=Number(document.getElementById('textHeight').value);draft.AdaptiveTextHeight=document.getElementById('adaptive').checked;draft.TextStyleName=document.getElementById('textStyle').value;draft.AuxiliaryLinetypeName=document.getElementById('linetype').value;draft.AuxiliaryLineweight=document.getElementById('lineweight').value;return draft;}
function render(){document.getElementById('textHeight').value=draft.TextHeight;document.getElementById('adaptive').checked=!!draft.AdaptiveTextHeight;option(document.getElementById('textStyle'),ctx.TextStyles,draft.TextStyleName);option(document.getElementById('linetype'),ctx.Linetypes,draft.AuxiliaryLinetypeName);option(document.getElementById('lineweight'),ctx.Lineweights,draft.AuxiliaryLineweight);colorButton('textColor',draft.TextColor);colorButton('auxColor',draft.AuxiliaryColor);document.getElementById('textColor').onclick=function(){post('pickTextColor',JSON.stringify(read()));};document.getElementById('auxColor').onclick=function(){post('pickAuxiliaryColor',JSON.stringify(read()));};document.getElementById('save').onclick=function(){var v=read();if(!isFinite(v.TextHeight)||v.TextHeight<.01){toast('文字高度必须大于或等于 0.01。','error');return;}pendingSnapshot=JSON.stringify(v);post('saveBuildingLengthSettings',pendingSnapshot);};}
window.CDBoxBuildingColorPicked=function(target,color){if(target==='text')draft.TextColor=clone(color);else draft.AuxiliaryColor=clone(color);colorButton(target==='text'?'textColor':'auxColor',color);updateState();};window.CDBoxStudioToast=toast;
function toast(m,k){var old=document.querySelector('.toast');if(old)old.remove();var x=document.createElement('div');x.className='toast '+(k||'');x.textContent=m||'';document.body.appendChild(x);setTimeout(function(){x.remove();},2800);}
var savedSnapshot='',pendingSnapshot='';
function updateState(){var dirty=JSON.stringify(read())!==savedSnapshot,node=document.getElementById('saveState');node.textContent=dirty?'未保存':'已保存';node.classList.toggle('dirty',dirty);}
window.CDBoxBuildingSettingsSaved=function(){savedSnapshot=pendingSnapshot;updateState();};
render();savedSnapshot=JSON.stringify(read());
document.querySelector('main').addEventListener('input',updateState);document.querySelector('main').addEventListener('change',updateState);
document.addEventListener('keydown',function(e){if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='s'){e.preventDefault();document.getElementById('save').click();}});
setTimeout(function(){post('ready','building-length-settings');},50);
})();
")
                .Append("</script></body></html>");
            return UtilityWorkbench.Apply(html.ToString());
        }

        private static CDBoxPageRouteResult Route(
            CDBoxPageRouteRequest request,
            ICDBoxColorPickerService colorPicker)
        {
            var result = new CDBoxPageRouteResult { Handled = true };
            if (request == null) return result;
            string name = (request.Name ?? string.Empty).Trim().ToLowerInvariant();
            if (name == "ready") return result;
            try
            {
                BuildingLengthAnnotationSettings draft =
                    Serializer.Deserialize<BuildingLengthAnnotationSettings>(
                        request.Argument ?? string.Empty)
                    ?? new BuildingLengthAnnotationSettings();
                draft.Normalize();
                if (name == "savebuildinglengthsettings")
                {
                    BuildingLengthAnnotationSettingsStore.Save(draft);
                    result.ExecuteScript = "window.CDBoxBuildingSettingsSaved && window.CDBoxBuildingSettingsSaved();";
                    result.ToastKind = "success";
                    result.ToastMessage = "建筑物边长注记设置已保存。";
                    return result;
                }
                bool text = name == "picktextcolor";
                if (text || name == "pickauxiliarycolor")
                {
                    CDBoxModuleColor selected;
                    CDBoxModuleColor initial = text
                        ? draft.TextColor : draft.AuxiliaryColor;
                    if (colorPicker.TryPick(initial, out selected, true, true))
                    {
                        string target = text ? "text" : "auxiliary";
                        result.ExecuteScript =
                            "window.CDBoxBuildingColorPicked && window.CDBoxBuildingColorPicked('"
                            + target + "'," + Serializer.Serialize(selected) + ");";
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "设置数据无效：" + ex.Message;
                return result;
            }
            result.Handled = false;
            return result;
        }

        private static void EnsureOption(IList<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value)) return;
            if (!values.Any(x => string.Equals(x, value,
                StringComparison.OrdinalIgnoreCase))) values.Add(value);
        }

        private static List<Option> LineweightOptions()
        {
            var result = new List<Option>
            {
                new Option("ByLayer", "随层"),
                new Option("ByBlock", "随块"),
                new Option("Default", "默认")
            };
            foreach (string value in new[] { "0.05", "0.09", "0.13", "0.15",
                "0.18", "0.20", "0.25", "0.30", "0.35", "0.40", "0.50",
                "0.60", "0.70", "0.80", "0.90", "1.00" })
                result.Add(new Option(value, value + " mm"));
            return result;
        }

        private sealed class PageContext
        {
            public BuildingLengthAnnotationSettings Settings { get; set; }
            public List<string> TextStyles { get; set; }
            public List<string> Linetypes { get; set; }
            public List<Option> Lineweights { get; set; }
        }

        private sealed class Option
        {
            public Option(string value, string label)
            {
                Value = value;
                Label = label;
            }
            public string Value { get; set; }
            public string Label { get; set; }
        }
    }
}

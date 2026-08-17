using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using CDBox.RealEstate.Settings;
using CDBox.Shared.UI;

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
                MinimumWidth = 760,
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
                .Append(@"
:root{--bg:#f4f7fc;--panel:#fff;--panel2:#f8faff;--text:#172033;--muted:#64748b;--line:#d9e3f1;--brand:#326fea;--brand2:#7c3aed;--ok:#087f5b}
*{box-sizing:border-box}html,body{margin:0;min-height:100%;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}button,input,select{font:inherit}
.page{width:min(940px,100%);margin:auto;padding:24px}.head{display:flex;justify-content:space-between;align-items:flex-start;gap:18px;margin-bottom:17px}.kicker{font-size:11px;font-weight:800;letter-spacing:.14em;color:var(--brand)}h1{margin:7px 0 6px;font-size:25px}.head p{margin:0;color:var(--muted);font-size:12px;line-height:1.7}.mode{padding:7px 11px;border:1px solid #cdddff;border-radius:999px;background:#edf4ff;color:#2458b8;font-size:11px;font-weight:800;white-space:nowrap}
.card{margin-bottom:14px;border:1px solid var(--line);border-radius:16px;background:var(--panel);box-shadow:0 12px 30px rgba(30,50,90,.06);overflow:hidden}.card-head{padding:14px 17px;border-bottom:1px solid var(--line);background:linear-gradient(90deg,var(--panel2),var(--panel))}.card-head strong{font-size:14px}.card-head small{display:block;margin-top:4px;color:var(--muted);line-height:1.6}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;padding:17px}.field{display:grid;gap:7px}.field>span{font-size:12px;font-weight:800}.field small{color:var(--muted);line-height:1.55}.control{height:39px;border:1px solid var(--line);border-radius:10px;background:#fff;color:var(--text);padding:0 11px;outline:0}.control:focus{border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.11)}
.switch-row{min-height:76px;display:flex;align-items:center;justify-content:space-between;gap:16px;padding:12px;border:1px solid var(--line);border-radius:12px;background:var(--panel2)}.switch-row strong,.switch-row small{display:block}.switch-row small{margin-top:4px;color:var(--muted);font-size:11px;line-height:1.55}.switch{position:relative;width:46px;height:25px;flex:0 0 auto}.switch input{opacity:0;width:0;height:0}.slider{position:absolute;inset:0;border-radius:999px;background:#b6c2d2;cursor:pointer;transition:.18s}.slider:before{content:'';position:absolute;width:19px;height:19px;left:3px;top:3px;border-radius:50%;background:#fff;box-shadow:0 2px 7px rgba(0,0,0,.22);transition:.18s}.switch input:checked+.slider{background:linear-gradient(135deg,var(--brand),var(--brand2))}.switch input:checked+.slider:before{transform:translateX(21px)}
.color{height:58px;width:100%;display:grid;grid-template-columns:32px 1fr auto;align-items:center;gap:10px;padding:8px 10px;border:1px solid var(--line);border-radius:11px;background:var(--panel2);color:var(--text);text-align:left;cursor:pointer}.color:hover{border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.08)}.swatch{width:30px;height:30px;border:1px solid rgba(15,23,42,.25);border-radius:8px}.color strong,.color small{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.color small{margin-top:3px;color:var(--muted)}.arrow{color:var(--brand);font-weight:900}
.foot{position:sticky;bottom:0;display:flex;justify-content:space-between;align-items:center;gap:12px;padding:12px 0 2px;background:linear-gradient(transparent,var(--bg) 28%)}.foot span{color:var(--muted);font-size:11px}.save{height:40px;padding:0 22px;border:0;border-radius:11px;color:#fff;font-weight:850;background:linear-gradient(135deg,var(--brand),var(--brand2));box-shadow:0 10px 22px rgba(50,111,234,.23);cursor:pointer}.toast{position:fixed;right:22px;bottom:20px;max-width:400px;padding:11px 14px;border-radius:10px;background:#172033;color:#fff;box-shadow:0 16px 36px rgba(15,23,42,.24)}.toast.success{background:var(--ok)}.toast.error{background:#b91c1c}
@media(max-width:720px){.page{padding:16px}.grid{grid-template-columns:1fr}.head{flex-direction:column}.mode{align-self:flex-start}}
")
                .Append("</style></head><body><main class=\"page\" data-page=\"realestate-building-length-settings\">")
                .Append("<header class=\"head\"><div><span class=\"kicker\">CDBox REALESTATE</span><h1>建筑物边长注记设置</h1><p>该页面独立于 CDBox 工作台，沿用统一界面与插件颜色选择器。</p></div><span class=\"mode\">不动产 · 建筑注记</span></header>")
                .Append("<section class=\"card\"><div class=\"card-head\"><strong>注记文字</strong><small>外边与面积计算辅助线的长度文字共用以下样式。</small></div><div class=\"grid\">")
                .Append("<label class=\"field\"><span>文字高度</span><input id=\"textHeight\" class=\"control\" type=\"number\" min=\"0.01\" step=\"0.05\"><small>关闭自适应时使用此高度；开启后作为最大高度。</small></label>")
                .Append("<div class=\"switch-row\"><div><strong>文字高度自适应</strong><small>按最短注记线段统一计算一次，同一建筑的全部文字高度始终相同。</small></div><label class=\"switch\"><input id=\"adaptive\" type=\"checkbox\"><span class=\"slider\"></span></label></div>")
                .Append("<label class=\"field\"><span>文字样式</span><select id=\"textStyle\" class=\"control\"></select><small>读取当前 CAD 图纸中可用的文字样式。</small></label>")
                .Append("<div class=\"field\"><span>文字颜色</span><button id=\"textColor\" class=\"color\" type=\"button\"></button><small>使用 CDBox 插件内颜色选择器。</small></div>")
                .Append("</div></section>")
                .Append("<section class=\"card\"><div class=\"card-head\"><strong>面积计算辅助线</strong><small>仅在建筑无法按正交矩形拆分时自动生成；外边不会被修改。</small></div><div class=\"grid\">")
                .Append("<label class=\"field\"><span>辅助线线型</span><select id=\"linetype\" class=\"control\"></select><small>读取当前 CAD 图纸已加载线型。</small></label>")
                .Append("<label class=\"field\"><span>辅助线线宽</span><select id=\"lineweight\" class=\"control\"></select><small>支持随层、随块、默认和常用毫米线宽。</small></label>")
                .Append("<div class=\"field\"><span>辅助线颜色</span><button id=\"auxColor\" class=\"color\" type=\"button\"></button><small>线长文字仍使用上方的文字颜色。</small></div>")
                .Append("</div></section><footer class=\"foot\"><span>设置保存在当前 Windows 用户的 CDBox 配置中。</span><button id=\"save\" class=\"save\" type=\"button\">保存设置</button></footer></main>")
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
function render(){document.getElementById('textHeight').value=draft.TextHeight;document.getElementById('adaptive').checked=!!draft.AdaptiveTextHeight;option(document.getElementById('textStyle'),ctx.TextStyles,draft.TextStyleName);option(document.getElementById('linetype'),ctx.Linetypes,draft.AuxiliaryLinetypeName);option(document.getElementById('lineweight'),ctx.Lineweights,draft.AuxiliaryLineweight);colorButton('textColor',draft.TextColor);colorButton('auxColor',draft.AuxiliaryColor);document.getElementById('textColor').onclick=function(){post('pickTextColor',JSON.stringify(read()));};document.getElementById('auxColor').onclick=function(){post('pickAuxiliaryColor',JSON.stringify(read()));};document.getElementById('save').onclick=function(){var v=read();if(!isFinite(v.TextHeight)||v.TextHeight<.01){toast('文字高度必须大于或等于 0.01。','error');return;}post('saveBuildingLengthSettings',JSON.stringify(v));};}
window.CDBoxBuildingColorPicked=function(target,color){if(target==='text')draft.TextColor=clone(color);else draft.AuxiliaryColor=clone(color);colorButton(target==='text'?'textColor':'auxColor',color);};window.CDBoxStudioToast=toast;
function toast(m,k){var old=document.querySelector('.toast');if(old)old.remove();var x=document.createElement('div');x.className='toast '+(k||'');x.textContent=m||'';document.body.appendChild(x);setTimeout(function(){x.remove();},2800);}
render();setTimeout(function(){post('ready','building-length-settings');},50);
})();
")
                .Append("</script></body></html>");
            return html.ToString();
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

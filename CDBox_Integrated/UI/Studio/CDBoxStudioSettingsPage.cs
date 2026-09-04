using System;
using System.Globalization;
using System.Text;
using TCPipeAutoDraw.Core.Colors;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioSettingsPage
    {
        public static string BuildStandaloneDocument()
        {
            CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
            CDBoxStudioComponentUpdatePlan componentPlan =
                CDBoxStudioComponentUpdatePlan.Capture();
            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>CDBox设置</title><style>");
            html.Append(@":root{--bg:#f5f7fb;--panel:#fff;--panel2:#f8fafc;--text:#162033;--muted:#64748b;--line:#dce5f1;--brand:#326fea;--brand2:#7c3aed;--ok:#059669;--warn:#d97706;--err:#dc2626}body[data-theme='fresh']{--bg:#f2f6ff;--panel2:#edf4ff;--line:#d7e5ff}body[data-theme='dark']{--bg:#0f172a;--panel:#172033;--panel2:#111827;--text:#e5e7eb;--muted:#94a3b8;--line:#2b3a50;--brand:#60a5fa;--brand2:#a78bfa}*{box-sizing:border-box}html,body{height:100%;margin:0;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}body{overflow:auto;padding:12px 22px 28px}.page{max-width:1180px;margin:auto}.head,.card{background:var(--panel);border:1px solid var(--line);border-radius:20px;box-shadow:0 12px 28px rgba(30,41,59,.07)}.head{padding:22px 24px;margin-bottom:14px}.head h1{margin:0;font-size:24px}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.card{padding:18px}.wide{grid-column:1/-1}.card h2{margin:0 0 14px;font-size:17px}.themes{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}.theme{border:1px solid var(--line);background:var(--panel2);border-radius:14px;padding:13px;cursor:pointer}.theme:has(input:checked){border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.1)}.switch{display:flex;align-items:center;gap:10px}.switch input{width:18px;height:18px;accent-color:var(--brand)}.hud-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.hud-field{display:grid;gap:9px;padding:13px;border:1px solid var(--line);border-radius:14px;background:var(--panel2)}.hud-field span{display:flex;justify-content:space-between;gap:12px;font-size:13px;font-weight:700}.hud-field output{color:var(--brand)}.hud-field input[type=range]{width:100%;accent-color:var(--brand)}.hud-note{margin:12px 0 0;color:var(--muted);font-size:12px}.paths{display:grid;grid-template-columns:1fr 1fr;gap:10px}.paths label{font-size:12px;color:var(--muted)}code{display:block;margin-top:5px;padding:10px;border:1px solid var(--line);border-radius:10px;background:var(--panel2);color:var(--text);word-break:break-all}.component-summary{display:grid;gap:6px;margin-top:10px;padding:10px;border:1px solid var(--line);border-radius:10px;background:var(--panel2)}.component-summary span{font-size:12px;color:var(--muted)}.actions{display:flex;gap:9px;flex-wrap:wrap;margin-top:14px}button,input,select{font:inherit}button{border:1px solid var(--line);background:var(--panel);color:var(--text);border-radius:11px;padding:9px 13px;font-weight:700;cursor:pointer}.primary{border:0;color:#fff;background:linear-gradient(135deg,var(--brand),var(--brand2))}.danger{color:#b91c1c;border-color:#fecaca}.update-row{display:grid;grid-template-columns:170px minmax(280px,1fr) auto;gap:10px;align-items:end}.field{display:grid;gap:6px;color:var(--muted);font-size:12px}.field input,.field select{height:40px;border:1px solid var(--line);border-radius:10px;background:var(--panel2);color:var(--text);padding:0 10px}.builtin-sources{display:grid;gap:4px;padding:9px 10px;border:1px solid var(--line);border-radius:10px;background:var(--panel2);color:var(--text)}.builtin-sources small{word-break:break-all}.progress{display:none;margin-top:12px;color:var(--muted)}.progress.show{display:block}.result{margin-top:12px;padding:12px;border:1px solid var(--line);border-radius:12px;background:var(--panel2);white-space:pre-wrap;word-break:break-word}.toast{position:fixed;right:22px;top:58px;max-width:420px;padding:12px 15px;border-radius:12px;background:var(--panel);border:1px solid var(--line);box-shadow:0 16px 38px rgba(15,23,42,.18);z-index:10}.no-animations *{animation:none!important;transition:none!important}@media(max-width:900px){.grid,.themes,.hud-grid,.paths,.update-row{grid-template-columns:1fr}} ");
            html.Append("</style></head><body data-theme=\"").Append(A(settings.Theme)).Append("\" class=\"").Append(settings.AnimationsEnabled ? string.Empty : "no-animations").Append("\"><main class=\"page\"><header class=\"head\"><h1>CDBox设置</h1></header><div class=\"grid\">");
            html.Append("<section class=\"card wide\"><h2>界面</h2><div class=\"themes\">");
            Theme(html, "light", "明亮浅色", settings.Theme); Theme(html, "fresh", "蓝紫现代", settings.Theme); Theme(html, "dark", "深色护眼", settings.Theme);
            html.Append("</div><div class=\"actions\"><label class=\"switch\"><input id=\"animations\" type=\"checkbox\"").Append(settings.AnimationsEnabled ? " checked" : string.Empty).Append(">启用界面动画</label></div></section>");
            html.Append("<section class=\"card wide\"><h2>悬浮球</h2><div class=\"hud-grid\">");
            html.Append("<div class=\"hud-field\"><span>悬浮球</span><label class=\"switch\"><input id=\"floatingCenterEnabled\" type=\"checkbox\"").Append(settings.FloatingCenterEnabled ? " checked" : string.Empty).Append(">启用悬浮球</label></div>");
            html.Append("<div class=\"hud-field\"><span>启动显示</span><label class=\"switch\"><input id=\"floatingCenterShowOnStartup\" type=\"checkbox\"").Append(settings.FloatingCenterShowOnStartup ? " checked" : string.Empty).Append(">加载插件后显示</label></div>");
            html.Append("<div class=\"hud-field\"><span>屏幕边缘</span><label class=\"switch\"><input id=\"floatingCenterSnapToEdges\" type=\"checkbox\"").Append(settings.FloatingCenterSnapToEdges ? " checked" : string.Empty).Append(">拖动后自动吸附</label></div>");
            html.Append("<label class=\"hud-field\"><span>普通提示自动关闭</span><input id=\"floatingCenterAutoCloseSeconds\" type=\"number\" min=\"1\" max=\"30\" step=\"1\" value=\"").Append(settings.FloatingCenterAutoCloseSeconds.ToString(CultureInfo.InvariantCulture)).Append("\"></label>");
            html.Append("<div class=\"hud-field\"><span>图纸检查</span><label class=\"switch\"><input id=\"floatingCenterAutoCheckEnabled\" type=\"checkbox\"").Append(settings.FloatingCenterAutoCheckEnabled ? " checked" : string.Empty).Append(">启用自动检查</label></div>");
            html.Append("<div class=\"hud-field\"><span>安全自动同步</span><label class=\"switch\"><input id=\"floatingCenterSafeAutoSyncEnabled\" type=\"checkbox\"").Append(settings.FloatingCenterSafeAutoSyncEnabled ? " checked" : string.Empty).Append(">允许安全小范围同步</label></div>");
            html.Append("</div></section>");
            html.Append("<section class=\"card\"><h2>颜色输出</h2><label class=\"field\">CAD 颜色写入方式<select id=\"colorOutputMode\">");
            ColorOutputOption(html, CDBoxColorOutputMode.PreserveOriginalType, "保持原类型", settings.ColorOutputMode);
            ColorOutputOption(html, CDBoxColorOutputMode.PreferIndexColor, "优先索引颜色", settings.ColorOutputMode);
            ColorOutputOption(html, CDBoxColorOutputMode.PreferTrueColor, "优先真彩色", settings.ColorOutputMode);
            ColorOutputOption(html, CDBoxColorOutputMode.CDBoxStandard, "CDBox 标准颜色", settings.ColorOutputMode);
            html.Append("</select></label><p class=\"hud-note\">默认保持对象原有 ACI / 真彩色类型；无法保持的 Color Book 修改会写为真彩色。</p></section>");
            html.Append("<section class=\"card wide\"><h2>浮窗与双击</h2><div class=\"hud-grid\">");
            HudRange(html, "hudNormalOpacity", "hudNormalOpacityValue", "常态透明度", settings.AnnotationHudNormalOpacity, true);
            HudRange(html, "hudHoverOpacity", "hudHoverOpacityValue", "鼠标悬停透明度", settings.AnnotationHudHoverOpacity, true);
            html.Append("<div class=\"hud-field\"><span>边缘光圈</span><label class=\"switch\"><input id=\"hudGlowEnabled\" type=\"checkbox\"").Append(settings.AnnotationHudGlowEnabled ? " checked" : string.Empty).Append(">启用轻微蓝色光圈</label></div>");
            HudRange(html, "hudGlowIntensity", "hudGlowIntensityValue", "光圈强度", settings.AnnotationHudGlowIntensity, false);
            html.Append("<div class=\"hud-field\"><span>双击快捷打开</span><label class=\"switch\"><input id=\"doubleClickOpen\" type=\"checkbox\"").Append(settings.DoubleClickOpenEnabled ? " checked" : string.Empty).Append(">双击打开标注浮窗和属性编辑器</label></div>");
            html.Append("</div><p class=\"hud-note\">外观设置统一应用于标注、对象选择、通知和操作提示浮窗。透明度 0% 对应安全最低透明度，100% 为完全不透明；保存后立即应用。</p></section>");
            html.Append("<section class=\"card wide\"><h2>安装与更新</h2><div class=\"component-summary\"><span>当前组件组合</span><b>").Append(H(componentPlan.Summary)).Append("</b></div>");
            if (!string.IsNullOrWhiteSpace(componentPlan.Warning))
                html.Append("<p class=\"hud-note\">").Append(H(componentPlan.Warning)).Append("</p>");
            html.Append("<div class=\"actions\"><button class=\"primary\" data-route=\"openInstaller\">打开安装器</button><button data-route=\"openLogs\">打开日志目录</button></div><p class=\"hud-note\">安装器可增加或移除业务模块，也可完整卸载 CDBox。</p></section>");
            html.Append("<section class=\"card wide\"><h2>网络更新</h2><p class=\"hud-note\">只下载并启动官方安装器。安装器会预选当前业务模块；应用更新时先完整卸载旧版本，再按所选模块安装新版本。模块增减和插件卸载也统一在安装器内完成。</p><div class=\"actions\"><button class=\"primary\" id=\"check\">检查更新</button><button id=\"download\" disabled>下载安装器</button></div><div id=\"progress\" class=\"progress\"></div><div id=\"result\" class=\"result\">尚未检查更新\n更新组件：").Append(H(componentPlan.Summary)).Append("</div></section>");
            html.Append("</div></main><script>");
            html.Append(@"(function(){var latest=null;function post(n,a){if(window.chrome&&chrome.webview)chrome.webview.postMessage('studio|'+n+'|'+encodeURIComponent(a||''));}function fraction(id){return (Number(document.getElementById(id).value||0)/100).toFixed(2);}function opacity(id){return (0.2+Number(document.getElementById(id).value||0)*0.008).toFixed(2);}function on(id){return document.getElementById(id).checked?'1':'0';}function payload(){var t=(document.querySelector('input[name=theme]:checked')||{}).value||'light';return 'theme='+encodeURIComponent(t)+'&animations='+on('animations')+'&colorOutputMode='+encodeURIComponent(document.getElementById('colorOutputMode').value)+'&hudNormalOpacity='+opacity('hudNormalOpacity')+'&hudHoverOpacity='+opacity('hudHoverOpacity')+'&hudGlowEnabled='+on('hudGlowEnabled')+'&hudGlowIntensity='+fraction('hudGlowIntensity')+'&doubleClickOpen='+on('doubleClickOpen')+'&floatingCenterEnabled='+on('floatingCenterEnabled')+'&floatingCenterShowOnStartup='+on('floatingCenterShowOnStartup')+'&floatingCenterSnapToEdges='+on('floatingCenterSnapToEdges')+'&floatingCenterAutoCloseSeconds='+encodeURIComponent(document.getElementById('floatingCenterAutoCloseSeconds').value||'5')+'&floatingCenterAutoCheckEnabled='+on('floatingCenterAutoCheckEnabled')+'&floatingCenterSafeAutoSyncEnabled='+on('floatingCenterSafeAutoSyncEnabled');}function save(){var t=(document.querySelector('input[name=theme]:checked')||{}).value||'light';document.body.dataset.theme=t;document.body.classList.toggle('no-animations',!document.getElementById('animations').checked);post('settings',payload());}function componentText(d){return d&&d.componentPlanText?d.componentPlanText:'未读取到当前组件组合';}function componentWarning(d){return d&&d.componentStateWarning?'\n组件状态提示：'+d.componentStateWarning:'';}document.querySelectorAll('input[name=theme],#animations,#hudGlowEnabled,#doubleClickOpen,#colorOutputMode,#floatingCenterEnabled,#floatingCenterShowOnStartup,#floatingCenterSnapToEdges,#floatingCenterAutoCloseSeconds,#floatingCenterAutoCheckEnabled,#floatingCenterSafeAutoSyncEnabled').forEach(function(x){x.addEventListener('change',save);});[['hudNormalOpacity','hudNormalOpacityValue'],['hudHoverOpacity','hudHoverOpacityValue'],['hudGlowIntensity','hudGlowIntensityValue']].forEach(function(pair){var input=document.getElementById(pair[0]),output=document.getElementById(pair[1]);input.addEventListener('input',function(){output.textContent=input.value+'%';});input.addEventListener('change',save);});document.querySelectorAll('[data-route]').forEach(function(x){x.addEventListener('click',function(){post(x.dataset.route,'');});});document.getElementById('check').onclick=function(){latest=null;document.getElementById('download').disabled=true;document.getElementById('progress').className='progress show';document.getElementById('progress').textContent='正在检查更新';post('checkUpdate',payload());};document.getElementById('download').onclick=function(){post('downloadUpdate',payload());};window.CDBoxStudioUpdateProgress=function(d){document.getElementById('progress').className='progress show';document.getElementById('progress').textContent=(d.message||'正在处理')+(d.percent?' · '+d.percent+'%':'');};window.CDBoxStudioUpdateResult=function(d){d=d||{};latest=d;document.getElementById('progress').className='progress';var text=d.success?(d.updateAvailable?'发现新版本：'+d.latestVersion+' · '+(d.installerFileName||'安装器'):'当前已是最新版本'):('检查失败：'+(d.errorMessage||'请稍后重试或查看日志'));document.getElementById('result').textContent=text+'\n更新组件：'+componentText(d)+componentWarning(d);document.getElementById('download').disabled=!(d.success&&d.updateAvailable);};window.CDBoxStudioDownloadResult=function(d){d=d||{};document.getElementById('progress').className='progress';var ready=d.success&&d.verified;var text=ready?(d.installerStarted?'安装器已启动，请关闭 CAD 后在安装器中完成更新':'安装器已校验，但未能启动：'+(d.installerErrorMessage||'请查看日志')):('更新准备失败：'+(d.errorMessage||'请稍后重试或查看日志'));document.getElementById('result').textContent=text+'\n将预选组件：'+componentText(d)+componentWarning(d);};window.CDBoxStudioToast=function(m,k){var x=document.createElement('div');x.className='toast';x.textContent=m||'';document.body.appendChild(x);setTimeout(function(){x.remove();},2800);};setTimeout(function(){post('ready','settings');},50);})();");
            html.Append("</script></body></html>");
            return html.ToString();
        }

        private static void Theme(StringBuilder html, string value, string title, string current)
        {
            html.Append("<label class=\"theme\"><input type=\"radio\" name=\"theme\" value=\"").Append(value).Append("\"");
            if (string.Equals(value, current, System.StringComparison.OrdinalIgnoreCase)) html.Append(" checked");
            html.Append(">").Append(title).Append("</label>");
        }

        private static void ColorOutputOption(StringBuilder html, CDBoxColorOutputMode value, string title,
            CDBoxColorOutputMode current)
        {
            html.Append("<option value=\"").Append(value).Append("\"");
            if (value == current) html.Append(" selected");
            html.Append(">").Append(title).Append("</option>");
        }

        private static void HudRange(StringBuilder html, string id, string outputId,
            string title, double value, bool opacityScale)
        {
            double normalized = opacityScale ? (value - 0.20) / 0.80 : value;
            string percent = Math.Round(Math.Max(0.0, Math.Min(1.0, normalized)) * 100.0)
                .ToString(CultureInfo.InvariantCulture);
            html.Append("<label class=\"hud-field\"><span>").Append(title).Append("<output id=\"")
                .Append(outputId).Append("\">").Append(percent).Append("%</output></span><input id=\"")
                .Append(id).Append("\" type=\"range\" min=\"0")
                .Append("\" max=\"100\" step=\"1\" value=\"").Append(percent).Append("\"></label>");
        }

        private static string H(string value) { return A(value); }
        private static string A(string value) { return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;"); }
    }
}

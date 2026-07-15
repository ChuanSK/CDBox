using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioHtml
    {
        private static readonly string[] CategoryOrder =
        {
            "总览",
            "收藏",
            "最近使用",
            "常用",
            "图层管理器",
            "标注设置",
            "标注",
            "断面",
            "管线属性",
            "属性默认表",
            "属性识别表",
            "工程量",
            "图框工具",
            "系统",
            "设置",
            "其他"
        };

        public static string Build(IEnumerable<CDBoxStudioAction> actions, CDBoxStudioState state, CDBoxStudioSettings settings, string runtimeVersion, string logFilePath, string settingsFilePath)
        {
            List<CDBoxStudioAction> list = actions == null
                ? new List<CDBoxStudioAction>()
                : actions.Where(a => a != null).ToList();

            state = state ?? new CDBoxStudioState();
            settings = settings ?? new CDBoxStudioSettings();
            settings.Normalize();

            string html = Template();
            html = html.Replace("{{THEME}}", HtmlAttr(settings.Theme));
            html = html.Replace("{{ANIMATION_CLASS}}", settings.AnimationsEnabled ? string.Empty : " no-animations");
            html = html.Replace("{{SIDEBAR_CLASS}}", settings.SidebarCollapsedDefault ? " sidebar-collapsed" : string.Empty);
            html = html.Replace("{{NAV}}", BuildNavigation(list, state));
            html = html.Replace("{{CARDS}}", BuildCards(list, state));
            html = html.Replace("{{COMMAND_ROWS}}", BuildCommandRows(list));
            html = html.Replace("{{SETTINGS_PAGE}}", BuildSettingsPage(list, state, settings, logFilePath, settingsFilePath));
            html = html.Replace("{{ANNOTATION_SETTINGS_PAGE}}", BuildAnnotationSettingsPage());
            html = html.Replace("{{LAYER_MANAGER_PAGE}}", BuildLayerManagerPage());
            html = html.Replace("{{QUANTITY_DASHBOARD_PAGE}}", BuildQuantityDashboardPage());
            html = html.Replace("{{QUANTITY_ATTRIBUTE_EDITOR_PAGE}}", CDBoxStudioQuantityAttributeEditorPage.BuildEmbeddedSection());
            html = html.Replace("{{SECTION_DRAWING_PAGE}}", CDBoxStudioSectionDrawingPage.BuildEmbeddedSection());
            html = html.Replace("{{RECOGNITION_RULES_PAGE}}", BuildRecognitionRulesPage());
            html = html.Replace("{{DEFAULT_PROFILES_PAGE}}", BuildDefaultProfilesPage());
            html = html.Replace("{{QUANTITY_DEFAULTS_STYLE}}", CDBoxStudioQuantityDefaultsPage.BuildStyles(false));
            html = html.Replace("{{RECOGNITION_RULES_STYLE}}", CDBoxStudioRecognitionRulesPage.BuildStyles(false));
            html = html.Replace("{{ANNOTATION_SETTINGS_STYLE}}", CDBoxStudioAnnotationSettingsPage.BuildStyles(false));
            html = html.Replace("{{LAYER_MANAGER_STYLE}}", CDBoxStudioLayerManagerPage.BuildStyles(false));
            html = html.Replace("{{QUANTITY_DASHBOARD_STYLE}}", CDBoxStudioQuantityDashboardPage.BuildStyles(false));
            html = html.Replace("{{QUANTITY_ATTRIBUTE_EDITOR_STYLE}}", CDBoxStudioQuantityAttributeEditorPage.BuildStyles(false));
            html = html.Replace("{{SECTION_DRAWING_STYLE}}", CDBoxStudioSectionDrawingPage.BuildStyles(false));
            html = html.Replace("{{QUANTITY_DEFAULTS_SCRIPT}}", CDBoxStudioQuantityDefaultsPage.BuildEmbeddedBridgeScript());
            html = html.Replace("{{ANNOTATION_SETTINGS_SCRIPT}}", CDBoxStudioAnnotationSettingsPage.BuildComponentScript());
            html = html.Replace("{{LAYER_MANAGER_SCRIPT}}", CDBoxStudioLayerManagerPage.BuildComponentScript());
            html = html.Replace("{{QUANTITY_DASHBOARD_SCRIPT}}", CDBoxStudioQuantityDashboardPage.BuildComponentScript());
            html = html.Replace("{{QUANTITY_ATTRIBUTE_EDITOR_SCRIPT}}", CDBoxStudioQuantityAttributeEditorPage.BuildComponentScript());
            html = html.Replace("{{SECTION_DRAWING_SCRIPT}}", CDBoxStudioSectionDrawingPage.BuildComponentScript());
            html = html.Replace("{{RECOGNITION_RULES_SCRIPT}}", CDBoxStudioRecognitionRulesPage.BuildEmbeddedBridgeScript());
            html = html.Replace("{{DEFAULT_PROFILES_JSON}}", CDBoxStudioDefaultProfiles.BuildDefaultsJson(CDBoxStudioDefaultProfiles.LoadDefaults()));
            html = html.Replace("{{BUILTIN_DEFAULT_PROFILES_JSON}}", CDBoxStudioDefaultProfiles.BuildDefaultsJson(CDBoxStudioDefaultProfiles.LoadBuiltInDefaults()));
            html = html.Replace("{{DEFAULT_PROFILES_PATH}}", Html(CDBoxStudioDefaultProfiles.DefaultsFilePath));
            html = html.Replace("{{RECOGNITION_RULES_JSON}}", CDBoxStudioRecognitionRules.BuildRulesJson(CDBoxStudioRecognitionRules.LoadRules()));
            html = html.Replace("{{DEFAULT_RECOGNITION_RULES_JSON}}", CDBoxStudioRecognitionRules.BuildRulesJson(CDBoxStudioRecognitionRules.LoadDefaultRules()));
            html = html.Replace("{{RECOGNITION_RULES_PATH}}", Html(CDBoxStudioRecognitionRules.RulesFilePath));
            html = html.Replace("{{TOTAL}}", list.Count.ToString());
            html = html.Replace("{{ENABLED}}", list.Count(a => a.Enabled).ToString());
            html = html.Replace("{{FAVORITES}}", state.FavoriteIds.Count.ToString());
            html = html.Replace("{{RECENT}}", state.RecentItems.Count.ToString());
            html = html.Replace("{{RUNTIME}}", Html(string.IsNullOrWhiteSpace(runtimeVersion) ? "WebView2" : runtimeVersion));
            html = html.Replace("{{LOG_PATH}}", Html(logFilePath ?? string.Empty));
            html = html.Replace("{{SETTINGS_PATH}}", Html(settingsFilePath ?? string.Empty));
            html = html.Replace("{{CURRENT_VERSION}}", Html(CDBoxStudioUpdateService.CurrentVersion));
            html = html.Replace("{{UPDATE_CHANNEL}}", HtmlAttr(settings.UpdateChannel));
            html = html.Replace("{{UPDATE_SOURCE_URL}}", HtmlAttr(settings.UpdateSourceUrl));
            html = html.Replace("{{DEFAULT_UPDATE_SOURCE_URL}}", Html(CDBoxStudioUpdateService.DefaultUpdateSourceUrl));
            html = html.Replace(".card-actions{position:absolute;", ".card-actions{display:flex;gap:2px;position:absolute;");
            html = html.Replace(".card:hover{", ".card.dragging{opacity:.45;transform:scale(.98)}.card.drag-target{border-color:var(--brand);box-shadow:0 0 0 3px rgba(59,130,246,.12)}.card:hover{");
            return html;
        }

        private static string BuildNavigation(List<CDBoxStudioAction> actions, CDBoxStudioState state)
        {
            List<string> categories = BuildCategories(actions, state);
            var nav = new StringBuilder();
            foreach (string category in categories)
            {
                string css = category == "总览" ? "nav-item active" : "nav-item";
                int count = GetCategoryCount(category, actions, state);
                nav.Append("<button class=\"").Append(css).Append("\" data-filter=\"").Append(HtmlAttr(category)).Append("\">")
                   .Append("<span>").Append(Html(category)).Append("</span><em>").Append(count).Append("</em></button>");
            }
            return nav.ToString();
        }

        private static string BuildCards(List<CDBoxStudioAction> actions, CDBoxStudioState state)
        {
            var cards = new StringBuilder();
            int index = 0;
            foreach (CDBoxStudioAction action in SortActions(actions, state))
            {
                bool favorite = state.IsFavorite(action.Id);
                CDBoxStudioRecentItem recent = state.GetRecent(action.Id);
                int recentRank = state.GetRecentRank(action.Id);
                string disabledClass = action.Enabled ? string.Empty : " disabled";
                string favoriteClass = favorite ? " favorited" : string.Empty;
                string favoriteTitle = favorite ? "取消收藏" : "收藏";
                string commandLine = string.IsNullOrWhiteSpace(action.CommandName) ? string.Empty : "<span class=\"command\">" + Html(action.CommandName) + "</span>";
                string badge = string.IsNullOrWhiteSpace(action.BadgeText) ? string.Empty : "<span class=\"badge\">" + Html(action.BadgeText) + "</span>";
                string recentBadge = recent == null ? string.Empty : "<span class=\"recent-badge\">最近 " + recent.UseCount + " 次</span>";

                cards.Append("<article class=\"card").Append(disabledClass).Append(favoriteClass).Append("\" data-card data-category=\"").Append(HtmlAttr(action.Category)).Append("\" data-id=\"").Append(HtmlAttr(action.Id)).Append("\"")
                    .Append(" data-title=\"").Append(HtmlAttr(action.Title)).Append("\"")
                    .Append(" data-command=\"").Append(HtmlAttr(action.CommandName)).Append("\"")
                    .Append(" data-favorite=\"").Append(favorite ? "1" : "0").Append("\"")
                    .Append(" data-recent=\"").Append(recent == null ? "0" : "1").Append("\"")
                    .Append(" data-recent-rank=\"").Append(recentRank).Append("\"")
                    .Append(" style=\"--i:").Append(index).Append("\">");

                cards.Append("<div class=\"card-actions\"><button class=\"star\" data-card-drag title=\"拖拽调整卡片顺序\" aria-label=\"拖拽调整卡片顺序\">↕</button><button class=\"star\" data-favorite-button title=\"").Append(favoriteTitle).Append("\" aria-label=\"").Append(favoriteTitle).Append("\">")
                    .Append("<svg viewBox=\"0 0 24 24\"><path d=\"M12 3.6l2.6 5.3 5.8.8-4.2 4.1 1 5.8-5.2-2.8-5.2 2.8 1-5.8-4.2-4.1 5.8-.8L12 3.6z\"/></svg>")
                    .Append("</button></div>");

                cards.Append("<button class=\"card-run\" data-run-button");
                if (!action.Enabled) cards.Append(" disabled");
                cards.Append(">");
                cards.Append("<div class=\"card-top\"><div class=\"icon\">").Append(BuildIcon(action.Category)).Append("</div><div class=\"card-meta\">")
                     .Append("<h3>").Append(Html(action.Title)).Append("</h3>")
                     .Append("</div></div>")
                     .Append("<div class=\"card-foot\"><span class=\"category\">").Append(Html(action.Category)).Append("</span>")
                     .Append(commandLine).Append(badge).Append(recentBadge).Append("</div></button></article>");
                index++;
            }
            return cards.ToString();
        }

        private static string BuildCommandRows(List<CDBoxStudioAction> actions)
        {
            var rows = new StringBuilder();
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__layerManager\" data-search=\"图层管理器 layer manager 父属性 分类 标签 锁定 冻结\"><span><strong>打开图层管理器</strong></span><kbd>LAYER</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__layerManagerWindow\" data-search=\"图层管理器 独立窗口 layer manager standalone\"><span><strong>图层管理器 · 独立窗口</strong></span><kbd>NEW</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__quantityDashboard\" data-search=\"工程量 看板 当前工程量 预算 参考 quantity dashboard gcl\"><span><strong>打开工程量看板</strong></span><kbd>QTY</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__quantityDashboardWindow\" data-search=\"工程量 看板 独立窗口 quantity dashboard standalone cdqboard\"><span><strong>工程量看板 · 独立窗口</strong></span><kbd>NEW</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__quantityAttributeEditor\" data-search=\"属性编辑器 主管 支管 检查井 quantity attribute sx preview 9\"><span><strong>属性编辑器 · Preview 9</strong></span><kbd>SX</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__sectionDrawing\" data-search=\"断面图 断面生成 section drawing dm preview 10\"><span><strong>断面图生成 · Preview 10</strong></span><kbd>DM</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__sectionDrawingWindow\" data-search=\"断面图 独立窗口 section drawing standalone\"><span><strong>断面图生成 · 独立窗口</strong></span><kbd>NEW</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__quantityFormalReport\" data-search=\"正式工程量表 GCL 工程量计算表 export report\"><span><strong>生成正式工程量表</strong></span><kbd>GCL</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__settings\" data-search=\"studio 设置 外观 theme animation sidebar favorite recent update 检查更新\"><span><strong>Studio 设置 / 外观设置</strong></span><kbd>SET</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__annotationSettings\" data-search=\"标注设置 annotation settings 表面积 管线长度 节点\"><span><strong>打开标注设置</strong></span><kbd>ANN</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__annotationSurface\" data-search=\"表面积标注设置 surface annotation settings\"><span><strong>打开表面积标注设置</strong></span><kbd>SURF</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__annotationPipeLength\" data-search=\"管线长度标注设置 pipe length annotation settings\"><span><strong>打开管线长度标注设置</strong></span><kbd>PIPE</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__annotationNode\" data-search=\"节点标注设置 node annotation settings\"><span><strong>打开节点标注设置</strong></span><kbd>NODE</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__checkUpdate\" data-search=\"检查更新 update update.json 国内源 版本 channel sha256 updater\"><span><strong>检查更新</strong></span><kbd>UPD</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__defaultProfiles\" data-search=\"属性默认表 主管 支管 节点 检查井 默认 sxmrb profiles defaults\"><span><strong>属性默认表</strong></span><kbd>DEF</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__defaultProfilesWindow\" data-search=\"属性默认表 独立窗口 WebView2 sxmrb defaults window\"><span><strong>属性默认表 · 独立窗口</strong></span><kbd>NEW</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__recognitionRules\" data-search=\"属性识别表 图层 规则 父属性 分类 标签 match rules layer\"><span><strong>属性识别表</strong></span><kbd>RULE</kbd></button>");
            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__recognitionRulesWindow\" data-search=\"属性识别表 独立窗口 WebView2 rules window\"><span><strong>属性识别表 · 独立窗口</strong></span><kbd>NEW</kbd></button>");

            foreach (CDBoxStudioAction action in SortActions(actions, new CDBoxStudioState()))
            {
                if (Same(action.Id, "module:annotation-settings") || Same(action.Id, "module:layer-manager") || Same(action.Id, "module:quantity-calculation")) continue;
                string disabled = action.Enabled ? string.Empty : " disabled";
                rows.Append("<button class=\"cmd-row").Append(disabled).Append("\" data-command-row data-id=\"").Append(HtmlAttr(action.Id)).Append("\"")
                    .Append(" data-search=\"").Append(HtmlAttr((action.Title + " " + action.Category + " " + action.Description + " " + action.CommandName).ToLowerInvariant())).Append("\"");
                if (!action.Enabled) rows.Append(" disabled");
                rows.Append("><span><strong>").Append(Html(action.Title)).Append("</strong></span>");
                if (!string.IsNullOrWhiteSpace(action.CommandName)) rows.Append("<kbd>").Append(Html(action.CommandName)).Append("</kbd>");
                rows.Append("</button>");
            }

            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__openLogs\" data-search=\"studio 日志 log openlogs\"><span><strong>打开 Studio 日志目录</strong></span><kbd>LOG</kbd></button>");
            return rows.ToString();
        }

        private static string BuildSettingsPage(List<CDBoxStudioAction> actions, CDBoxStudioState state, CDBoxStudioSettings settings, string logFilePath, string settingsFilePath)
        {
            var page = new StringBuilder();
            page.Append("<section id=\"settingsPage\" class=\"settings-page\" style=\"display:none\">");
            page.Append("<div class=\"settings-head\"><div><span class=\"kicker\">Studio Preview 9</span><h2>Studio 设置 / 外观设置</h2></div><button class=\"ghost-btn\" id=\"backToHome\">返回总览</button></div>");

            page.Append("<div class=\"settings-grid\">");
            page.Append("<article class=\"setting-card wide\"><h3>主题选择</h3><div class=\"theme-options\">");
            AppendThemeOption(page, "light", "明亮浅色", "稳定默认，适合长期使用", settings.Theme);
            AppendThemeOption(page, "fresh", "蓝紫现代", "更接近后台工作台 Preview 风格", settings.Theme);
            AppendThemeOption(page, "dark", "深色护眼", "夜间或低亮度环境使用", settings.Theme);
            page.Append("</div></article>");

            page.Append("<article class=\"setting-card\"><h3>动画</h3><label class=\"switch\"><input id=\"animationsToggle\" type=\"checkbox\"");
            if (settings.AnimationsEnabled) page.Append(" checked");
            page.Append("/><span></span><em>启用页面 / 卡片动画</em></label></article>");

            page.Append("<article class=\"setting-card\"><h3>侧栏默认值</h3><label class=\"switch\"><input id=\"sidebarToggle\" type=\"checkbox\"");
            if (settings.SidebarCollapsedDefault) page.Append(" checked");
            page.Append("/><span></span><em>默认折叠左侧导航</em></label></article>");

            page.Append("<article class=\"setting-card\"><h3>最近使用记录</h3><strong>当前 ").Append(state.RecentItems.Count).Append(" 项</strong><button id=\"clearRecentButton\" class=\"danger-btn\">清空最近使用</button></article>");
            page.Append("<article class=\"setting-card\"><h3>配置文件</h3><code>").Append(Html(settingsFilePath ?? string.Empty)).Append("</code></article>");

            page.Append("<article class=\"setting-card wide update-card\"><div class=\"settings-subhead\"><div><h3>插件内联网更新</h3></div><strong>当前 {{CURRENT_VERSION}}</strong></div>");
            page.Append("<div class=\"update-controls full\"><label><span>更新通道</span><select id=\"updateChannel\"><option value=\"studio-preview\"");
            if (string.Equals(settings.UpdateChannel, "studio-preview", StringComparison.OrdinalIgnoreCase)) page.Append(" selected");
            page.Append(">studio-preview</option></select></label>");
            page.Append("<label class=\"source\"><span>国内 update.json 更新源</span><input id=\"updateSourceUrl\" type=\"text\" value=\"").Append(HtmlAttr(settings.UpdateSourceUrl)).Append("\" placeholder=\"").Append(HtmlAttr(CDBoxStudioUpdateService.DefaultUpdateSourceUrl)).Append("\" /></label><div class=\"update-actions\"><button id=\"checkUpdateButton\" class=\"primary-btn\">检查更新</button><button id=\"downloadUpdateButton\" class=\"ghost-btn\" disabled>下载、校验并准备安装</button></div></div>");
            page.Append("<div id=\"updateProgress\" class=\"update-progress idle\"><div><span></span></div><em>等待操作</em></div>");
            page.Append("<div id=\"updateResult\" class=\"update-result idle\"><strong>尚未检查更新</strong></div></article>");
            page.Append("</div>");

            page.Append("<article class=\"setting-card favorites-manager\"><div class=\"settings-subhead\"><div><h3>收藏管理</h3></div><strong>").Append(state.FavoriteIds.Count).Append(" 项</strong></div>");
            List<CDBoxStudioAction> favorites = SortActions(actions.Where(a => state.IsFavorite(a.Id)).ToList(), state).ToList();
            if (favorites.Count == 0)
            {
                page.Append("<div class=\"empty-line\">暂无收藏功能。</div>");
            }
            else
            {
                page.Append("<div class=\"favorite-list\">");
                foreach (CDBoxStudioAction action in favorites)
                {
                    page.Append("<div class=\"favorite-row\"><div><strong>").Append(Html(action.Title)).Append("</strong><em>").Append(Html(action.Category)).Append(" · ").Append(Html(action.CommandName)).Append("</em></div><button data-remove-favorite=\"").Append(HtmlAttr(action.Id)).Append("\">移除</button></div>");
                }
                page.Append("</div>");
            }
            page.Append("</article>");

            page.Append("</section>");
            return page.ToString();
        }


        private static string BuildLayerManagerPage()
        {
            return CDBoxStudioLayerManagerPage.BuildEmbeddedSection();
        }

        private static string BuildAnnotationSettingsPage()
        {
            return CDBoxStudioAnnotationSettingsPage.BuildEmbeddedSection();
        }

        private static string BuildQuantityDashboardPage()
        {
            return CDBoxStudioQuantityDashboardPage.BuildEmbeddedSection();
        }

        private static string BuildDefaultProfilesPage()
        {
            return CDBoxStudioQuantityDefaultsPage.BuildEmbeddedSection();
        }

        private static string BuildRecognitionRulesPage()
        {
            return CDBoxStudioRecognitionRulesPage.BuildEmbeddedSection();
        }

        private static void AppendThemeOption(StringBuilder page, string value, string title, string description, string current)
        {
            page.Append("<label class=\"theme-option theme-").Append(HtmlAttr(value)).Append("\"><input type=\"radio\" name=\"studioTheme\" value=\"").Append(HtmlAttr(value)).Append("\"");
            if (string.Equals(value, current, StringComparison.OrdinalIgnoreCase)) page.Append(" checked");
            page.Append("/><span><strong>").Append(Html(title)).Append("</strong></span></label>");
        }

        private static string Template()
        {
            return @"<!doctype html>
<html lang=""zh-CN"">
<head>
<meta charset=""utf-8"">
<meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>CDBox Studio</title>
<style>
:root{--bg:#f5f7fb;--panel:#fff;--panel2:#f8fafc;--muted:#6b7280;--text:#162033;--line:#e7ebf2;--brand:#3b82f6;--brand2:#7c3aed;--ok:#10b981;--warn:#f59e0b;--err:#ef4444;--shadow:0 20px 45px rgba(30,41,59,.08);--shadow2:0 10px 24px rgba(30,41,59,.08);--chip:#f1f5f9;--chipText:#475569}
body[data-theme='fresh']{--bg:#f3f7ff;--panel:#ffffff;--panel2:#eef4ff;--muted:#65758f;--text:#172033;--line:#dbe7ff;--brand:#2563eb;--brand2:#9333ea;--chip:#eef2ff;--chipText:#3730a3}
body[data-theme='dark']{--bg:#0f172a;--panel:#172033;--panel2:#111827;--muted:#94a3b8;--text:#e5e7eb;--line:#263244;--brand:#60a5fa;--brand2:#a78bfa;--chip:#1e293b;--chipText:#cbd5e1;--shadow:0 20px 45px rgba(0,0,0,.24);--shadow2:0 10px 24px rgba(0,0,0,.24)}
*{box-sizing:border-box}html,body{height:100%;margin:0;overflow:hidden;background:var(--bg);font-family:'Microsoft YaHei UI','Segoe UI',system-ui,sans-serif;color:var(--text)}button,input,select,textarea{font:inherit}button{color:inherit}.shell{height:100%;display:grid;grid-template-columns:270px 1fr;background:radial-gradient(circle at 72% -12%,rgba(59,130,246,.16),transparent 30%),radial-gradient(circle at 12% 12%,rgba(124,58,237,.10),transparent 28%),var(--bg);transition:grid-template-columns .18s ease}.shell.sidebar-collapsed{grid-template-columns:82px 1fr}
.sidebar{position:relative;display:flex;flex-direction:column;min-height:0;padding:24px 18px;border-right:1px solid var(--line);background:color-mix(in srgb,var(--panel) 84%,transparent);backdrop-filter:blur(18px);animation:sideIn .35s ease both}.brand{flex:0 0 auto;height:76px;display:flex;align-items:center;gap:12px;margin-bottom:14px;padding:0 10px}.logo{width:46px;height:46px;border-radius:16px;background:linear-gradient(135deg,var(--brand),var(--brand2));display:grid;place-items:center;color:#fff;font-weight:800;box-shadow:0 12px 24px rgba(59,130,246,.26);flex:0 0 auto}.brand h1{font-size:19px;margin:0}.brand p{font-size:12px;color:var(--muted);margin:4px 0 0}.collapse-btn{position:absolute;right:14px;top:12px;border:1px solid var(--line);background:var(--panel);width:30px;height:30px;border-radius:10px;cursor:pointer}.nav-title{flex:0 0 auto;font-size:12px;color:var(--muted);margin:16px 10px 10px;letter-spacing:.12em;text-transform:uppercase}.nav{flex:1 1 auto;min-height:0;display:flex;flex-direction:column;gap:8px;overflow-y:auto;overscroll-behavior:contain;padding-right:2px;scrollbar-width:none;-ms-overflow-style:none}.nav::-webkit-scrollbar{width:0;height:0}.nav-item{border:0;background:transparent;border-radius:14px;padding:12px;color:var(--muted);font-size:14px;text-align:left;display:flex;justify-content:space-between;align-items:center;cursor:pointer;transition:.18s}.nav-item:hover{background:var(--panel2);color:var(--text);transform:translateX(2px)}.nav-item.active{background:linear-gradient(135deg,rgba(59,130,246,.12),rgba(124,58,237,.10));color:var(--brand);font-weight:700;box-shadow:inset 0 0 0 1px rgba(59,130,246,.10)}.nav-item em{font-style:normal;font-size:12px;background:var(--panel);border:1px solid var(--line);border-radius:999px;padding:2px 8px;color:var(--muted)}.side-tip{flex:0 0 auto;margin-top:14px;padding:14px;border-radius:18px;background:var(--panel2);border:1px solid var(--line);color:var(--muted);font-size:12px;line-height:1.65}.side-tip button{border:0;background:rgba(59,130,246,.12);color:var(--brand);border-radius:999px;padding:5px 10px;margin-top:8px;cursor:pointer}.shell.sidebar-collapsed .brand div:not(.logo),.shell.sidebar-collapsed .nav-title,.shell.sidebar-collapsed .nav-item em,.shell.sidebar-collapsed .side-tip{display:none}.shell.sidebar-collapsed .sidebar{padding-left:14px;padding-right:14px}.shell.sidebar-collapsed .nav-item{justify-content:center;padding:12px 6px}.shell.sidebar-collapsed .nav-item span{font-size:12px;writing-mode:vertical-rl;letter-spacing:.08em}.shell.sidebar-collapsed .brand{justify-content:center;padding:0}.shell.sidebar-collapsed .logo{width:44px;height:44px}
.main{min-width:0;overflow:auto;padding:28px 32px 40px}.hero{display:grid;grid-template-columns:1fr 380px;gap:22px;margin-bottom:24px;animation:fadeUp .35s ease both}.hero-card,.stats,.setting-card{position:relative;overflow:hidden;background:color-mix(in srgb,var(--panel) 92%,transparent);border:1px solid var(--line);border-radius:26px;box-shadow:var(--shadow)}.hero-card{padding:30px}.kicker{font-size:12px;color:var(--brand);font-weight:700;letter-spacing:.08em;text-transform:uppercase}.hero-card h2{font-size:30px;line-height:1.18;margin:12px 0}.hero-card p{margin:0;color:var(--muted);line-height:1.75}.stats{padding:20px;display:grid;grid-template-columns:1fr 1fr;gap:12px}.stat{background:var(--panel2);border:1px solid var(--line);border-radius:20px;padding:16px}.stat strong{display:block;font-size:26px}.stat span{font-size:12px;color:var(--muted)}.toolbar{display:flex;align-items:center;justify-content:space-between;gap:18px;margin-bottom:18px}.section-title h2{font-size:20px;margin:0 0 6px}.section-title p{font-size:13px;color:var(--muted);margin:0}.toolbar-actions{display:flex;gap:10px;align-items:center}.search{width:320px;border:1px solid var(--line);border-radius:16px;background:var(--panel);color:var(--text);padding:12px 14px;outline:0;box-shadow:var(--shadow2)}.search:focus{border-color:rgba(59,130,246,.45)}.ghost-btn,.danger-btn{border:1px solid var(--line);background:var(--panel);border-radius:14px;padding:11px 14px;cursor:pointer;box-shadow:var(--shadow2)}.ghost-btn:hover{background:var(--panel2)}.danger-btn{background:#fef2f2;border-color:#fecaca;color:#b91c1c}.danger-btn:hover{background:#fee2e2}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:16px}.card{position:relative;background:var(--panel);border:1px solid var(--line);border-radius:24px;padding:16px;box-shadow:var(--shadow2);min-height:190px;display:flex;animation:cardIn .28s ease both;animation-delay:calc(var(--i)*18ms);transition:transform .18s ease,box-shadow .18s ease}.card:hover{transform:translateY(-4px);box-shadow:var(--shadow)}.card.disabled{opacity:.56}.card-actions{position:absolute;right:14px;top:12px;z-index:2}.star{border:0;background:transparent;width:30px;height:30px;border-radius:999px;cursor:pointer;color:#cbd5e1;display:grid;place-items:center}.star svg{width:19px;height:19px;fill:currentColor}.card.favorited .star{color:#f59e0b;background:#fff7ed}.card-run{width:100%;border:0;background:transparent;text-align:left;cursor:pointer;padding:0}.card-run:disabled{cursor:not-allowed}.card-top{display:flex;gap:14px;padding-right:32px}.icon{width:46px;height:46px;border-radius:16px;background:linear-gradient(135deg,rgba(59,130,246,.12),rgba(124,58,237,.10));display:grid;place-items:center;color:var(--brand);flex:0 0 auto}.icon svg{width:22px;height:22px}.card h3{font-size:16px;margin:2px 0 8px}.card p{font-size:13px;line-height:1.65;color:var(--muted);margin:0}.card-foot{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin-top:18px}.category,.command,.badge,.recent-badge{font-size:12px;border-radius:999px;padding:4px 9px}.category{background:var(--chip);color:var(--chipText)}.command{background:#fff7ed;color:#c2410c;border:1px solid #fed7aa}.badge{background:#ecfdf5;color:#047857;border:1px solid #bbf7d0}.recent-badge{background:#eef2ff;color:#4338ca;border:1px solid #c7d2fe}.empty{display:none;color:var(--muted);text-align:center;padding:70px 0}.empty.show{display:block}
.settings-page{animation:fadeUp .22s ease both}.settings-head{display:flex;align-items:flex-start;justify-content:space-between;gap:16px;background:var(--panel);border:1px solid var(--line);border-radius:26px;padding:26px;margin-bottom:18px;box-shadow:var(--shadow2)}.settings-head h2{margin:8px 0;font-size:25px}.settings-head p{margin:0;color:var(--muted);line-height:1.65}.settings-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}.setting-card{padding:20px}.setting-card.wide{grid-column:1/-1}.setting-card h3{margin:0 0 8px}.setting-card p{margin:0 0 16px;color:var(--muted);line-height:1.65}.setting-card code{display:block;white-space:normal;word-break:break-all;background:var(--panel2);border:1px solid var(--line);border-radius:12px;padding:10px;color:var(--muted);font-size:12px}.muted-path{font-size:12px;margin-top:12px!important}.theme-options{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.theme-option{position:relative;border:1px solid var(--line);border-radius:18px;background:var(--panel2);padding:16px 16px 16px 48px;cursor:pointer;min-height:88px}.theme-option input{position:absolute;left:16px;top:18px}.theme-option strong{display:block;margin-bottom:6px}.theme-option em{font-style:normal;color:var(--muted);font-size:12px;line-height:1.5}.theme-option:has(input:checked){border-color:rgba(59,130,246,.62);box-shadow:0 0 0 3px rgba(59,130,246,.12)}.switch{display:flex;align-items:center;gap:12px;cursor:pointer}.switch input{display:none}.switch span{width:48px;height:28px;border-radius:999px;background:#cbd5e1;position:relative;transition:.18s}.switch span:after{content:'';position:absolute;left:4px;top:4px;width:20px;height:20px;border-radius:999px;background:#fff;transition:.18s;box-shadow:0 2px 8px rgba(0,0,0,.16)}.switch input:checked+span{background:linear-gradient(135deg,var(--brand),var(--brand2))}.switch input:checked+span:after{transform:translateX(20px)}.switch em{font-style:normal;color:var(--muted)}.favorites-manager{margin-top:16px}.settings-subhead{display:flex;align-items:center;justify-content:space-between;gap:16px;margin-bottom:16px}.settings-subhead h3{margin:0 0 6px}.settings-subhead p{margin:0;color:var(--muted)}.favorite-list{display:flex;flex-direction:column;gap:10px}.favorite-row{display:flex;align-items:center;justify-content:space-between;gap:12px;background:var(--panel2);border:1px solid var(--line);border-radius:16px;padding:12px 14px}.favorite-row strong{display:block}.favorite-row em{display:block;font-style:normal;color:var(--muted);font-size:12px;margin-top:4px}.favorite-row button{border:0;background:#fff7ed;color:#c2410c;border-radius:999px;padding:7px 12px;cursor:pointer}.empty-line{padding:28px;text-align:center;color:var(--muted);background:var(--panel2);border:1px dashed var(--line);border-radius:18px}
.update-card{overflow:visible}.update-controls{display:grid;grid-template-columns:160px minmax(260px,1fr) auto;gap:12px;align-items:end}.update-controls.full{grid-template-columns:170px minmax(320px,1fr) auto}.update-controls label{display:flex;flex-direction:column;gap:7px;color:var(--muted);font-size:12px}.update-controls select,.update-controls input{width:100%;border:1px solid var(--line);border-radius:12px;background:var(--panel);color:var(--text);padding:11px 12px;outline:0}.update-controls select:focus,.update-controls input:focus{border-color:rgba(59,130,246,.46)}.update-actions{display:flex;gap:10px;align-items:center}.update-actions button{white-space:nowrap}.update-actions button:disabled{opacity:.48;cursor:not-allowed}.update-progress{margin-top:14px;border:1px solid var(--line);background:var(--panel);border-radius:999px;padding:8px 12px;display:grid;grid-template-columns:1fr auto;gap:12px;align-items:center;color:var(--muted);font-size:12px}.update-progress div{height:9px;background:var(--panel2);border-radius:999px;overflow:hidden}.update-progress span{display:block;height:100%;width:0;background:linear-gradient(135deg,var(--brand),var(--brand2));border-radius:999px;transition:width .2s ease}.update-progress.running span{width:42%;animation:progressPulse 1.1s ease-in-out infinite}.update-progress.done span{width:100%;animation:none}.update-progress.error span{width:100%;background:var(--err);animation:none}.update-result{margin-top:14px;border:1px solid var(--line);border-radius:18px;background:var(--panel2);padding:15px;color:var(--muted);line-height:1.65}.update-result strong{display:block;color:var(--text);margin-bottom:6px}.update-result p{margin:0 0 8px}.update-result .update-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px;margin-top:12px}.update-result .update-item{border:1px solid var(--line);background:var(--panel);border-radius:14px;padding:10px}.update-result .update-item span{display:block;font-size:12px;color:var(--muted);margin-bottom:4px}.update-result .update-item em{font-style:normal;color:var(--text);word-break:break-all}.update-result .notes{white-space:pre-line;border:1px solid var(--line);background:var(--panel);border-radius:14px;padding:12px;margin-top:12px}.update-result.success{border-color:#bbf7d0;background:#f0fdf4}.update-result.warning{border-color:#fed7aa;background:#fff7ed}.update-result.error{border-color:#fecaca;background:#fef2f2}.update-sources,.update-attempts{margin-top:12px;display:flex;flex-direction:column;gap:8px}.update-source,.update-attempt{border:1px solid var(--line);background:var(--panel);border-radius:12px;padding:9px 10px}.update-source b,.update-attempt b{display:block}.update-source small,.update-attempt small{display:block;color:var(--muted);word-break:break-all;margin-top:3px}.update-attempt.ok{border-color:#bbf7d0;background:#f0fdf4}.update-attempt.fail{border-color:#fecaca;background:#fef2f2}@keyframes progressPulse{0%{transform:translateX(-90%);width:35%}50%{width:55%}100%{transform:translateX(260%);width:35%}}
.toast-stack{position:fixed;right:24px;top:22px;z-index:50;display:flex;flex-direction:column;gap:10px}.toast{min-width:230px;max-width:380px;background:var(--panel);border:1px solid var(--line);border-left:4px solid var(--brand);border-radius:16px;padding:12px 14px;box-shadow:var(--shadow2);font-size:13px;color:var(--text);animation:toastIn .2s ease both}.toast.success{border-left-color:var(--ok)}.toast.warning{border-left-color:var(--warn)}.toast.error{border-left-color:var(--err)}.cmd-overlay{position:fixed;inset:0;z-index:40;background:rgba(15,23,42,.22);backdrop-filter:blur(5px);display:none;align-items:flex-start;justify-content:center;padding-top:76px}.cmd-overlay.show{display:flex;animation:fadeIn .16s ease both}.cmd-panel{width:min(720px,calc(100% - 42px));max-height:76vh;background:var(--panel);border:1px solid var(--line);border-radius:24px;box-shadow:0 30px 80px rgba(15,23,42,.22);overflow:hidden;animation:panelIn .2s ease both}.cmd-head{display:flex;align-items:center;gap:12px;padding:16px;border-bottom:1px solid var(--line)}.cmd-head input{flex:1;border:0;outline:0;font-size:16px;background:transparent;color:var(--text)}.cmd-key{font-size:12px;color:var(--muted);background:var(--panel2);border:1px solid var(--line);border-radius:9px;padding:4px 8px}.cmd-list{max-height:58vh;overflow:auto;padding:10px}.cmd-row{width:100%;border:0;background:transparent;border-radius:16px;padding:12px;text-align:left;display:flex;align-items:center;justify-content:space-between;gap:18px;cursor:pointer;color:var(--text)}.cmd-row:hover,.cmd-row.active{background:var(--panel2)}.cmd-row.disabled{opacity:.42;cursor:not-allowed}.cmd-row strong{display:block;font-size:14px;color:var(--text)}.cmd-row em{display:block;font-style:normal;font-size:12px;color:var(--muted);margin-top:4px;line-height:1.45}.cmd-row kbd{font-size:12px;color:#c2410c;background:#fff7ed;border:1px solid #fed7aa;border-radius:999px;padding:4px 8px}.cmd-empty{display:none;padding:34px;text-align:center;color:var(--muted)}.cmd-empty.show{display:block}.no-animations *,.no-animations *:before,.no-animations *:after{animation:none!important;transition:none!important;scroll-behavior:auto!important}@keyframes fadeIn{from{opacity:0}to{opacity:1}}@keyframes sideIn{from{opacity:0;transform:translateX(-10px)}to{opacity:1;transform:none}}@keyframes fadeUp{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:none}}@keyframes cardIn{from{opacity:0;transform:translateY(12px) scale(.985)}to{opacity:1;transform:none}}@keyframes toastIn{from{opacity:0;transform:translateX(12px)}to{opacity:1;transform:none}}@keyframes panelIn{from{opacity:0;transform:translateY(-10px) scale(.985)}to{opacity:1;transform:none}}@media(max-width:1040px){.shell{grid-template-columns:230px 1fr}.hero{grid-template-columns:1fr}.search{max-width:none;width:100%}.toolbar{align-items:flex-start;flex-direction:column}.toolbar-actions{width:100%;align-items:stretch}.ghost-btn{white-space:nowrap}.settings-grid,.theme-options,.update-controls{grid-template-columns:1fr}}

	.profiles-page{animation:fadeUp .24s ease both}.profiles-card{padding:20px}.profile-actions{display:flex;gap:10px;align-items:center}.danger-btn.warn{background:#fff7ed;border-color:#fdba74;color:#c2410c}.danger-btn.warn:hover{background:#ffedd5}.profile-tabs{display:flex;gap:10px;flex-wrap:wrap;border-bottom:1px solid var(--line);padding-bottom:12px;margin-bottom:14px}.profile-tab{border:1px solid var(--line);background:var(--panel2);border-radius:999px;padding:9px 14px;cursor:pointer;color:var(--muted);font-weight:800}.profile-tab.active{color:#fff;border-color:transparent;background:linear-gradient(135deg,var(--brand),var(--brand2));box-shadow:0 12px 24px rgba(59,130,246,.16)}.profile-editor{min-height:360px}.profile-note{display:flex;align-items:center;justify-content:space-between;gap:12px;margin:0 0 14px;color:var(--muted);font-size:13px}.profile-fields{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.profile-field{border:1px solid var(--line);background:var(--panel2);border-radius:14px;padding:12px}.profile-field.wide{grid-column:1/-1}.profile-field label{display:block;font-weight:800;font-size:13px;margin-bottom:7px}.profile-field p{margin:7px 0 0;color:var(--muted);font-size:12px;line-height:1.45}.profile-field input[type=text],.profile-field input[type=number],.profile-field select,.profile-field textarea{width:100%;border:1px solid var(--line);border-radius:10px;background:var(--panel);color:var(--text);padding:9px;outline:0}.profile-field textarea{min-height:138px;resize:vertical;line-height:1.55}.profile-check{display:flex;align-items:center;gap:10px;min-height:38px}.profile-check input{width:18px;height:18px;accent-color:var(--brand)}.profile-help{margin-top:14px;border:1px dashed var(--line);border-radius:14px;background:var(--panel2);padding:12px;color:var(--muted);font-size:12px;line-height:1.65}.profile-help strong{color:var(--text);margin-right:6px}
	.head-actions{display:flex;gap:10px;flex-wrap:wrap;justify-content:flex-end}.primary-btn{border:0;background:linear-gradient(135deg,var(--brand),var(--brand2));color:#fff;border-radius:14px;padding:11px 16px;font-weight:700;box-shadow:0 12px 24px rgba(59,130,246,.18);cursor:pointer}.rules-page{animation:fadeUp .32s ease both}.rules-card{padding:20px}.rules-toolbar{display:flex;justify-content:space-between;gap:18px;align-items:center;margin-bottom:14px}.rules-toolbar strong{display:block;font-size:18px}.rules-toolbar em{display:block;color:var(--muted);font-style:normal;font-size:12px;margin-top:4px}.rules-search{box-shadow:none;width:360px}.rules-actions{display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin-bottom:14px}.rules-table-wrap{overflow:auto;border:1px solid var(--line);border-radius:18px;background:var(--panel)}.rules-table{width:100%;min-width:1060px;border-collapse:separate;border-spacing:0}.rules-table th,.rules-table td{border-bottom:1px solid var(--line);padding:9px 8px;text-align:left;font-size:13px;vertical-align:middle}.rules-table th{position:sticky;top:0;background:var(--panel2);z-index:1;color:var(--muted);font-weight:700}.rules-table tr{cursor:pointer}.rules-table tr:hover{background:var(--panel2)}.rules-table tr.selected{background:linear-gradient(135deg,rgba(59,130,246,.10),rgba(124,58,237,.08))}.rules-table input[type=text],.rules-table select{width:100%;border:1px solid var(--line);border-radius:10px;background:var(--panel);color:var(--text);padding:8px;outline:0}.rules-table input[type=text]:focus,.rules-table select:focus{border-color:rgba(59,130,246,.50)}.rules-table input[type=checkbox]{width:18px;height:18px;accent-color:var(--brand)}.rules-table td:nth-child(1){width:46px;color:var(--muted);font-weight:700}.rules-table td:nth-child(2),.rules-table td:nth-child(8){text-align:center;width:78px}.rules-table td:nth-child(3){width:105px}.rules-table td:nth-child(4){width:230px}.rules-table td:nth-child(5){width:130px}.rules-table td:nth-child(6){width:150px}.rules-table td:nth-child(7){width:270px}

{{QUANTITY_DEFAULTS_STYLE}}
{{RECOGNITION_RULES_STYLE}}
{{ANNOTATION_SETTINGS_STYLE}}
{{LAYER_MANAGER_STYLE}}
{{QUANTITY_DASHBOARD_STYLE}}
{{QUANTITY_ATTRIBUTE_EDITOR_STYLE}}
{{SECTION_DRAWING_STYLE}}
</style>
</head>
<body data-theme=""{{THEME}}"" class=""{{ANIMATION_CLASS}}"">
<div id=""shell"" class=""shell{{SIDEBAR_CLASS}}"">
  <aside class=""sidebar"">
    <button id=""collapseSidebar"" class=""collapse-btn"" title=""折叠 / 展开侧栏"">☰</button>
    <div class=""brand""><div class=""logo"">CD</div><div><h1>CDBox Studio</h1></div></div>
    <div class=""nav-title"">Navigation</div><nav class=""nav"">{{NAV}}</nav>
    <div class=""side-tip"">Ctrl+K 可打开命令面板。Studio 不替换旧窗口，所有业务功能仍调用原命令或原窗口。<br><button id=""openLogsSide"">打开日志目录</button></div>
  </aside>
  <main class=""main"">
    <div id=""homePage""><section class=""hero""><div class=""hero-card""><span class=""kicker"">CDBox v2.2.2 Stable · Studio Preview 8</span></div><div class=""stats""><div class=""stat""><strong>{{TOTAL}}</strong><span>已接入卡片</span></div><div class=""stat""><strong>{{FAVORITES}}</strong><span>收藏功能</span></div><div class=""stat""><strong>{{RECENT}}</strong><span>最近使用</span></div><div class=""stat""><strong>{{ENABLED}}</strong><span>可直接调用</span></div></div></section><section class=""toolbar""><div class=""section-title""><h2 id=""sectionTitle"">全部功能</h2></div><div class=""toolbar-actions""><input id=""search"" class=""search"" placeholder=""搜索：表面积、GCL、断面、属性..."" /><button id=""cmdButton"" class=""ghost-btn"">Ctrl+K 命令面板</button></div></section><section id=""grid"" class=""grid"">{{CARDS}}</section><div id=""empty"" class=""empty"">没有匹配的功能卡片。</div></div>
    {{SETTINGS_PAGE}}
    {{ANNOTATION_SETTINGS_PAGE}}
    {{LAYER_MANAGER_PAGE}}
    {{QUANTITY_DASHBOARD_PAGE}}
    {{QUANTITY_ATTRIBUTE_EDITOR_PAGE}}
    {{SECTION_DRAWING_PAGE}}
    {{DEFAULT_PROFILES_PAGE}}
    {{RECOGNITION_RULES_PAGE}}
  </main>
</div>
<div id=""toastStack"" class=""toast-stack""></div>
<div id=""cmdOverlay"" class=""cmd-overlay"" aria-hidden=""true""><div class=""cmd-panel""><div class=""cmd-head""><span>⌘</span><input id=""cmdInput"" placeholder=""输入功能名称或命令，例如 GCL、断面、设置..."" /><span class=""cmd-key"">Esc</span></div><div id=""cmdList"" class=""cmd-list"">{{COMMAND_ROWS}}<div id=""cmdEmpty"" class=""cmd-empty"">没有匹配的命令。</div></div></div></div>
<script>
(function(){
  var active='总览';
  var search=document.getElementById('search');
  var title=document.getElementById('sectionTitle');
  var empty=document.getElementById('empty');
  var grid=document.getElementById('grid');
  var cards=[].slice.call(document.querySelectorAll('[data-card]'));
  var overlay=document.getElementById('cmdOverlay');
  var cmdInput=document.getElementById('cmdInput');
  var cmdRows=[].slice.call(document.querySelectorAll('[data-command-row]'));
  var cmdEmpty=document.getElementById('cmdEmpty');
  var selectedIndex=0;
  var shell=document.getElementById('shell');
  var homePage=document.getElementById('homePage');
  var settingsPage=document.getElementById('settingsPage');
  var annotationSettingsPage=document.getElementById('annotationSettingsPage');
  var layerManagerPage=document.getElementById('layerManagerPage');
  var quantityDashboardPage=document.getElementById('quantityDashboardPage');
  var quantityAttributeEditorPage=document.getElementById('quantityAttributeEditorPage');
  var sectionDrawingPage=document.getElementById('sectionDrawingPage');
  var defaultProfilesPage=document.getElementById('defaultProfilesPage');
  var recognitionRulesPage=document.getElementById('recognitionRulesPage');
{{QUANTITY_DEFAULTS_SCRIPT}}
{{RECOGNITION_RULES_SCRIPT}}
{{ANNOTATION_SETTINGS_SCRIPT}}
{{LAYER_MANAGER_SCRIPT}}
{{QUANTITY_DASHBOARD_SCRIPT}}
{{QUANTITY_ATTRIBUTE_EDITOR_SCRIPT}}
{{SECTION_DRAWING_SCRIPT}}

  var defaultProfilesData={{DEFAULT_PROFILES_JSON}};
  var builtInDefaultProfilesData={{BUILTIN_DEFAULT_PROFILES_JSON}};
  var defaultProfiles=cloneProfiles((defaultProfilesData&&defaultProfilesData.profiles)||[]);
  var builtInDefaultProfiles=cloneProfiles((builtInDefaultProfilesData&&builtInDefaultProfilesData.profiles)||[]);
  var activeProfileIndex=0;
  var recognitionRulesData={{RECOGNITION_RULES_JSON}};
  var defaultRecognitionRulesData={{DEFAULT_RECOGNITION_RULES_JSON}};
  var recognitionRulesEditor=null;
  var annotationSettingsEditor=null;
  var layerManagerEditor=null;
  var quantityDashboardEditor=null;
  var quantityAttributeEditor=null;
  var sectionDrawingEditor=null;
  var cardOrderKey='CDBoxStudio.CardOrder.v1';

  function saveCardOrder(){try{localStorage.setItem(cardOrderKey,JSON.stringify([].slice.call(grid.querySelectorAll('[data-card]')).map(function(x){return x.getAttribute('data-id');})));}catch(ignore){}}
  function restoreCardOrder(){try{var order=JSON.parse(localStorage.getItem(cardOrderKey)||'[]'),map={};cards.forEach(function(x){map[x.getAttribute('data-id')]=x;});order.forEach(function(id){if(map[id])grid.appendChild(map[id]);});cards.forEach(function(x){if(order.indexOf(x.getAttribute('data-id'))<0)grid.appendChild(x);});cards=[].slice.call(grid.querySelectorAll('[data-card]'));}catch(ignore){}}
  function bindCardDragging(){var dragged=null;cards.forEach(function(card){var handle=card.querySelector('[data-card-drag]');if(!handle)return;handle.addEventListener('mousedown',function(ev){ev.stopPropagation();card.setAttribute('draggable','true');});handle.addEventListener('mouseup',function(){card.removeAttribute('draggable');});handle.addEventListener('click',function(ev){ev.preventDefault();ev.stopPropagation();});card.addEventListener('dragstart',function(ev){if(card.getAttribute('draggable')!=='true'){ev.preventDefault();return;}dragged=card;card.classList.add('dragging');if(ev.dataTransfer)ev.dataTransfer.effectAllowed='move';});card.addEventListener('dragover',function(ev){if(!dragged||dragged===card)return;ev.preventDefault();card.classList.add('drag-target');});card.addEventListener('dragleave',function(){card.classList.remove('drag-target');});card.addEventListener('drop',function(ev){if(!dragged||dragged===card)return;ev.preventDefault();var box=card.getBoundingClientRect(),after=ev.clientY>box.top+box.height/2||(Math.abs(ev.clientY-(box.top+box.height/2))<box.height*.25&&ev.clientX>box.left+box.width/2);grid.insertBefore(dragged,after?card.nextSibling:card);cards=[].slice.call(grid.querySelectorAll('[data-card]'));saveCardOrder();card.classList.remove('drag-target');});card.addEventListener('dragend',function(){cards.forEach(function(x){x.classList.remove('dragging','drag-target');x.removeAttribute('draggable');});dragged=null;saveCardOrder();});});}

  function post(name,arg){if(window.chrome&&chrome.webview){chrome.webview.postMessage('studio|'+name+'|'+encodeURIComponent(arg||''));}}
  function toast(message,kind){var stack=document.getElementById('toastStack');var node=document.createElement('div');node.className='toast '+(kind||'info');node.textContent=message||'';stack.appendChild(node);setTimeout(function(){node.style.opacity='0';node.style.transform='translateX(10px)';},2600);setTimeout(function(){if(node.parentNode)node.parentNode.removeChild(node);},3100);} window.CDBoxStudioToast=toast;
  function hideStudioPages(){if(quantityDashboardEditor&&quantityDashboardEditor.suspend)quantityDashboardEditor.suspend();homePage.style.display='none';settingsPage.style.display='none';annotationSettingsPage.style.display='none';layerManagerPage.style.display='none';quantityDashboardPage.style.display='none';quantityAttributeEditorPage.style.display='none';sectionDrawingPage.style.display='none';defaultProfilesPage.style.display='none';recognitionRulesPage.style.display='none';}
  function setActiveNavigation(name){document.querySelectorAll('[data-filter]').forEach(function(x){x.classList.toggle('active',(x.getAttribute('data-filter')||'')===name);});}
  function showHome(){hideStudioPages();homePage.style.display='block';}
  function showSettings(){hideStudioPages();settingsPage.style.display='block';active='设置';setActiveNavigation('设置');post('filter','设置');}
  function initLayerManagerPage(){if(layerManagerEditor)return layerManagerEditor;layerManagerEditor=window.CDBoxLayerManagerPage.create({rootId:'layerManagerPage',post:post,toast:toast,standalone:false});return layerManagerEditor;}
  function showLayerManager(){hideStudioPages();layerManagerPage.style.display='block';active='图层管理器';setActiveNavigation('图层管理器');initLayerManagerPage();post('layerManagerOpened','');post('filter','图层管理器');}
  function initQuantityDashboardPage(){if(quantityDashboardEditor)return quantityDashboardEditor;quantityDashboardEditor=window.CDBoxQuantityDashboardPage.create({rootId:'quantityDashboardPage',post:post,toast:toast,standalone:false});var host=quantityDashboardPage.querySelector('.qd-head-actions');if(host&&!host.querySelector('[data-open-quantity-window]')){var button=document.createElement('button');button.className='qd-btn';button.setAttribute('data-open-quantity-window','1');button.textContent='独立窗口';button.addEventListener('click',function(){post('openQuantityDashboardWindow','');});host.insertBefore(button,host.firstChild);}return quantityDashboardEditor;}
  function showQuantityDashboard(){hideStudioPages();quantityDashboardPage.style.display='block';active='工程量';setActiveNavigation('工程量');initQuantityDashboardPage().open();post('quantityDashboardOpened','');post('filter','工程量');}
  function initQuantityAttributeEditor(){if(quantityAttributeEditor)return quantityAttributeEditor;quantityAttributeEditor=window.CDBoxQuantityAttributeEditorPage.create({rootId:'quantityAttributeEditorPage',post:post,toast:toast,standalone:false});return quantityAttributeEditor;}
  function showQuantityAttributeEditor(){hideStudioPages();quantityAttributeEditorPage.style.display='block';active='属性编辑器';setActiveNavigation('管线属性');initQuantityAttributeEditor().open();post('filter','属性编辑器');}
  function initSectionDrawingPage(){if(sectionDrawingEditor)return sectionDrawingEditor;sectionDrawingEditor=window.CDBoxSectionDrawingPage.create({rootId:'sectionDrawingPage',post:post,toast:toast,standalone:false});return sectionDrawingEditor;}
  function showSectionDrawing(){hideStudioPages();sectionDrawingPage.style.display='block';active='断面';setActiveNavigation('断面');initSectionDrawingPage().open();post('filter','断面图生成');}
  function initAnnotationSettingsPage(){if(annotationSettingsEditor)return annotationSettingsEditor;annotationSettingsEditor=window.CDBoxAnnotationSettingsPage.create({rootId:'annotationSettingsPage',post:post,toast:toast,standalone:false,initialSection:'surface',syncHash:true});return annotationSettingsEditor;}
  function showAnnotationSettings(section){hideStudioPages();annotationSettingsPage.style.display='block';active='标注设置';setActiveNavigation('标注设置');initAnnotationSettingsPage().open(section||'surface');post('annotationSettingsOpened',section||'surface');post('filter','标注设置');}
  function showDefaultProfiles(){hideStudioPages();defaultProfilesPage.style.display='block';active='属性默认表';setActiveNavigation('属性默认表');renderDefaultProfiles();post('filter','属性默认表');}
  function showRecognitionRules(){hideStudioPages();recognitionRulesPage.style.display='block';active='属性识别表';setActiveNavigation('属性识别表');renderRules();post('filter','属性识别表');}
  function apply(){if(active==='设置'){showSettings();return;}if(active==='图层管理器'){showLayerManager();return;}if(active==='工程量'){showQuantityDashboard();return;}if(active==='属性编辑器'){showQuantityAttributeEditor();return;}if(active==='断面'){showSectionDrawing();return;}if(active==='标注设置'){showAnnotationSettings(annotationSettingsEditor?annotationSettingsEditor.section:'surface');return;}if(active==='属性默认表'){showDefaultProfiles();return;}if(active==='属性识别表'){showRecognitionRules();return;}showHome();var q=(search.value||'').trim().toLowerCase();var visible=0;cards.forEach(function(card){var cat=card.getAttribute('data-category')||'';var text=card.innerText.toLowerCase()+' '+(card.getAttribute('data-command')||'').toLowerCase();var byCat=active==='总览'||cat===active||(active==='收藏'&&card.getAttribute('data-favorite')==='1')||(active==='最近使用'&&card.getAttribute('data-recent')==='1');var byText=!q||text.indexOf(q)>=0;var show=byCat&&byText;card.style.display=show?'flex':'none';if(show) visible++;});title.textContent=active==='总览'?'全部功能':active;empty.className=visible?'empty':'empty show';post('filter',title.textContent);}
  function withPageLeave(next){if(active==='标注设置'&&annotationSettingsEditor&&annotationSettingsEditor.isDirty()){annotationSettingsEditor.requestLeave(next);return;}if(active==='图层管理器'&&layerManagerEditor&&layerManagerEditor.isDirty()){layerManagerEditor.requestLeave(next);return;}next();}
  var latestUpdateManifest=null;
  function buildSettingsPayload(){var theme=(document.querySelector('input[name=studioTheme]:checked')||{}).value||'light';var animations=document.getElementById('animationsToggle').checked?'1':'0';var collapsed=document.getElementById('sidebarToggle').checked?'1':'0';var channel=(document.getElementById('updateChannel')||{}).value||'studio-preview';var source=(document.getElementById('updateSourceUrl')||{}).value||'';return 'theme='+encodeURIComponent(theme)+'&animations='+animations+'&sidebarCollapsed='+collapsed+'&updateChannel='+encodeURIComponent(channel)+'&updateSourceUrl='+encodeURIComponent(source);}
  function saveSettings(){var theme=(document.querySelector('input[name=studioTheme]:checked')||{}).value||'light';var animations=document.getElementById('animationsToggle').checked?'1':'0';var collapsed=document.getElementById('sidebarToggle').checked?'1':'0';document.body.setAttribute('data-theme',theme);document.body.classList.toggle('no-animations',animations!=='1');shell.classList.toggle('sidebar-collapsed',collapsed==='1');post('settings',buildSettingsPayload());}
  function setUpdateProgress(kind,text){var bar=document.getElementById('updateProgress');if(!bar)return;bar.className='update-progress '+(kind||'idle');var em=bar.querySelector('em');if(em)em.textContent=text||'等待操作';}
  window.CDBoxStudioUpdateProgress=function(data){data=data||{};var bar=document.getElementById('updateProgress');if(!bar)return;var percent=Math.max(0,Math.min(100,Number(data.percent||0)));bar.className='update-progress '+(data.kind||'progress');var span=bar.querySelector('span');if(span)span.style.width=percent+'%';var em=bar.querySelector('em');if(em)em.textContent=(data.message||'下载进度')+(percent>0?' · '+percent+'%':'');};
  function setDownloadButton(enabled){var btn=document.getElementById('downloadUpdateButton');if(btn)btn.disabled=!enabled;}
  function checkUpdate(){var box=document.getElementById('updateResult');latestUpdateManifest=null;setDownloadButton(false);setUpdateProgress('running','正在检查 update.json');if(box){box.className='update-result';box.innerHTML='<strong>正在检查更新...</strong><p>正在从国内 update.json 更新源读取固定 studio-preview 清单。</p>';}post('checkUpdate',buildSettingsPayload());}
  function downloadUpdate(){var box=document.getElementById('updateResult');setDownloadButton(false);setUpdateProgress('running','正在下载、校验并准备独立更新器');if(box){box.className='update-result';box.innerHTML='<strong>正在下载、校验并准备安装...</strong><p>将按 update.json 中 package.urls 的顺序尝试下载，失败会自动切换下载源。校验通过后会启动独立更新器，请正常关闭 AutoCAD，更新器不会强制结束进程。</p>';}post('downloadUpdate',buildSettingsPayload());}
  function htmlEscape(v){return String(v||'').replace(/[&<>""']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[c];});}
  function renderUpdateSources(list){if(!list||!list.length)return '<div class=""update-sources""><strong>下载源</strong><div class=""update-source""><b>未配置</b><small>package.urls 为空。</small></div></div>';return '<div class=""update-sources""><strong>下载源 / 多源 fallback 顺序</strong>'+list.map(function(s,i){return '<div class=""update-source""><b>'+(i+1)+'. '+htmlEscape(s.name||'下载源')+(s.enabled===false?'（已禁用）':'')+'</b><small>'+htmlEscape(s.url||'')+'</small>'+(s.sha256?'<small>SHA256：'+htmlEscape(s.sha256)+'</small>':'')+'</div>';}).join('')+'</div>';}
  window.CDBoxStudioUpdateResult=function(data){var box=document.getElementById('updateResult');if(!box)return;data=data||{};var ok=!!data.success;latestUpdateManifest=ok?data:null;setDownloadButton(ok&&data.updateAvailable&&data.sources&&data.sources.length);setUpdateProgress(ok?'done':'error',ok?'检查完成':'检查失败');box.className='update-result '+(ok?(data.updateAvailable?'success':''):'error');if(!ok){box.innerHTML='<strong>检查更新失败</strong><p>'+htmlEscape(data.errorMessage||'未知错误')+'</p><div class=""update-grid""><div class=""update-item""><span>当前版本</span><em>'+htmlEscape(data.currentVersion||'')+'</em></div><div class=""update-item""><span>更新源</span><em>'+htmlEscape(data.sourceUrl||'')+'</em></div></div>';return;}var title=data.updateAvailable?'发现新版本':'当前版本与更新源一致';var notes=data.notes?'<div class=""notes""><strong>更新说明</strong>'+htmlEscape(data.notes)+'</div>':'<div class=""notes""><strong>更新说明</strong>update.json 未提供 notes[]。</div>';box.innerHTML='<strong>'+title+'</strong><p>'+htmlEscape(data.title||'')+'</p><div class=""update-grid""><div class=""update-item""><span>当前版本</span><em>'+htmlEscape(data.currentVersion||'')+'</em></div><div class=""update-item""><span>当前版本码</span><em>'+htmlEscape(data.currentVersionCode||'')+'</em></div><div class=""update-item""><span>最新版本</span><em>'+htmlEscape(data.latestVersion||'')+'</em></div><div class=""update-item""><span>最新版本码</span><em>'+htmlEscape(data.versionCode||'')+'</em></div><div class=""update-item""><span>更新通道</span><em>'+htmlEscape(data.channel||'')+'</em></div><div class=""update-item""><span>发布日期</span><em>'+htmlEscape(data.releaseDate||'')+'</em></div><div class=""update-item""><span>强制更新</span><em>'+(data.mandatory?'是':'否')+'</em></div><div class=""update-item""><span>包大小</span><em>'+htmlEscape(data.packageSizeText||'未提供')+'</em></div><div class=""update-item""><span>包文件</span><em>'+htmlEscape(data.packageFileName||'')+'</em></div><div class=""update-item""><span>SHA256</span><em>'+htmlEscape(data.sha256||'')+'</em></div><div class=""update-item""><span>更新源</span><em>'+htmlEscape(data.sourceUrl||'')+'</em></div><div class=""update-item""><span>检查时间</span><em>'+htmlEscape(data.checkedAt||'')+'</em></div></div>'+notes+renderUpdateSources(data.sources)+'<p class=""muted-path"">下一步可点击“下载、校验并准备安装”。校验通过后会生成 pending-update.json，并把 CDBoxUpdater.exe 复制到插件目录外启动。</p>';};
  window.CDBoxStudioDownloadResult=function(data){var box=document.getElementById('updateResult');if(!box)return;data=data||{};var ok=!!data.success&&!!data.verified;setUpdateProgress(ok?'done':'error',ok?'下载完成，SHA256 校验通过':'下载或校验失败');setDownloadButton(!!latestUpdateManifest&&!!latestUpdateManifest.updateAvailable);box.className='update-result '+(ok?'success':'error');var attempts='';if(data.attempts&&data.attempts.length){attempts='<div class=""update-attempts""><strong>下载尝试记录</strong>'+data.attempts.map(function(a,i){return '<div class=""update-attempt '+(a.success?'ok':'fail')+'""><b>'+(i+1)+'. '+htmlEscape(a.sourceName||'下载源')+' · '+(a.success?'成功':'失败')+'</b><small>'+htmlEscape(a.sourceUrl||'')+'</small><small>下载：'+htmlEscape(a.bytesReceived||0)+' / '+htmlEscape(a.bytesTotal||0)+' 字节</small>'+(a.sha256Actual?'<small>实际 SHA256：'+htmlEscape(a.sha256Actual)+'</small>':'')+(a.errorMessage?'<small>错误：'+htmlEscape(a.errorMessage)+'</small>':'')+'</div>';}).join('')+'</div>';}
    var installReady=ok&&!!data.installerStarted;var installText=installReady?'更新器已启动，请正常关闭 AutoCAD。更新器会等待所有相关 AutoCAD 进程退出，不会强制结束。':(ok?'更新包校验通过，但更新器未启动：'+htmlEscape(data.installerErrorMessage||'请查看 Studio 日志') : htmlEscape(data.errorMessage||'未知错误'));box.innerHTML='<strong>'+(installReady?'更新已准备，等待关闭 AutoCAD':(ok?'更新包已下载并通过校验':'更新包下载 / 校验失败'))+'</strong><p>'+installText+'</p><div class=""update-grid""><div class=""update-item""><span>目标版本</span><em>'+htmlEscape(data.latestVersion||'')+'</em></div><div class=""update-item""><span>版本码</span><em>'+htmlEscape(data.versionCode||'')+'</em></div><div class=""update-item""><span>包文件</span><em>'+htmlEscape(data.packageFileName||'')+'</em></div><div class=""update-item""><span>包大小</span><em>'+htmlEscape(data.packageSizeText||'')+'</em></div><div class=""update-item""><span>成功下载源</span><em>'+htmlEscape(data.sourceName||'')+'</em></div><div class=""update-item""><span>保存路径</span><em>'+htmlEscape(data.filePath||'')+'</em></div><div class=""update-item""><span>目标 bundle</span><em>'+htmlEscape(data.targetBundlePath||'')+'</em></div><div class=""update-item""><span>pending-update.json</span><em>'+htmlEscape(data.pendingUpdatePath||'')+'</em></div><div class=""update-item""><span>更新器</span><em>'+htmlEscape(data.updaterPath||'')+'</em></div><div class=""update-item""><span>更新器日志</span><em>'+htmlEscape(data.updaterLogPath||'')+'</em></div><div class=""update-item""><span>安装结果</span><em>'+htmlEscape(data.lastUpdateResultPath||'')+'</em></div><div class=""update-item""><span>期望 SHA256</span><em>'+htmlEscape(data.sha256Expected||'')+'</em></div><div class=""update-item""><span>实际 SHA256</span><em>'+htmlEscape(data.sha256Actual||'')+'</em></div></div>'+attempts+'<p class=""muted-path"">'+(installReady?'关闭 AutoCAD 后，更新器将二次校验 SHA256、解压完整 CDBox.bundle、校验必要文件、备份旧 bundle 后替换；失败时自动回滚。用户 AppData 配置不会被覆盖。':'更新器命令：'+htmlEscape(data.installerCommand||''))+'</p>';};

  function runIdDirect(id){if(!id)return;if(id==='__openLogs'){post('openLogs','');return;}if(id==='__layerManager'||id==='module:layer-manager'){showLayerManager();return;}if(id==='__layerManagerWindow'){post('openLayerManagerWindow','');return;}if(id==='__quantityDashboard'||id==='module:quantity-calculation'){showQuantityDashboard();return;}if(id==='__quantityDashboardWindow'){post('openQuantityDashboardWindow','');return;}if(id==='__quantityAttributeEditor'||id==='module:quantity-pipe-attributes'){showQuantityAttributeEditor();return;}if(id==='__sectionDrawing'||id==='module:section-drawing'){showSectionDrawing();return;}if(id==='__sectionDrawingWindow'){post('openSectionDrawingWindow','');return;}if(id==='__quantityFormalReport'){post('exportQuantityReport','{}');return;}if(id==='__settings'){showSettings();return;}if(id==='__checkUpdate'){showSettings();setTimeout(checkUpdate,80);return;}if(id==='__annotationSettings'||id==='module:annotation-settings'){showAnnotationSettings('surface');return;}if(id==='__annotationSurface'){showAnnotationSettings('surface');return;}if(id==='__annotationPipeLength'){showAnnotationSettings('pipeLength');return;}if(id==='__annotationNode'){showAnnotationSettings('node');return;}if(id==='__defaultProfiles'){showDefaultProfiles();return;}if(id==='__defaultProfilesWindow'){post('openDefaultProfilesWindow','');return;}if(id==='__recognitionRules'){showRecognitionRules();return;}if(id==='__recognitionRulesWindow'){post('openRecognitionWindow','');return;}post('run',id);}  function runId(id){if(!id)return;if(active==='工程量'&&(id==='__quantityDashboard'||id==='module:quantity-calculation')){runIdDirect(id);return;}if(active==='属性编辑器'&&(id==='__quantityAttributeEditor'||id==='module:quantity-pipe-attributes')){runIdDirect(id);return;}if(active==='断面'&&(id==='__sectionDrawing'||id==='module:section-drawing')){runIdDirect(id);return;}if(active==='图层管理器'&&(id==='__layerManager'||id==='module:layer-manager')){runIdDirect(id);return;}if(active==='标注设置'&&(id==='__annotationSettings'||id==='__annotationSurface'||id==='__annotationPipeLength'||id==='__annotationNode'||id==='module:annotation-settings')){runIdDirect(id);return;}withPageLeave(function(){runIdDirect(id);});}
  function visibleCommandRows(){return cmdRows.filter(function(row){return row.style.display!=='none'&&!row.disabled;});}
  function updateSelection(delta){var rows=visibleCommandRows();if(!rows.length)return;selectedIndex=(selectedIndex+delta+rows.length)%rows.length;cmdRows.forEach(function(r){r.classList.remove('active');});rows[selectedIndex].classList.add('active');rows[selectedIndex].scrollIntoView({block:'nearest'});}
  function filterCommands(){var q=(cmdInput.value||'').trim().toLowerCase();var count=0;cmdRows.forEach(function(row){var hit=!q||(row.getAttribute('data-search')||row.innerText.toLowerCase()).indexOf(q)>=0;row.style.display=hit?'flex':'none';if(hit&&!row.disabled)count++;row.classList.remove('active');});selectedIndex=0;var rows=visibleCommandRows();if(rows.length)rows[0].classList.add('active');cmdEmpty.className=count?'cmd-empty':'cmd-empty show';}
  function openCommandPanel(){overlay.classList.add('show');overlay.setAttribute('aria-hidden','false');cmdInput.value='';filterCommands();setTimeout(function(){cmdInput.focus();},20);}
  function closeCommandPanel(){overlay.classList.remove('show');overlay.setAttribute('aria-hidden','true');}

  function initRecognitionRulesPage(){
    if(recognitionRulesEditor)return recognitionRulesEditor;
    recognitionRulesEditor=window.CDBoxRecognitionRulesPage.create({rootId:'recognitionRulesPage',data:recognitionRulesData,defaultData:defaultRecognitionRulesData,toast:toast,post:post,standalone:false,autoOpenSearch:false});
    return recognitionRulesEditor;
  }
  function renderRules(){initRecognitionRulesPage().render();}

  document.querySelectorAll('[data-filter]').forEach(function(btn){btn.addEventListener('click',function(){var target=btn.getAttribute('data-filter')||'总览';if(target===active){apply();return;}withPageLeave(function(){active=target;apply();});});});
  restoreCardOrder();bindCardDragging();
  cards.forEach(function(card){var run=card.querySelector('[data-run-button]');var fav=card.querySelector('[data-favorite-button]');if(run){run.addEventListener('click',function(){if(run.disabled)return;runId(card.getAttribute('data-id'));});}if(fav){fav.addEventListener('click',function(ev){ev.stopPropagation();post('favorite',card.getAttribute('data-id'));});}});
  cmdRows.forEach(function(row){row.addEventListener('click',function(){if(row.disabled)return;runId(row.getAttribute('data-id'));closeCommandPanel();});});
  document.querySelectorAll('input[name=studioTheme]').forEach(function(x){x.addEventListener('change',saveSettings);});
  document.getElementById('animationsToggle').addEventListener('change',saveSettings);
  document.getElementById('sidebarToggle').addEventListener('change',saveSettings);
  document.getElementById('updateChannel').addEventListener('change',saveSettings);
  document.getElementById('updateSourceUrl').addEventListener('change',saveSettings);
  document.getElementById('checkUpdateButton').addEventListener('click',checkUpdate);
  document.getElementById('downloadUpdateButton').addEventListener('click',downloadUpdate);
  document.querySelectorAll('[data-remove-favorite]').forEach(function(btn){btn.addEventListener('click',function(){post('removeFavorite',btn.getAttribute('data-remove-favorite'));});});
  document.getElementById('clearRecentButton').addEventListener('click',function(){post('clearRecent','');});
  document.getElementById('backToHome').addEventListener('click',function(){active='总览';document.querySelectorAll('[data-filter]').forEach(function(x){x.classList.toggle('active',(x.getAttribute('data-filter')||'')==='总览');});apply();});
  document.getElementById('backToHomeFromRules').addEventListener('click',function(){active='总览';document.querySelectorAll('[data-filter]').forEach(function(x){x.classList.toggle('active',(x.getAttribute('data-filter')||'')==='总览');});apply();});
  document.getElementById('backToHomeFromDefaults').addEventListener('click',function(){active='总览';document.querySelectorAll('[data-filter]').forEach(function(x){x.classList.toggle('active',(x.getAttribute('data-filter')||'')==='总览');});apply();});
  document.getElementById('saveDefaultProfilesButton').addEventListener('click',saveDefaultProfiles);
  document.getElementById('restoreDefaultProfilesButton').addEventListener('click',restoreDefaultProfiles);
  document.getElementById('openDefaultProfilesWindow').addEventListener('click',function(){post('openDefaultProfilesWindow','');});
  document.getElementById('openLegacyDefaultProfiles').addEventListener('click',function(){post('openLegacyDefaultProfiles','');});
  initRecognitionRulesPage();
  document.getElementById('openRecognitionWindow').addEventListener('click',function(){post('openRecognitionWindow','');});
  search.addEventListener('input',apply);cmdInput.addEventListener('input',filterCommands);document.getElementById('cmdButton').addEventListener('click',openCommandPanel);document.getElementById('openLogsSide').addEventListener('click',function(){post('openLogs','');});document.getElementById('collapseSidebar').addEventListener('click',function(){shell.classList.toggle('sidebar-collapsed');});overlay.addEventListener('click',function(ev){if(ev.target===overlay)closeCommandPanel();});
  document.addEventListener('keydown',function(ev){if((ev.ctrlKey||ev.metaKey)&&ev.key.toLowerCase()==='k'){ev.preventDefault();openCommandPanel();return;}if(!overlay.classList.contains('show'))return;if(ev.key==='Escape'){closeCommandPanel();return;}if(ev.key==='ArrowDown'){ev.preventDefault();updateSelection(1);return;}if(ev.key==='ArrowUp'){ev.preventDefault();updateSelection(-1);return;}if(ev.key==='Enter'){var rows=visibleCommandRows();if(rows[selectedIndex]){runId(rows[selectedIndex].getAttribute('data-id'));closeCommandPanel();}}});
  renderDefaultProfiles();initRecognitionRulesPage();renderRules();apply();filterCommands();setTimeout(function(){post('ready','');},60);
})();
</script>
</body></html>";
        }

        private static List<string> BuildCategories(List<CDBoxStudioAction> actions, CDBoxStudioState state)
        {
            var result = new List<string>();
            foreach (string category in CategoryOrder)
            {
                if (category == "总览"
                    || category == "设置"
                    || category == "图层管理器"
                    || category == "标注设置"
                    || category == "属性默认表"
                    || category == "属性识别表"
                    || category == "工程量"
                    || (category == "收藏" && state.FavoriteIds.Count > 0)
                    || (category == "最近使用" && state.RecentItems.Count > 0)
                    || actions.Any(a => Same(a.Category, category))) result.Add(category);
            }

            foreach (string category in actions.Select(a => a.Category).Distinct())
            {
                if (!result.Any(x => Same(x, category))) result.Add(category);
            }

            return result;
        }

        private static int GetCategoryCount(string category, List<CDBoxStudioAction> actions, CDBoxStudioState state)
        {
            if (Same(category, "总览")) return actions.Count;
            if (Same(category, "收藏")) return actions.Count(a => state.IsFavorite(a.Id));
            if (Same(category, "最近使用")) return actions.Count(a => state.HasRecent(a.Id));
            if (Same(category, "设置")) return 1;
            if (Same(category, "图层管理器")) return 1;
            if (Same(category, "标注设置")) return 1;
            if (Same(category, "属性默认表")) return 1;
            if (Same(category, "属性识别表")) return 1;
            if (Same(category, "工程量")) return 1;
            return actions.Count(a => Same(a.Category, category));
        }

        private static IEnumerable<CDBoxStudioAction> SortActions(List<CDBoxStudioAction> actions, CDBoxStudioState state)
        {
            return actions
                .OrderBy(a => CategoryIndex(a.Category))
                .ThenBy(a => state.GetRecentRank(a.Id))
                .ThenByDescending(a => state.IsFavorite(a.Id))
                .ThenBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase);
        }

        private static int CategoryIndex(string category)
        {
            for (int i = 0; i < CategoryOrder.Length; i++)
            {
                if (Same(CategoryOrder[i], category)) return i;
            }
            return 999;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string Html(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private static string HtmlAttr(string text)
        {
            return Html(text).Replace("\r", string.Empty).Replace("\n", " ");
        }

        private static string BuildIcon(string category)
        {
            if (Same(category, "标注")) return "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M4 7h16\"/><path d=\"M4 12h10\"/><path d=\"M4 17h16\"/></svg>";
            if (Same(category, "断面")) return "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M4 18h16\"/><path d=\"M7 18V8h10v10\"/><circle cx=\"12\" cy=\"14\" r=\"2.5\"/></svg>";
            if (Same(category, "工程量") || Same(category, "管线属性")) return "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M8 6h13\"/><path d=\"M8 12h13\"/><path d=\"M8 18h13\"/><path d=\"M3 6h.01\"/><path d=\"M3 12h.01\"/><path d=\"M3 18h.01\"/></svg>";
            if (Same(category, "设置")) return "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M12 15.5A3.5 3.5 0 1 0 12 8a3.5 3.5 0 0 0 0 7.5Z\"/><path d=\"M19.4 15a1.7 1.7 0 0 0 .34 1.88l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.7 1.7 0 0 0-1.88-.34 1.7 1.7 0 0 0-1 1.56V21a2 2 0 1 1-4 0v-.09a1.7 1.7 0 0 0-1-1.56 1.7 1.7 0 0 0-1.88.34l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.7 1.7 0 0 0 4.6 15a1.7 1.7 0 0 0-1.56-1H3a2 2 0 1 1 0-4h.09a1.7 1.7 0 0 0 1.56-1 1.7 1.7 0 0 0-.34-1.88l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.7 1.7 0 0 0 9 4.6a1.7 1.7 0 0 0 1-1.56V3a2 2 0 1 1 4 0v.09a1.7 1.7 0 0 0 1 1.56 1.7 1.7 0 0 0 1.88-.34l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.7 1.7 0 0 0 19.4 9c.08.3.24.58.46.8.22.22.5.38.8.46H21a2 2 0 1 1 0 4h-.09a1.7 1.7 0 0 0-1.51.74Z\"/></svg>";
            return "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M4 7h16\"/><path d=\"M4 12h16\"/><path d=\"M4 17h10\"/></svg>";
        }
    }
}

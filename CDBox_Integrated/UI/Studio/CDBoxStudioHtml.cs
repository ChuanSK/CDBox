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
            "标注",
            "断面",
            "管线属性",
            "工程量",
            "图框工具",
            "系统",
            "其他"
        };

        public static string Build(IEnumerable<CDBoxStudioAction> actions, CDBoxStudioState state, string runtimeVersion, string logFilePath)
        {
            List<CDBoxStudioAction> list = actions == null
                ? new List<CDBoxStudioAction>()
                : actions.Where(a => a != null).ToList();

            state = state ?? new CDBoxStudioState();

            string nav = BuildNavigation(list, state);
            string cards = BuildCards(list, state);
            string commandRows = BuildCommandRows(list);

            string html = Template();
            html = html.Replace("{{NAV}}", nav);
            html = html.Replace("{{CARDS}}", cards);
            html = html.Replace("{{COMMAND_ROWS}}", commandRows);
            html = html.Replace("{{TOTAL}}", list.Count.ToString());
            html = html.Replace("{{ENABLED}}", list.Count(a => a.Enabled).ToString());
            html = html.Replace("{{FAVORITES}}", state.FavoriteIds.Count.ToString());
            html = html.Replace("{{RECENT}}", state.RecentItems.Count.ToString());
            html = html.Replace("{{RUNTIME}}", Html(string.IsNullOrWhiteSpace(runtimeVersion) ? "WebView2" : runtimeVersion));
            html = html.Replace("{{LOG_PATH}}", Html(logFilePath ?? string.Empty));
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

                cards.Append("<div class=\"card-actions\"><button class=\"star\" data-favorite-button title=\"").Append(favoriteTitle).Append("\" aria-label=\"").Append(favoriteTitle).Append("\">")
                    .Append("<svg viewBox=\"0 0 24 24\"><path d=\"M12 3.6l2.6 5.3 5.8.8-4.2 4.1 1 5.8-5.2-2.8-5.2 2.8 1-5.8-4.2-4.1 5.8-.8L12 3.6z\"/></svg>")
                    .Append("</button></div>");

                cards.Append("<button class=\"card-run\" data-run-button");
                if (!action.Enabled) cards.Append(" disabled");
                cards.Append(">");
                cards.Append("<div class=\"card-top\"><div class=\"icon\">").Append(BuildIcon(action.Category)).Append("</div><div class=\"card-meta\">")
                     .Append("<h3>").Append(Html(action.Title)).Append("</h3>")
                     .Append("<p>").Append(Html(action.Description)).Append("</p>")
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
            foreach (CDBoxStudioAction action in SortActions(actions, new CDBoxStudioState()))
            {
                string disabled = action.Enabled ? string.Empty : " disabled";
                rows.Append("<button class=\"cmd-row").Append(disabled).Append("\" data-command-row data-id=\"").Append(HtmlAttr(action.Id)).Append("\"")
                    .Append(" data-search=\"").Append(HtmlAttr((action.Title + " " + action.Category + " " + action.Description + " " + action.CommandName).ToLowerInvariant())).Append("\"");
                if (!action.Enabled) rows.Append(" disabled");
                rows.Append("><span><strong>").Append(Html(action.Title)).Append("</strong><em>").Append(Html(action.Description)).Append("</em></span>");
                if (!string.IsNullOrWhiteSpace(action.CommandName)) rows.Append("<kbd>").Append(Html(action.CommandName)).Append("</kbd>");
                rows.Append("</button>");
            }

            rows.Append("<button class=\"cmd-row\" data-command-row data-id=\"__openLogs\" data-search=\"studio 日志 log openlogs\"><span><strong>打开 Studio 日志目录</strong><em>查看 WebView2 初始化、路由调用和错误记录。</em></span><kbd>LOG</kbd></button>");
            return rows.ToString();
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
:root{--bg:#f5f7fb;--panel:#fff;--muted:#6b7280;--text:#162033;--line:#e7ebf2;--brand:#3b82f6;--brand2:#7c3aed;--ok:#10b981;--warn:#f59e0b;--err:#ef4444;--shadow:0 20px 45px rgba(30,41,59,.08);--shadow2:0 10px 24px rgba(30,41,59,.08)}
*{box-sizing:border-box}html,body{height:100%;margin:0;overflow:hidden;background:var(--bg);font-family:'Microsoft YaHei UI','Segoe UI',system-ui,sans-serif;color:var(--text)}button,input{font:inherit}.shell{height:100%;display:grid;grid-template-columns:270px 1fr;background:radial-gradient(circle at 72% -12%,rgba(59,130,246,.16),transparent 30%),radial-gradient(circle at 12% 12%,rgba(124,58,237,.10),transparent 28%),var(--bg)}
.sidebar{position:relative;padding:24px 18px;border-right:1px solid var(--line);background:rgba(255,255,255,.82);backdrop-filter:blur(18px);animation:sideIn .35s ease both}.brand{height:76px;display:flex;align-items:center;gap:12px;margin-bottom:14px;padding:0 10px}.logo{width:46px;height:46px;border-radius:16px;background:linear-gradient(135deg,var(--brand),var(--brand2));display:grid;place-items:center;color:#fff;font-weight:800;box-shadow:0 12px 24px rgba(59,130,246,.26)}.brand h1{font-size:19px;margin:0}.brand p{font-size:12px;color:var(--muted);margin:4px 0 0}.nav-title{font-size:12px;color:#8a94a6;margin:16px 10px 10px;letter-spacing:.12em;text-transform:uppercase}.nav{display:flex;flex-direction:column;gap:8px}.nav-item{border:0;background:transparent;border-radius:14px;padding:12px;color:#526070;font-size:14px;text-align:left;display:flex;justify-content:space-between;align-items:center;cursor:pointer;transition:.18s}.nav-item:hover{background:#f0f5ff;color:#1f3b68;transform:translateX(2px)}.nav-item.active{background:linear-gradient(135deg,#eaf3ff,#f4edff);color:#1e40af;font-weight:700;box-shadow:inset 0 0 0 1px rgba(59,130,246,.10)}.nav-item em{font-style:normal;font-size:12px;background:#fff;border:1px solid var(--line);border-radius:999px;padding:2px 8px;color:#687386}.side-tip{position:absolute;left:18px;right:18px;bottom:20px;padding:14px;border-radius:18px;background:#f8fafc;border:1px solid var(--line);color:#64748b;font-size:12px;line-height:1.65}.side-tip button{border:0;background:#eaf3ff;color:#1d4ed8;border-radius:999px;padding:5px 10px;margin-top:8px;cursor:pointer}.side-tip button:hover{background:#dbeafe}.main{min-width:0;overflow:auto;padding:28px 32px 40px}.hero{display:grid;grid-template-columns:1fr 380px;gap:22px;margin-bottom:24px;animation:fadeUp .35s ease both}.hero-card{position:relative;overflow:hidden;background:rgba(255,255,255,.88);border:1px solid rgba(231,235,242,.9);border-radius:26px;padding:30px;box-shadow:var(--shadow)}.hero-card:after{content:'';position:absolute;right:-70px;top:-80px;width:230px;height:230px;border-radius:50%;background:linear-gradient(135deg,rgba(59,130,246,.18),rgba(124,58,237,.15))}.kicker{display:inline-flex;background:#eef5ff;color:#1d4ed8;border:1px solid #dbeafe;border-radius:999px;padding:6px 11px;font-size:12px;font-weight:700}.hero h2{font-size:32px;line-height:1.18;margin:18px 0 12px;letter-spacing:-.04em}.hero p{max-width:720px;color:#667085;font-size:14px;line-height:1.8;margin:0}.stats{display:grid;grid-template-columns:1fr 1fr;gap:14px}.stat{background:#fff;border:1px solid var(--line);border-radius:22px;padding:20px;box-shadow:var(--shadow2);transition:.18s}.stat:hover{transform:translateY(-2px)}.stat strong{font-size:28px;display:block;margin-bottom:6px}.stat span{font-size:13px;color:var(--muted)}.toolbar{display:flex;gap:12px;align-items:center;justify-content:space-between;margin:0 0 16px;animation:fadeUp .42s ease both}.toolbar-actions{display:flex;gap:10px;align-items:center}.search{width:360px;max-width:50%;border:1px solid var(--line);background:#fff;border-radius:16px;padding:12px 14px;outline:none;font-size:14px;box-shadow:0 8px 22px rgba(30,41,59,.04)}.search:focus{border-color:#bfdbfe;box-shadow:0 0 0 4px rgba(59,130,246,.12)}.ghost-btn{border:1px solid var(--line);background:#fff;border-radius:16px;padding:12px 14px;cursor:pointer;color:#475569;box-shadow:0 8px 22px rgba(30,41,59,.04);transition:.16s}.ghost-btn:hover{transform:translateY(-1px);border-color:#cfe0ff;color:#1d4ed8}.section-title h2{font-size:20px;margin:0}.section-title p{font-size:13px;color:var(--muted);margin:4px 0 0}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(285px,1fr));gap:18px}.card{position:relative;border:1px solid var(--line);background:rgba(255,255,255,.94);border-radius:22px;text-align:left;min-height:184px;display:flex;flex-direction:column;box-shadow:0 12px 30px rgba(30,41,59,.06);transition:.18s;animation:cardIn .36s ease both;animation-delay:calc(var(--i)*18ms)}.card:hover{transform:translateY(-3px);box-shadow:0 20px 46px rgba(30,41,59,.10);border-color:#cfe0ff}.card.disabled{opacity:.52}.card.favorited{border-color:#fcd34d;box-shadow:0 16px 36px rgba(245,158,11,.12)}.card-actions{position:absolute;right:12px;top:12px;z-index:2}.star{width:32px;height:32px;border:1px solid var(--line);background:#fff;border-radius:12px;display:grid;place-items:center;cursor:pointer;color:#cbd5e1;transition:.16s}.star svg{width:17px;height:17px;fill:currentColor}.star:hover{color:#f59e0b;transform:scale(1.05)}.favorited .star{color:#f59e0b;background:#fffbeb;border-color:#fde68a}.card-run{border:0;background:transparent;padding:18px;text-align:left;cursor:pointer;min-height:182px;display:flex;flex-direction:column;justify-content:space-between;color:inherit}.card-run:disabled{cursor:not-allowed}.card-top{display:flex;gap:14px;padding-right:34px}.icon{width:46px;height:46px;border-radius:16px;background:linear-gradient(135deg,#eef5ff,#f5f3ff);display:grid;place-items:center;color:#2563eb;flex:0 0 auto}.icon svg{width:22px;height:22px}.card h3{font-size:16px;margin:2px 0 8px}.card p{font-size:13px;line-height:1.65;color:#667085;margin:0}.card-foot{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin-top:18px}.category,.command,.badge,.recent-badge{font-size:12px;border-radius:999px;padding:4px 9px}.category{background:#f1f5f9;color:#475569}.command{background:#fff7ed;color:#c2410c;border:1px solid #fed7aa}.badge{background:#ecfdf5;color:#047857;border:1px solid #bbf7d0}.recent-badge{background:#eef2ff;color:#4338ca;border:1px solid #c7d2fe}.empty{display:none;color:#64748b;text-align:center;padding:70px 0}.empty.show{display:block}.toast-stack{position:fixed;right:24px;top:22px;z-index:50;display:flex;flex-direction:column;gap:10px}.toast{min-width:230px;max-width:380px;background:#fff;border:1px solid var(--line);border-left:4px solid var(--brand);border-radius:16px;padding:12px 14px;box-shadow:var(--shadow2);font-size:13px;color:#334155;animation:toastIn .2s ease both}.toast.success{border-left-color:var(--ok)}.toast.warning{border-left-color:var(--warn)}.toast.error{border-left-color:var(--err)}.cmd-overlay{position:fixed;inset:0;z-index:40;background:rgba(15,23,42,.22);backdrop-filter:blur(5px);display:none;align-items:flex-start;justify-content:center;padding-top:76px}.cmd-overlay.show{display:flex;animation:fadeIn .16s ease both}.cmd-panel{width:min(720px,calc(100% - 42px));max-height:76vh;background:#fff;border:1px solid var(--line);border-radius:24px;box-shadow:0 30px 80px rgba(15,23,42,.22);overflow:hidden;animation:panelIn .2s ease both}.cmd-head{display:flex;align-items:center;gap:12px;padding:16px;border-bottom:1px solid var(--line)}.cmd-head input{flex:1;border:0;outline:0;font-size:16px}.cmd-key{font-size:12px;color:#64748b;background:#f8fafc;border:1px solid var(--line);border-radius:9px;padding:4px 8px}.cmd-list{max-height:58vh;overflow:auto;padding:10px}.cmd-row{width:100%;border:0;background:transparent;border-radius:16px;padding:12px;text-align:left;display:flex;align-items:center;justify-content:space-between;gap:18px;cursor:pointer;color:#334155}.cmd-row:hover,.cmd-row.active{background:#f1f5ff}.cmd-row.disabled{opacity:.42;cursor:not-allowed}.cmd-row strong{display:block;font-size:14px;color:#162033}.cmd-row em{display:block;font-style:normal;font-size:12px;color:#64748b;margin-top:4px;line-height:1.45}.cmd-row kbd{font-size:12px;color:#c2410c;background:#fff7ed;border:1px solid #fed7aa;border-radius:999px;padding:4px 8px}.cmd-empty{display:none;padding:34px;text-align:center;color:#64748b}.cmd-empty.show{display:block}@keyframes fadeIn{from{opacity:0}to{opacity:1}}@keyframes sideIn{from{opacity:0;transform:translateX(-10px)}to{opacity:1;transform:none}}@keyframes fadeUp{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:none}}@keyframes cardIn{from{opacity:0;transform:translateY(12px) scale(.985)}to{opacity:1;transform:none}}@keyframes toastIn{from{opacity:0;transform:translateX(12px)}to{opacity:1;transform:none}}@keyframes panelIn{from{opacity:0;transform:translateY(-10px) scale(.985)}to{opacity:1;transform:none}}@media(max-width:1040px){.shell{grid-template-columns:230px 1fr}.hero{grid-template-columns:1fr}.search{max-width:none;width:100%}.toolbar{align-items:flex-start;flex-direction:column}.toolbar-actions{width:100%;align-items:stretch}.ghost-btn{white-space:nowrap}}
</style>
</head>
<body>
<div class=""shell"">
  <aside class=""sidebar"">
    <div class=""brand""><div class=""logo"">CD</div><div><h1>CDBox Studio</h1><p>WebView2 实验工作台</p></div></div>
    <div class=""nav-title"">Navigation</div><nav class=""nav"">{{NAV}}</nav>
    <div class=""side-tip"">Ctrl+K 可打开命令面板。Studio 不替换旧窗口，所有业务功能仍调用原命令或原窗口。<br><button id=""openLogsSide"">打开日志目录</button></div>
  </aside>
  <main class=""main"">
    <section class=""hero"">
      <div class=""hero-card""><span class=""kicker"">CDBox v2.2.2 Stable · Studio Preview</span><h2>把稳定功能装进一个现代工作台。</h2><p>本版补齐工作台基础能力：统一命令路由、Toast、最近使用、收藏、Ctrl+K 命令面板、动画、WebView2 Runtime 检测与 Studio 日志。旧功能逻辑保持原样。</p></div>
      <div class=""stats""><div class=""stat""><strong>{{TOTAL}}</strong><span>已接入卡片</span></div><div class=""stat""><strong>{{FAVORITES}}</strong><span>收藏功能</span></div><div class=""stat""><strong>{{RECENT}}</strong><span>最近使用</span></div><div class=""stat""><strong>{{ENABLED}}</strong><span>可直接调用</span></div></div>
    </section>
    <section class=""toolbar""><div class=""section-title""><h2 id=""sectionTitle"">全部功能</h2><p>按分类、收藏、最近使用筛选，也可以搜索功能名称、说明或命令。</p></div><div class=""toolbar-actions""><input id=""search"" class=""search"" placeholder=""搜索：表面积、GCL、断面、属性..."" /><button id=""cmdButton"" class=""ghost-btn"">Ctrl+K 命令面板</button></div></section>
    <section id=""grid"" class=""grid"">{{CARDS}}</section><div id=""empty"" class=""empty"">没有匹配的功能卡片。</div>
    <p style=""margin:22px 0 0;color:#94a3b8;font-size:12px"">Runtime：{{RUNTIME}}　日志：{{LOG_PATH}}</p>
  </main>
</div>
<div id=""toastStack"" class=""toast-stack""></div>
<div id=""cmdOverlay"" class=""cmd-overlay"" aria-hidden=""true""><div class=""cmd-panel""><div class=""cmd-head""><span>⌘</span><input id=""cmdInput"" placeholder=""输入功能名称或命令，例如 GCL、断面、标注..."" /><span class=""cmd-key"">Esc</span></div><div id=""cmdList"" class=""cmd-list"">{{COMMAND_ROWS}}<div id=""cmdEmpty"" class=""cmd-empty"">没有匹配的命令。</div></div></div></div>
<script>
(function(){
  var active='总览';
  var search=document.getElementById('search');
  var title=document.getElementById('sectionTitle');
  var empty=document.getElementById('empty');
  var cards=[].slice.call(document.querySelectorAll('[data-card]'));
  var overlay=document.getElementById('cmdOverlay');
  var cmdInput=document.getElementById('cmdInput');
  var cmdRows=[].slice.call(document.querySelectorAll('[data-command-row]'));
  var cmdEmpty=document.getElementById('cmdEmpty');
  var selectedIndex=0;

  function post(name,arg){
    if(window.chrome&&chrome.webview){chrome.webview.postMessage('studio|'+name+'|'+encodeURIComponent(arg||''));}
  }
  function toast(message,kind){
    var stack=document.getElementById('toastStack');
    var node=document.createElement('div');
    node.className='toast '+(kind||'info');
    node.textContent=message||'';
    stack.appendChild(node);
    setTimeout(function(){node.style.opacity='0';node.style.transform='translateX(10px)';},2600);
    setTimeout(function(){if(node.parentNode)node.parentNode.removeChild(node);},3100);
  }
  window.CDBoxStudioToast=toast;

  function apply(){
    var q=(search.value||'').trim().toLowerCase();
    var visible=0;
    cards.forEach(function(card){
      var cat=card.getAttribute('data-category')||'';
      var text=card.innerText.toLowerCase()+' '+(card.getAttribute('data-command')||'').toLowerCase();
      var byCat=active==='总览'||cat===active||(active==='收藏'&&card.getAttribute('data-favorite')==='1')||(active==='最近使用'&&card.getAttribute('data-recent')==='1');
      var byText=!q||text.indexOf(q)>=0;
      var show=byCat&&byText;
      card.style.display=show?'flex':'none';
      if(show) visible++;
    });
    title.textContent=active==='总览'?'全部功能':active;
    empty.className=visible?'empty':'empty show';
    post('filter',title.textContent);
  }
  function runId(id){
    if(!id)return;
    if(id==='__openLogs'){post('openLogs','');return;}
    post('run',id);
  }
  function visibleCommandRows(){return cmdRows.filter(function(row){return row.style.display!=='none'&&!row.disabled;});}
  function updateSelection(delta){
    var rows=visibleCommandRows();
    if(!rows.length)return;
    selectedIndex=(selectedIndex+delta+rows.length)%rows.length;
    cmdRows.forEach(function(r){r.classList.remove('active');});
    rows[selectedIndex].classList.add('active');
    rows[selectedIndex].scrollIntoView({block:'nearest'});
  }
  function filterCommands(){
    var q=(cmdInput.value||'').trim().toLowerCase();
    var count=0;
    cmdRows.forEach(function(row){
      var hit=!q||(row.getAttribute('data-search')||row.innerText.toLowerCase()).indexOf(q)>=0;
      row.style.display=hit?'flex':'none';
      if(hit&&!row.disabled)count++;
      row.classList.remove('active');
    });
    selectedIndex=0;
    var rows=visibleCommandRows();
    if(rows.length)rows[0].classList.add('active');
    cmdEmpty.className=count?'cmd-empty':'cmd-empty show';
  }
  function openCommandPanel(){
    overlay.classList.add('show');
    overlay.setAttribute('aria-hidden','false');
    cmdInput.value='';
    filterCommands();
    setTimeout(function(){cmdInput.focus();},20);
  }
  function closeCommandPanel(){
    overlay.classList.remove('show');
    overlay.setAttribute('aria-hidden','true');
  }

  document.querySelectorAll('[data-filter]').forEach(function(btn){
    btn.addEventListener('click',function(){
      document.querySelectorAll('[data-filter]').forEach(function(x){x.classList.remove('active');});
      btn.classList.add('active');
      active=btn.getAttribute('data-filter')||'总览';
      apply();
    });
  });
  cards.forEach(function(card){
    var run=card.querySelector('[data-run-button]');
    var fav=card.querySelector('[data-favorite-button]');
    if(run){run.addEventListener('click',function(){if(run.disabled)return;runId(card.getAttribute('data-id'));});}
    if(fav){fav.addEventListener('click',function(ev){ev.stopPropagation();post('favorite',card.getAttribute('data-id'));});}
  });
  cmdRows.forEach(function(row){row.addEventListener('click',function(){if(row.disabled)return;runId(row.getAttribute('data-id'));closeCommandPanel();});});
  search.addEventListener('input',apply);
  cmdInput.addEventListener('input',filterCommands);
  document.getElementById('cmdButton').addEventListener('click',openCommandPanel);
  document.getElementById('openLogsSide').addEventListener('click',function(){post('openLogs','');});
  overlay.addEventListener('click',function(ev){if(ev.target===overlay)closeCommandPanel();});
  document.addEventListener('keydown',function(ev){
    if((ev.ctrlKey||ev.metaKey)&&ev.key.toLowerCase()==='k'){ev.preventDefault();openCommandPanel();return;}
    if(!overlay.classList.contains('show'))return;
    if(ev.key==='Escape'){closeCommandPanel();return;}
    if(ev.key==='ArrowDown'){ev.preventDefault();updateSelection(1);return;}
    if(ev.key==='ArrowUp'){ev.preventDefault();updateSelection(-1);return;}
    if(ev.key==='Enter'){
      var rows=visibleCommandRows();
      if(rows[selectedIndex]){runId(rows[selectedIndex].getAttribute('data-id'));closeCommandPanel();}
    }
  });
  apply();
  filterCommands();
  setTimeout(function(){post('ready','');},60);
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
            return "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M4 7h16\"/><path d=\"M4 12h16\"/><path d=\"M4 17h10\"/></svg>";
        }
    }
}

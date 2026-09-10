using System;
using System.Web;

namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>独立设置页和旧宿主页共用同一套更新展示。</summary>
    internal static class CDBoxReleaseUpdatePage
    {
        public static string BuildControls(CDBoxStudioSettings settings)
        {
            return "<h3>版本检查</h3><p>插件只检查版本并引导前往发布网站，不下载或安装更新。</p>"
                + "<div class='release-fields' style='display:grid;gap:10px'>"
                + "<label>更新通道 <select id='releaseChannel' aria-label='更新通道'>"
                + "<option value='stable'" + (settings.UpdateChannel == "stable" ? " selected" : "") + ">Stable（正式版）</option>"
                + "<option value='preview'" + (settings.UpdateChannel == "preview" ? " selected" : "") + ">Preview（预览版）</option></select></label>"
                + "</div>"
                + "<div class='actions update-actions' style='margin-top:12px'><button id='checkUpdateButton' class='primary primary-btn'>检查更新</button>"
                + "<button id='openReleaseWebsiteButton' class='ghost-btn'>前往发布网站</button></div>"
                + "<div id='releaseProgress' role='status' style='margin-top:12px'></div>"
                + "<div id='releaseResult' class='result update-result' style='white-space:pre-wrap'>尚未检查更新</div>";
        }

        public static string BuildScript()
        {
            return @"
window.CDBoxReleaseUpdates={init:function(post,payload){
  var channel=document.getElementById('releaseChannel');
  var button=document.getElementById('checkUpdateButton'),box=document.getElementById('releaseResult'),progress=document.getElementById('releaseProgress'),pending=null;
  function key(){return channel.value;}
  function changed(){box.textContent='设置已更改，请重新检查更新';post('settings',payload());}
  channel.addEventListener('change',changed);
  document.getElementById('openReleaseWebsiteButton').onclick=function(){post('openReleaseWebsite','');};
  button.onclick=function(){pending=key();button.disabled=true;progress.textContent='正在查询版本服务';post('checkUpdate',payload());};
  window.CDBoxStudioUpdateProgress=function(d){progress.textContent=(d||{}).message||'正在查询版本服务';};
  window.CDBoxStudioUpdateResult=function(d){
    d=d||{};button.disabled=false;progress.textContent='';
    if(pending!==null&&pending!==key()){pending=null;box.textContent='设置已更改，请重新检查更新';return;}
    pending=null;
    if(!d.success){box.textContent='检查更新失败：'+(d.errorMessage||'请稍后重试')+'\n可直接前往发布网站查看最新版本。';return;}
    var text=!d.hasRelease?'该通道暂无已发布版本':(d.updateAvailable?'发现新版本：'+d.latestVersion:'当前版本无需更新');
    box.textContent=text+'\n所选通道：'+d.channel+'\n当前版本：'+d.currentVersion
      +(d.hasRelease?'\n服务最新版本：'+d.latestVersion:'')+(d.title?'\n'+d.title:'')
      +(d.releaseDate?'\n发布时间：'+d.releaseDate:'')+(d.summary?'\n\n更新摘要：\n'+d.summary:'')
      +(d.notes?'\n\n更新日志：\n'+d.notes:'')+'\n检查时间：'+d.checkedAt;
    if(d.updateAvailable&&window.confirm('发现 CDBox 新版本 '+d.latestVersion+'，是否前往发布网站查看并下载？'))
      post('openReleaseWebsite','');
  };
}};
";
        }
    }
}

param(
    [string]$BuildDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\parcel-workbench\build'),
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\settings-workbench\preview')
)
$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.Shared.dll'))
$core = [Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.dll'))
$type = $core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioSettings', $true)
$settings = [Activator]::CreateInstance($type, $true)
$page = $core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioSettingsPage', $true)
$html = $page.GetMethod('BuildDocument', [Reflection.BindingFlags]'Static,NonPublic').Invoke($null, @($settings,'基础组件 · 通用 · 污水 · 不动产','',[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $BuildDirectory 'CDBox.dll')).ProductVersion))
$bridge = @'
<script>
window.previewMessages=[];window.previewErrors=[];window.previewSettingsSaved=null;window.previewFail=false;window.previewDelay=20;
window.addEventListener('error',e=>window.previewErrors.push(e.message));
window.chrome=window.chrome||{};window.chrome.webview={postMessage:function(raw){window.previewMessages.push(raw);const bits=raw.split('|'),name=bits[1],arg=decodeURIComponent(bits[2]||'');if(name==='settings'){const failed=window.previewFail;setTimeout(()=>{if(!failed)window.previewSettingsSaved=Object.fromEntries(new URLSearchParams(arg));window.CDBoxSettingsSaved(!failed);},window.previewDelay);}if(name==='copyTheme')window.previewCopiedTheme=arg;}};
</script>
'@
$html = $html.Replace('<script>window.CDBoxSettingsContext=', $bridge + '<script>window.CDBoxSettingsContext=')
[void][IO.Directory]::CreateDirectory($OutputDirectory)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'settings.html'),$html,(New-Object Text.UTF8Encoding($false)))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SettingsWorkbenchPreview.html') -Destination (Join-Path $OutputDirectory 'index.html')
Write-Output "Settings preview: $OutputDirectory"

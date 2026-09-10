param([string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$violations = [Collections.Generic.List[string]]::new()
$uiPattern = 'System\.Windows\.(Forms|Controls|Media)|Microsoft\.Web\.WebView2|:\s*(Window|Form|UserControl|Control)\b|<(?:!doctype|html\b|style\b|script\b|div\b|button\b|textarea\b)|new\s+CDBoxPageDefinition\b'
foreach ($module in @('CDBox.Common', 'CDBox.Wastewater', 'CDBox.RealEstate')) {
    $directory = Join-Path $RepositoryRoot $module
    [xml]$project = Get-Content -LiteralPath (Join-Path $directory "$module.csproj") -Raw
    $files = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    Get-ChildItem -LiteralPath $directory -Recurse -File -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        ForEach-Object { [void]$files.Add($_.FullName) }
    foreach ($include in $project.SelectNodes('//Compile[@Include]')) {
        $path = [IO.Path]::GetFullPath((Join-Path $directory $include.Include))
        Get-ChildItem -Path $path -File | ForEach-Object { [void]$files.Add($_.FullName) }
    }
    foreach ($file in $files) {
        $source = [IO.File]::ReadAllText($file)
        if ($source -match $uiPattern) { $violations.Add("$module 含 UI 实现：$file") }
    }
    foreach ($reference in $project.SelectNodes('//Reference|//PackageReference')) {
        if ($reference.Include -match '^(System\.Windows\.Forms|PresentationCore|PresentationFramework|System\.Xaml|Microsoft\.Web\.WebView2)') {
            $violations.Add("$module 引用了 UI 实现程序集：$($reference.Include)")
        }
    }
    if ($project.SelectNodes('//UseWindowsForms[text()="true"]|//UseWPF[text()="true"]').Count -gt 0) {
        $violations.Add("$module 启用了独立 UI 构建")
    }
    $assets = Get-ChildItem -LiteralPath $directory -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' -and $_.Extension -in @('.html','.css','.js','.xaml') }
    foreach ($asset in $assets) { $violations.Add("$module 含独立 UI 资源：$($asset.FullName)") }
    Write-Output "$module : 已检查 $($files.Count) 个业务源文件及实际链接源文件"
}
if ($violations.Count -gt 0) { throw ($violations -join [Environment]::NewLine) }
Write-Output 'PASS: 业务模块无页面、原生控件、独立样式资源或 UI 框架依赖。'

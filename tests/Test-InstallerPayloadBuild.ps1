param(
    [Parameter(Mandatory=$true)][string]$InstallerPath,
    [Parameter(Mandatory=$true)][string]$BuildDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$installer = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($InstallerPath))
$stream = $installer.GetManifestResourceStream('CDBoxInstaller.Payload.zip')
if ($null -eq $stream) { throw 'Missing embedded installer payload.' }
$zip = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Read)
try {
    foreach ($file in @('CDBox.dll','CDBox.Shared.dll','CDBox.Common.dll','CDBox.Wastewater.dll','CDBox.RealEstate.dll','Templates/地籍调查表.docx','Templates/房产.docx','Templates/权籍调查表.xls','Templates/四张检查表.xls')) {
        $entry = $zip.GetEntry('CDBox.bundle/Contents/' + $file)
        if ($null -eq $entry) { throw "Missing payload entry: $file" }
        $entryStream = $entry.Open()
        try { $embeddedHash = (Get-FileHash -InputStream $entryStream -Algorithm SHA256).Hash }
        finally { $entryStream.Dispose() }
        $builtHash = (Get-FileHash -LiteralPath (Join-Path $BuildDirectory $file) -Algorithm SHA256).Hash
        if ($embeddedHash -ne $builtHash) { throw "Payload differs from verified build: $file" }
        Write-Output "PASS: $file matches verified build."
    }
    $reader = New-Object IO.StreamReader($zip.GetEntry('CDBox.bundle/PackageContents.xml').Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($InstallerPath).ProductVersion
    if ($manifest.ApplicationPackage.AppVersion -ne $version) { throw 'Installer and bundle versions differ.' }
    Write-Output "PASS: Installer and bundle version $version; $($zip.Entries.Count) embedded files."
}
finally { $zip.Dispose(); $stream.Dispose() }

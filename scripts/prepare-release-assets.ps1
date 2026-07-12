param(
    [string]$Version = "",
    [string]$ReleaseNotes = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$SharedDirectoryName = -join ([char[]](0x5171, 0x4EAB, 0x7A7A, 0x95F4))
$SharedScript = (Resolve-Path (Join-Path (Join-Path (Split-Path -Parent $ProjectRoot) $SharedDirectoryName) "DesktopUpdateKit\tools\Prepare-ReleaseAssets.ps1")).Path
$ConfigPath = Join-Path $ProjectRoot "release.config.json"

& $SharedScript -ProjectRoot $ProjectRoot -ConfigPath $ConfigPath -Version $Version -ReleaseNotes $ReleaseNotes
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

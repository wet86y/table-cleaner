$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$SharedRoot = Join-Path $ProjectRoot "shared\DesktopUpdateKit"
$SharedScriptPath = Join-Path $SharedRoot "tools\Build-Release.ps1"
if (-not (Test-Path -LiteralPath $SharedScriptPath)) {
    throw "DesktopUpdateKit submodule is missing. Run: git submodule update --init --recursive"
}
$SharedScript = (Resolve-Path -LiteralPath $SharedScriptPath).Path
$ConfigPath = Join-Path $ProjectRoot "release.config.json"
$Config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
$ProjectPath = Join-Path $ProjectRoot $Config.projectFile

# A normal Release build does not include UpdaterStub. Clear its incremental
# outputs so publish cannot reuse that assembly after UpdaterStubPath is added.
dotnet clean $ProjectPath -c Release
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $SharedScript -ProjectRoot $ProjectRoot -ConfigPath $ConfigPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& (Join-Path $PSScriptRoot "test-release-executable.ps1")

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $Root "src\TableCleaner"
$ArtifactsDir = Join-Path $Root "artifacts\表格工具-win-x64"

Set-Location $ProjectDir

Write-Host "Restoring packages..."
dotnet restore
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

Write-Host "Publishing self-contained release..."
dotnet publish -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $ArtifactsDir `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

$exe = Join-Path $ArtifactsDir "表格工具.exe"
if (Test-Path $exe) {
    $size = (Get-Item $exe).Length / 1MB
    Write-Host "Done: $exe ({0:F1} MB)" -f $size
} else {
    throw "Published exe not found: $exe"
}

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $Root "src\TableCleaner"
$Project = Join-Path $ProjectDir "TableCleaner.csproj"
$SharedToolkitRoot = Join-Path $Root "shared\DesktopUpdateKit"
if (-not (Test-Path -LiteralPath (Join-Path $SharedToolkitRoot "src\DesktopUpdateKit\UpdateClient.cs"))) {
    throw "DesktopUpdateKit submodule is missing. Run: git submodule update --init --recursive"
}

Write-Host "Building diagnostic configuration..."
dotnet build $Project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$ExecutableName = (-join ([char[]](0x7B28, 0x86CB, 0x8868, 0x683C))) + ".exe"
$exe = Join-Path $Root (Join-Path "build\bin\Release" $ExecutableName)
Write-Host ""
if (Test-Path $exe) {
    Write-Host "PASS: Build artifact found at $exe"
} else {
    throw "FAIL: Build artifact not found"
}

Write-Host "PASS: Self-check passed."

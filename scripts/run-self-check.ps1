$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $Root "src\TableCleaner"
$Project = Join-Path $ProjectDir "TableCleaner.csproj"

Write-Host "Building diagnostic configuration..."
dotnet build $Project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$exe = Join-Path $Root "build\bin\Release\笨蛋表格.exe"
Write-Host ""
if (Test-Path $exe) {
    Write-Host "PASS: Build artifact found at $exe"
} else {
    throw "FAIL: Build artifact not found"
}

Write-Host "PASS: Self-check passed."

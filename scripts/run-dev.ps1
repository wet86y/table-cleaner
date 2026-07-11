$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $Root "src\TableCleaner"
Set-Location $ProjectDir

dotnet restore
dotnet run --project (Join-Path $ProjectDir "TableCleaner.csproj") -c Release

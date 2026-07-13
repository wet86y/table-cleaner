param(
    [string]$ExecutablePath = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $Config = Get-Content -LiteralPath (Join-Path $ProjectRoot "release.config.json") -Raw | ConvertFrom-Json
    $ExecutablePath = Join-Path (Join-Path $ProjectRoot $Config.artifactsDirectory) $Config.publishedExeName
}

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "Release executable not found: $ExecutablePath"
}

foreach ($argument in @("--verify-release", "--verify-ui-layout")) {
    $Process = Start-Process -FilePath $ExecutablePath `
        -ArgumentList $argument `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    if ($Process.ExitCode -ne 0) {
        throw "Release executable self-check '$argument' failed with exit code $($Process.ExitCode)."
    }
}

Write-Host "Release executable self-check passed: updater resource and update layouts are valid."

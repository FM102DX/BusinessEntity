param(
    [switch]$RestartApp,
    [switch]$SkipHealthWait
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Split-Path -Parent $ScriptDir
$EnvPath = Join-Path $Root ".env"

function Read-DotEnv {
    param([Parameter(Mandatory = $true)][string]$Path)
    $map = @{}
    if (-not (Test-Path $Path)) {
        return $map
    }

    foreach ($line in Get-Content -Path $Path) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $trimmed = $line.Trim()
        if ($trimmed.StartsWith("#")) {
            continue
        }

        $idx = $trimmed.IndexOf("=")
        if ($idx -lt 1) {
            continue
        }

        $map[$trimmed.Substring(0, $idx).Trim()] = $trimmed.Substring($idx + 1).Trim()
    }

    return $map
}

function Invoke-Compose {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    Push-Location $Root
    try {
        & docker @("compose", "--env-file", ".env", "-f", "docker-compose.yml") @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose command failed: $($Arguments -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Wait-HttpOk {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [int]$TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                Write-Host "Application is reachable: $Url" -ForegroundColor Green
                return
            }
        }
        catch {
            Start-Sleep -Seconds 3
        }
    }

    Write-Host "Application health wait timed out: $Url" -ForegroundColor Yellow
}

if ($RestartApp) {
    Invoke-Compose -Arguments @("restart", "business-entity")
}

if (-not $SkipHealthWait) {
    $envMap = Read-DotEnv -Path $EnvPath
    $port = if ($envMap.ContainsKey("BUSINESSENTITY_HTTP_PORT")) { $envMap["BUSINESSENTITY_HTTP_PORT"] } else { "7000" }
    Wait-HttpOk -Url "http://localhost:$port" -TimeoutSeconds 180
}

Write-Host "Initial data bootstrap is application-owned in this release." -ForegroundColor Green
Write-Host "The application startup creates/repairs schema, system users, system roles, seed owner metadata, and current startup seed data." -ForegroundColor Cyan
Write-Host "A future InstallationBootstrapService should replace demo seed with Minimal/Demo modes and write InstallationBootstrapState." -ForegroundColor Yellow

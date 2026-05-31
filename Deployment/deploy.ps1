param(
    [ValidateSet("install", "start", "stop", "restart", "status", "logs", "doctor")]
    [string]$Command = "status",
    [string]$Service = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$InstallScript = Join-Path $Root "install.ps1"
$ComposeArgs = @("compose", "--env-file", ".env", "-f", "docker-compose.yml")

function Invoke-RequiredTool {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $FilePath $($Arguments -join ' ')"
    }
}

function Invoke-Compose {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    Push-Location $Root
    try {
        Invoke-RequiredTool -FilePath "docker" -Arguments ($ComposeArgs + $Arguments)
    }
    finally {
        Pop-Location
    }
}

switch ($Command) {
    "install" {
        & $InstallScript
    }
    "start" {
        & $InstallScript -SkipImageLoad -SkipHealthWait
    }
    "stop" {
        Invoke-Compose -Arguments @("down", "--remove-orphans")
    }
    "restart" {
        Invoke-Compose -Arguments @("restart")
    }
    "status" {
        Invoke-Compose -Arguments @("ps")
    }
    "logs" {
        if ([string]::IsNullOrWhiteSpace($Service)) {
            Invoke-Compose -Arguments @("logs", "--tail", "200")
        }
        else {
            Invoke-Compose -Arguments @("logs", "--tail", "200", $Service)
        }
    }
    "doctor" {
        Invoke-Compose -Arguments @("config")
        Invoke-Compose -Arguments @("ps")
    }
}

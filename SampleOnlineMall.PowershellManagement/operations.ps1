<# 
Docker Compose Management Script for Sample Online Mall
This script provides a menu-driven interface to manage the docker-compose environment
Created for SampleOnlineMall project
#>

# Get the script directory and navigate to the repository root
[string] $scriptDir = $PSScriptRoot
[string] $repoRoot = Split-Path $scriptDir -Parent
[bool] $canExit = $false

function Show-Menu() {
    Clear-Host
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   Sample Online Mall - Docker Manager" -ForegroundColor Cyan  
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Available Actions:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "10 - Launch environment from docker compose" -ForegroundColor Green
    Write-Host ""
    Write-Host "99 - Exit" -ForegroundColor Red
    Write-Host ""
}

function Start-DockerComposeEnvironment() {
    Write-Host ""
    Write-Host "Starting Docker Compose environment..." -ForegroundColor Yellow
    Write-Host "Repository root: $repoRoot" -ForegroundColor Gray
    
    try {
        # Change to repository root directory
        Set-Location $repoRoot
        
        # Check if docker-compose.yml exists
        if (-not (Test-Path "docker-compose.yml")) {
            Write-Host "Error: docker-compose.yml not found in $repoRoot" -ForegroundColor Red
            return
        }
        
        Write-Host "Found docker-compose.yml, starting services..." -ForegroundColor Green
        
        # Start docker compose
        docker compose up -d
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "✅ Docker Compose environment started successfully!" -ForegroundColor Green
            Write-Host ""
            Write-Host "Services are now running:" -ForegroundColor Cyan
            Write-Host "  - PostgreSQL Database: localhost:5432" -ForegroundColor White
            Write-Host "  - WebLogger Service: http://localhost:7000" -ForegroundColor White  
            Write-Host "  - AssortmentApi Service: http://localhost:8000" -ForegroundColor White
            Write-Host "  - Blazor Frontend: http://localhost:3000" -ForegroundColor White
            Write-Host ""
            Write-Host "To check status: docker compose ps" -ForegroundColor Gray
            Write-Host "To view logs: docker compose logs -f" -ForegroundColor Gray
            Write-Host "To stop services: docker compose down" -ForegroundColor Gray
        } else {
            Write-Host ""
            Write-Host "❌ Failed to start Docker Compose environment" -ForegroundColor Red
            Write-Host "Please check Docker is running and try again." -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host ""
        Write-Host "❌ Error occurred: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Write-Host ""
    Read-Host "Press Enter to continue..."
}

function Execute-MenuAction([string] $action) {
    switch ($action) {
        "10" {
            Start-DockerComposeEnvironment
        }
        "99" {
            Write-Host ""
            Write-Host "Exiting..." -ForegroundColor Yellow
            $script:canExit = $true
        }
        default {
            Write-Host ""
            Write-Host "❌ Invalid option: $action" -ForegroundColor Red
            Write-Host "Please enter a valid menu number." -ForegroundColor Yellow
            Write-Host ""
            Read-Host "Press Enter to continue..."
        }
    }
}

# Main execution loop
Write-Host "Docker Compose Manager for Sample Online Mall" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

do {
    Show-Menu
    $userInput = Read-Host "Please enter action number"
    Execute-MenuAction -action $userInput.Trim()
} while ($script:canExit -eq $false)

Write-Host "Goodbye!" -ForegroundColor Green
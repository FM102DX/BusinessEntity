# Network Connectivity Test Script
# Проверка сетевого соединения с серверами

param(
    [string[]]$Targets = @("google.com", "8.8.8.8", "microsoft.com"),
    [int]$Count = 4,
    [int]$Timeout = 5000,
    [switch]$Continuous,
    [switch]$TestDockerContainers
)

function Test-NetworkConnectivity {
    param(
        [string]$Target,
        [int]$Count,
        [int]$Timeout
    )
    
    Write-Host "Проверка соединения с $Target..." -ForegroundColor Yellow
    
    try {
        if ($Target -match '^\d+\.\d+\.\d+\.\d+$') {
            # IP адрес
            $result = Test-Connection -ComputerName $Target -Count $Count -TimeoutSeconds ($Timeout/1000) -ErrorAction Stop
        } else {
            # Доменное имя
            $result = Test-Connection -ComputerName $Target -Count $Count -TimeoutSeconds ($Timeout/1000) -ErrorAction Stop
        }
        
        $successCount = ($result | Where-Object { $_.Status -eq 'Success' }).Count
        $avgTime = ($result | Where-Object { $_.Status -eq 'Success' } | Measure-Object -Property ResponseTime -Average).Average
        
        Write-Host "✓ $Target: $successCount/$Count пакетов получено" -ForegroundColor Green
        if ($avgTime) {
            Write-Host "  Среднее время отклика: $([math]::Round($avgTime, 2)) мс" -ForegroundColor Green
        }
        
        return $true
    }
    catch {
        Write-Host "✗ $Target: Недоступен - $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Test-DockerContainerConnectivity {
    Write-Host "`nПроверка Docker контейнеров..." -ForegroundColor Cyan
    
    try {
        $containers = docker ps --format "table {{.Names}}\t{{.Ports}}" | Select-Object -Skip 1
        
        if ($containers) {
            foreach ($container in $containers) {
                $parts = $container -split '\s+'
                $name = $parts[0]
                $ports = $parts[1]
                
                Write-Host "Контейнер: $name" -ForegroundColor White
                
                if ($ports -and $ports -ne "") {
                    # Извлекаем порты из строки формата "0.0.0.0:5440->5432/tcp"
                    $portMatches = [regex]::Matches($ports, '(?:0\.0\.0\.0:)?(\d+)')
                    
                    foreach ($match in $portMatches) {
                        $port = $match.Groups[1].Value
                        $result = Test-NetConnection -ComputerName "localhost" -Port $port -WarningAction SilentlyContinue
                        
                        if ($result.TcpTestSucceeded) {
                            Write-Host "  ✓ Порт $port: Доступен" -ForegroundColor Green
                        } else {
                            Write-Host "  ✗ Порт $port: Недоступен" -ForegroundColor Red
                        }
                    }
                } else {
                    Write-Host "  - Внешние порты не найдены" -ForegroundColor Yellow
                }
            }
        } else {
            Write-Host "Запущенные контейнеры не найдены" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "Ошибка при проверке Docker контейнеров: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Show-NetworkInfo {
    Write-Host "`nИнформация о сети:" -ForegroundColor Cyan
    
    # Получаем активные сетевые адаптеры
    $adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' }
    
    foreach ($adapter in $adapters) {
        Write-Host "Адаптер: $($adapter.Name)" -ForegroundColor White
        
        $ipConfig = Get-NetIPAddress -InterfaceIndex $adapter.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue
        
        if ($ipConfig) {
            Write-Host "  IP: $($ipConfig.IPAddress)" -ForegroundColor Green
            Write-Host "  Префикс: $($ipConfig.PrefixLength)" -ForegroundColor Green
        }
    }
    
    # Показываем шлюз по умолчанию
    $gateway = Get-NetRoute -DestinationPrefix "0.0.0.0/0" | Select-Object -First 1
    if ($gateway) {
        Write-Host "Шлюз по умолчанию: $($gateway.NextHop)" -ForegroundColor Green
    }
}

# Основная логика
Write-Host "=== Тест сетевого соединения ===" -ForegroundColor Magenta
Write-Host "Дата: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray

Show-NetworkInfo

Write-Host "`nПроверка внешних серверов:" -ForegroundColor Cyan

do {
    $allSuccess = $true
    
    foreach ($target in $Targets) {
        $success = Test-NetworkConnectivity -Target $target -Count $Count -Timeout $Timeout
        if (-not $success) {
            $allSuccess = $false
        }
        Start-Sleep -Milliseconds 500
    }
    
    if ($TestDockerContainers) {
        Test-DockerContainerConnectivity
    }
    
    if ($allSuccess) {
        Write-Host "`n✓ Все проверки пройдены успешно!" -ForegroundColor Green
    } else {
        Write-Host "`n⚠ Некоторые соединения недоступны" -ForegroundColor Yellow
    }
    
    if ($Continuous) {
        Write-Host "`nНажмите Ctrl+C для остановки или любую клавишу для повтора..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        Clear-Host
    }
    
} while ($Continuous)

Write-Host "`nПроверка завершена." -ForegroundColor Magenta
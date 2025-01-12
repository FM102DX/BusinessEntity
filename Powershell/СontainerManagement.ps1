# Управление Docker-контейнерами
# Список контейнеров
$containers = @(
    [PSCustomObject]@{
        Name = "admin-node"
        PortInt = 80
        PortExt = 5020
        ProjectPath = "C:\Develop\Mall2\ControlNode"
        ContextPath = "C:\Develop\Mall2"
        LogPath = "C:\Develop\Logs\"
    },  
[PSCustomObject]@{
        Name = "assort-api-container"
        PortInt = 80
        PortExt = 5010
        ProjectPath = "C:\Develop\Mall2\SampleOnlineMall.AssortmentApi"
        ContextPath = "C:\Develop\Mall2"
        LogPath = "C:\Develop\Logs\"
    },    
    [PSCustomObject]@{
        Name = "web_logger-container"
        PortInt = 80
        PortExt = 5080
        ProjectPath = "C:\Develop\Mall2\BlazorServerWebLogger"
        ContextPath = "C:\Develop\Mall2"
        LogPath = "C:\Develop\Logs\"
    },
    [PSCustomObject]@{
        Name = "postgres-logger-db"
        PortInt = 5432
        PortExt = 5440
        ProjectPath = "C:\Develop\Mall2\PostgresLogger"
        ContextPath = "C:\Develop\Mall2"
        LogPath = "C:\Develop\Logs\PostgresLogger"
    },
    [PSCustomObject]@{
        Name = "postgres-assort-db"
        PortInt = 5432
        PortExt = 5432
        ProjectPath = "C:\Develop\Mall2\PostgresAssort"
        ContextPath = "C:\Develop\Mall2"
        LogPath = "C:\Develop\Logs\PostgresAssort"
    }
)
function Select-Container {
    Write-Host "Выберите контейнер:" -ForegroundColor Cyan
    $i = 1
    foreach ($container in $containers) {
        Write-Host "$i. $($container.Name) (Port: $($container.PortExt))"
        $i++
    }
    $choice = Read-Host "Введите номер контейнера"
    return $containers[$choice - 1]
}

function GetContainerByName {
    param (
        [string]$containerName
    )

    # Поиск контейнера по имени
    $container = $containers | Where-Object { $_.Name -eq $containerName }

    if ($null -eq $container) {
        Write-Host "Контейнер с именем '$containerName' не найден." -ForegroundColor Red
        return $null
    }

    Write-Host "Контейнер '$containerName' найден." -ForegroundColor Green
    return $container
}

function Action10 {
    $container = Select-Container
    if ($null -eq $container) { Write-Host "Неверный выбор" -ForegroundColor Red; return }
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "docker exec -it $($container.Name) sh"
}
function Action20 {
    $container = Select-Container
    if ($null -eq $container) { Write-Host "Неверный выбор" -ForegroundColor Red; return }
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "docker exec -it $($container.Name) /bin/bash -c 'apt-get update && apt-get install -y procps curl net-tools mc && echo && ps aux | grep -v /bin/bash && echo && (curl -s http://localhost:80 || echo CURLfailed) && echo && netstat -tuln'"
}

function BuildAndRunBusinessLogicContainers {
    BuildAndRunContainer -container (GetContainerByName -containerName 'admin-node')
    BuildAndRunContainer -container (GetContainerByName -containerName 'assort-api-container')
    BuildAndRunContainer -container (GetContainerByName -containerName 'web_logger-container')
}

function SelectContainerAndRun {
    $container = Select-Container
    if ($null -eq $container) {
        Write-Host "Неверный выбор контейнера" -ForegroundColor Red
        return $null
    }
    BuildAndRunContainer -container $container
}


function BuildAndRunContainer {
    param (
        [PSCustomObject]$container
    )

    # Подготовка основных путей и переменных
    $projectPath    = $container.ProjectPath
    $contextPath    = $container.ContextPath
    $imageName      = ("{0}_image" -f $container.Name).ToLower()
    $dockerfilePath = Join-Path $projectPath 'Dockerfile'
    $portExt        = $container.PortExt
    $portInt        = $container.PortInt

    if (-not (Test-Path $contextPath)) {
        Write-Error "Контекст сборки не найден: $contextPath"
        return
    }

    
# Проверка и удаление существующего контейнера, если он есть
Write-Host "Проверка запущенного контейнера с именем $($container.Name)..."
$existingContainer = docker ps -a -q --filter "name=$($container.Name)"

if ($existingContainer) {
    Write-Host "Остановка существующего контейнера $($container.Name)..."
    docker stop $existingContainer

    Write-Host "Удаление существующего контейнера $($container.Name)..."
    docker rm $existingContainer

    # Проверка, что контейнер действительно удалён
    $checkContainer = docker ps -a -q --filter "name=$($container.Name)"
    if ($checkContainer) {
        Write-Host "Ошибка: не удалось удалить контейнер с именем $($container.Name)!" -ForegroundColor Red
        throw "Не удалось удалить контейнер. Скрипт завершён."
    } else {
        Write-Host "Контейнер $($container.Name) успешно удалён." -ForegroundColor Green
    }
} else {
    Write-Host "Контейнер с именем $($container.Name) не найден."
}


    Write-Host "Переход в каталог проекта..."
    Set-Location -Path $projectPath

    Write-Host "Сборка проекта..."
    dotnet build

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка сборки проекта. Проверьте код и повторите попытку."
        return
    }

    Write-Host "Создание Docker-образа..."
    docker build -t $imageName -f $dockerfilePath $contextPath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка создания Docker-образа. Проверьте Dockerfile и повторите попытку."
        return
    }

    Write-Host "Запуск нового контейнера..."
    docker run -e 'ASPNETCORE_URLS=http://*:80' --network docker-networkmall2 -e 'ASPNETCORE_ENVIRONMENT=Development' -d --name $container.Name -p "$($portExt):$($portInt)" $imageName

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка запуска контейнера. Проверьте параметры и повторите попытку."
        return
    }

    Write-Host "Получение IP-адреса контейнера..."
    $containerIP = docker inspect -f '{{range.NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $container.Name
    Write-Host "Контейнер запущен. IP-адрес контейнера: $containerIP"
}

function Show-Menu {
    Write-Host "Меню:" -ForegroundColor Green
    Write-Host "10 -- Подключиться к выбранному контейнеру"
    Write-Host "20 -- Вывести диагностическую информацию для выбранного контейнера"
    Write-Host "30 -- Сбилдить и запустить контейнер"
    Write-Host "40 -- Сбилдить и запустить бизнес-логику"
    Write-Host "80 -- Очистить экран (CLS)"
    Write-Host "99 -- Выход"
}

# Основной цикл работы
do {
    Show-Menu
    $choice = Read-Host "Выберите пункт меню"

    switch ($choice) {
        "10" { Action10 }
        "20" { Action20 }
        "30" { SelectContainerAndRun }
        "40" { BuildAndRunBusinessLogicContainers } 
        "80" { Clear-Host }
        "99" { break }
        default {
            Write-Host "Некорректный выбор. Повторите ввод." -ForegroundColor Red
        }
    }
}while ($choice -ne "99")

Write-Host "Программа завершена." -ForegroundColor Green

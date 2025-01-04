# Получение пути текущего скрипта
$scriptDirectory = $PSScriptRoot

# Функция для выполнения внешнего скрипта
function ExecExternalScript {
    param (
        [string]$scriptPath
    )

    $targetScriptPath = Join-Path -Path $scriptDirectory -ChildPath $scriptPath

    # Запуск целевого скрипта
    Write-Host "Запуск скрипта: $targetScriptPath"
    & $targetScriptPath

    # Проверка результата выполнения
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка при выполнении скрипта: $targetScriptPath"
        exit $LASTEXITCODE
    }
}

# Определяем обработчики для каждого пункта меню

function Action20 {
    ExecExternalScript -scriptPath "Powershell\DockerNetworkInfo.ps1"
}

function Action30 {
    ExecExternalScript -scriptPath "Powershell\AddContainersToNetwork.ps1"
}

function Action4 {
            # Определяем путь к каталогу для сохранения логов на хосте
            $destinationPath = "C:\debuglogs"

            # Очищаем каталог на хосте, если он существует
            if (Test-Path $destinationPath) {
                Remove-Item -Recurse -Force $destinationPath
            }

            # Создаем каталог заново
            New-Item -ItemType Directory -Force -Path $destinationPath

            # Имя контейнера
            $containerName = "SampleOnlineMall.AssortmentApi_2"

            # Путь к логам внутри контейнера
            $containerLogsPath = "/app/bin/debug/net6.0/Logs"

            # Копируем содержимое из контейнера на хост
            Write-Host "Копирование логов из контейнера $containerName..."
            $str1 = "$($containerName):$containerLogsPath/."; $str2= "$destinationPath"
            $str1
            docker cp $str1 $str2

            Write-Host "Логи успешно скопированы в $destinationPath"

            # Открытие папки в проводнике Windows
            Start-Process explorer.exe $destinationPath
}


function Action41 {
    # Определяем путь к каталогу для сохранения логов на хосте
    $destinationPath = "C:\debuglogs"

    # Очищаем каталог на хосте, если он существует
    if (Test-Path $destinationPath) {
        Remove-Item -Recurse -Force $destinationPath
    }

    # Создаем каталог заново
    New-Item -ItemType Directory -Force -Path $destinationPath

    # Имя контейнера
    $containerName = "assortmentapi-container"

    # Путь к логам внутри контейнера
    $containerLogsPath = "/app/Logs"

    # Копируем содержимое из контейнера на хост
    Write-Host "Копирование логов из контейнера $containerName..."
    $str1 = "$($containerName):$containerLogsPath/."; $str2= "$destinationPath"
    $str1
    docker cp $str1 $str2

    Write-Host "Логи успешно скопированы в $destinationPath"

    # Открытие папки в проводнике Windows
    Start-Process explorer.exe $destinationPath
}

function Action70 {
    ExecExternalScript -scriptPath "Powershell\СontainerManagement.ps1"
}
function ClearScreen {
    Clear-Host
}

# Функция отображения меню
function Show-Menu {
    Write-Host "Меню:"
    Write-Host "20  -- Показать состав докер сети"
    Write-Host "30  -- Добавить все запущенные контейнеры в сеть docker-networkmall2"
    Write-Host "40  -- Скопировать логи ассортиментного контроллера кот из VS2022"
    Write-Host "41  -- Скопировать логи ассортиментного контроллера кот из под Docker"
    Write-Host "70  -- Сделать действия с докер контейнерами"
    Write-Host "80  -- Очистить экран (CLS)"
    Write-Host "99  -- Выход"
}

# Основной цикл работы
do {
    Show-Menu
    $choice = Read-Host "Выберите пункт меню"

    switch ($choice) {
        "20"   { Action20 } #сеть
        "30"   { Action30 } #сеть
        "40"   { Action40 } #логи 
        "41"   { Action41 } #логи
        "70"   { Action70 } 
        "80"   { ClearScreen }
        "99"   { Write-Host "Выход из программы..." -ForegroundColor Red }
        default {
            Write-Host "Некорректный выбор. Повторите ввод." -ForegroundColor Red
            continue  # Возвращаемся к началу цикла без ожидания ввода
        }
    }

} while ($choice -ne "99")

Write-Host "Программа завершена." -ForegroundColor Green

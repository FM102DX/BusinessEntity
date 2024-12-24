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
function Action1 {
    ExecExternalScript -scriptPath "Powershell\BuildAssortAndOpenInDocker.ps1"
}

function Action2 {
    ExecExternalScript -scriptPath "Powershell\DockerNetworkInfo.ps1"
}

function Action3 {
    ExecExternalScript -scriptPath "Powershell\AddContainersToNetwork.ps1"
}

function ClearScreen {
    Clear-Host
}

# Функция отображения меню
function Show-Menu {
    Write-Host "Меню:"
    Write-Host "1 -- Запустить ассортимент в докере"
    Write-Host "2 -- Показать состав докер сети"
    Write-Host "3 -- Добавить все запущенные контейнеры в сеть docker-networkmall2"
    Write-Host "80 -- Очистить экран (CLS)"
    Write-Host "99 -- Выход"
}

# Основной цикл работы
do {
    Show-Menu
    $choice = Read-Host "Выберите пункт меню"

    switch ($choice) {
        "1" { Action1 }
        "2" { Action2 }
        "3" { Action3 }
        "80" { ClearScreen }
        "99" { Write-Host "Выход из программы..." -ForegroundColor Red }
        default {
            Write-Host "Некорректный выбор. Повторите ввод." -ForegroundColor Red
            continue  # Возвращаемся к началу цикла без ожидания ввода
        }
    }

} while ($choice -ne "99")

Write-Host "Программа завершена." -ForegroundColor Green

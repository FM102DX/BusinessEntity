# ShowMultipleContainersNetworks.ps1
# Выводим ТОЛЬКО те сети, в которых есть хотя бы один контейнер.
# Если таких сетей нет, сообщаем пользователю.

Write-Host "===Сети Докер, содержащие хотябы 1 контейнер ===" -ForegroundColor Yellow

# Получаем список всех имён Docker-сетей
$networkList = docker network ls --format "{{.Name}}"

# Заводим коллекцию для подходящих сетей
$matchingNetworks = @()

foreach ($netName in $networkList) {
    # Информация по сети в формате JSON
    $netInspect = docker network inspect $netName | ConvertFrom-Json
    $netObject  = $netInspect[0]

    $containers = $netObject.Containers
    $containerCount = $containers.Count

    # Проверяем, если в сети есть хотя бы один контейнер
    if ($containerCount -gt 0) {
        # Добавляем информацию об этой сети в нашу коллекцию
        $matchingNetworks += $netObject
    }
}

# Если matchingNetworks пуст, пишем уведомление
if ($matchingNetworks.Count -eq 0) {
    Write-Host "Нет сетей, в которых есть запущенные докер-контейнеры." -ForegroundColor Red
    Write-Host "******************"
    Write-Host ""
    return
}

# Выводим таблицу для каждой найденной сети
foreach ($netObject in $matchingNetworks) {
    $netName = $netObject.Name
    $containers = $netObject.Containers

    # Преобразуем контейнеры в массив значений
    $containerArray = @()
    foreach ($key in $containers.PSObject.Properties.Name) {
        $containerArray += $containers.$key
    }

    # Если контейнеров нет, пропускаем эту сеть
    if ($containerArray.Count -eq 0) {
        continue
    }

    # Заголовок таблицы
    Write-Host "`nСеть: $netName (Количество контейнеров: $($containerArray.Count))" -ForegroundColor Yellow
    Write-Host "------------------------------------------------------------------------------------------------------"
    Write-Host "Контейнер                                                    | IPv4-адрес         | MAC-адрес         "
    Write-Host "-------------------------------------------------------------|--------------------|-------------------"

    # Перебор контейнеров в массиве
    foreach ($container in $containerArray) {
        if ($null -eq $container) {
            continue  # Пропускаем пустые записи
        }

        # Извлечение данных контейнера
        $name = $container.Name
        $ipv4 = $container.IPv4Address
        $macAddress = $container.MacAddress

        # Обработка IPv4 (удаление маски)
        if ($null -ne $ipv4) {
            $ipv4 = $ipv4.Split('/')[0]
        } else {
            $ipv4 = "Нет IP-адреса"
        }

        # Форматированный вывод строки
        $formattedLine = "{0,-60} | {1,-18} | {2,-17}" -f $name, $ipv4, $macAddress
        Write-Host -Object $formattedLine
    }

    Write-Host "------------------------------------------------------------------------------------------------------"
}

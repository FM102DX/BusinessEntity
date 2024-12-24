# Проверяем, существует ли сеть с именем docker-networkmall2 
$networkName = "docker-networkmall2"

try {
    $existingNetwork = docker network ls --filter "name=$networkName" --format "{{.Name}}"

    if (-not $existingNetwork) {
        Write-Output "Сеть '$networkName' не существует. Создаем..."
        docker network create $networkName
    } else {
        Write-Host "Сеть '$networkName' уже существует." -ForegroundColor Blue
    }

    # Получаем список всех запущенных контейнеров
    $runningContainers = docker ps --format "{{.Names}}"

    if (-not $runningContainers) {
        Write-Output "Нет запущенных контейнеров."
        return
    }

    # Добавляем каждый запущенный контейнер в сеть
    foreach ($container in $runningContainers) {
        try {
            # Проверяем, подключен ли контейнер к сети
            $networkInspect = docker network inspect $networkName | ConvertFrom-Json
            $isConnected = $false

            foreach ($entry in $networkInspect.Containers.PSObject.Properties.Value) {

               # Write-Host "`n$entry.Name $container`n"
                if ($entry.Name -eq $container) {
                    $isConnected = $true
                    break
                }
            }

            if (-not $isConnected) {
                Write-Output "Добавляем контейнер $container в сеть $networkName..."
                docker network connect $networkName $container
            } else {
                Write-Host "Контейнер $container уже подключен к сети $networkName." -ForegroundColor Blue
            }
        } catch {
            Write-Host "Ошибка при обработке контейнера $container : $_" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "Общая ошибка выполнения скрипта: $_" -ForegroundColor Red
}

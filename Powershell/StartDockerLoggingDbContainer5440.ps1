# Имя контейнера
$containerName = "postgres-logger-db"

# Имя образа
$imageName = "postgres:latest"

# Порт для новой БД
$hostPort = 5440
$containerPort = 5432

# Переменные окружения для PostgreSQL
$pgUser = "adm01"
$pgPassword = "adm01pws"
$pgDatabase = "logger_db"

# Проверка переменных перед запуском
Write-Host "=== Проверка параметров ==="
Write-Host "Container Name: '$containerName'"
Write-Host "Image Name: '$imageName'"
Write-Host "Postgres User: '$pgUser'"
Write-Host "Postgres Password: '$pgPassword'"
Write-Host "Postgres Database: '$pgDatabase'"
Write-Host "Host Port: '$hostPort'"
Write-Host "Container Port: '$containerPort'"


# Генерация команды docker run
$dockerRunCommand = "docker run -d --name $containerName --network docker-networkmall2 " +
    "-e POSTGRES_USER=$pgUser -e POSTGRES_PASSWORD=$pgPassword " +
    "-e POSTGRES_DB=$pgDatabase -p $($hostPort):$containerPort $imageName"

Write-Host "Команда запуска: $dockerRunCommand"

# Запуск контейнера
try {
    Write-Host "Запускается контейнер $containerName..."
    Invoke-Expression $dockerRunCommand

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Контейнер $containerName успешно запущен."
    } else {
        Write-Host "Ошибка при запуске контейнера $containerName. Код выхода: $LASTEXITCODE"
    }
} catch {
    Write-Host "Произошла ошибка при запуске контейнера:"
    Write-Host $_.Exception.Message
    if ($_.Exception.InnerException) {
        Write-Host "Дополнительная информация: $($_.Exception.InnerException.Message)"
    }
}

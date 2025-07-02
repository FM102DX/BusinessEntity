# PowerShell скрипт для отправки 5 тестовых сообщений в контроллер WebLogger
# URL: http://localhost:5080/api/WebLogger/CreateLogRecord
# Использует рандомные анекдоты и сообщения об ошибках

# Массив анекдотов про программистов
$anecdotes = @(
    "Программист читает книгу и говорит: 'Зря я это начал. Тут ничего не работает без исходников!'",
    "Почему программисты любят зиму? Потому что её можно сократить до 'зим'.",
    "Что будет, если программист уйдет в отпуск? Баги тоже уходят в отпуск!",
    "Встречаются два программиста: - Как дела? - Не знаю, не дебажил.",
    "Программисту говорят: 'Ты неадекватный!' А он в ответ: 'NullPointerException!'"
)

# Массив анекдотов про недвижимость
$realEstateJokes = @(
    "Почему агент по недвижимости никогда не теряет ключи? Потому что у него всегда есть запасной план!",
    "Недвижимость — это когда ты продаешь мечту и покупаешь реальность.",
    "Клиент спрашивает: 'Почему дом такой дорогой?' Агент отвечает: 'Это недвижимость, а не распродажа!'",
    "Чем больше этажей, тем выше надежда на скорую продажу.",
    "Квартира в центре города: близко к работе, далеко от зарплаты.",
    "Недвижимость — единственный бизнес, где квадратные метры имеют круглую цену.",
    "Агент по недвижимости не шутит про скидки. Он торгуется серьёзно!",
    "Зачем покупать дом на дереве? Чтобы быть поближе к своим корням!",
    "Недвижимость — это когда ты строишь планы и продаёшь их другому.",
    "Продается дом с видом на горы. Горы не входят в стоимость."
)

# Массив сгенерированных сообщений об ошибках
$errorMessages = @(
    "Ошибка подключения к базе данных.",
    "Некорректный формат запроса.",
    "Сервер временно недоступен.",
    "Ошибка аутентификации пользователя.",
    "Таймаут запроса истёк."
)

# URL контроллера
$url = "http://localhost:5080/api/WebLogger/CreateLogRecord"

# Функция для отправки POST-запроса с лог-записью
function Send-LogEntry {
    param (
        [datetime]$Timestamp,
        [string]$ServiceCode,
        [string]$MessageType,
        [string]$Message
    )

    $logEntry = [PSCustomObject]@{
        Timestamp = $Timestamp.ToString("o")
        ServiceCode = $ServiceCode
        MessageType = $MessageType
        Message = $Message
    }

    $jsonBody = $logEntry | ConvertTo-Json -Depth 10

    try {
        $response = Invoke-RestMethod -Uri $url -Method Post -Body $jsonBody -ContentType "application/json; charset=utf-8"
        Write-Host "Ответ сервера: $($response.StatusCode) - $($response.StatusDescription)"
    } catch {
        Write-Host "Ошибка при отправке запроса: $_"
    }
}

# Отправка 5 тестовых сообщений
for ($i = 1; $i -le 5; $i++) {
    $randomMessage = Get-Random -InputObject ($anecdotes + $realEstateJokes + $errorMessages)
    $timestamp = Get-Date -AsUTC
    Send-LogEntry -Timestamp $timestamp -ServiceCode "SHELL" -MessageType "Info" -Message $randomMessage
}

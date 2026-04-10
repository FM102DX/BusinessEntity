# Интеграция Authentik в Docker Compose - Саммари

**Дата**: 10 апреля 2026  
**Задача**: Интеграция сервиса аутентификации Authentik в основной `docker-compose.yml` проекта BusinessEntity

---

## 🎯 Цель работы

Объединить отдельный `Authentic/compose.yml` с основным `docker-compose.yml`, чтобы весь стек (BusinessEntity + Authentik + PostgreSQL + WebLogger) запускался одной командой `docker compose up -d`.

---

## ✅ Выполненные задачи

### 1. Интеграция Docker Compose

**Было**: Authentik запускался отдельно через `Authentic/compose.yml`

**Сделано**: Инлайнили все сервисы Authentik в основной `docker-compose.yml`:

- `authentic_postgresql` — PostgreSQL 16-alpine для Authentik
- `authentic_server` — основной сервер Authentik (порты 9000/9443)
- `authentic_worker` — воркер для фоновых задач

**Ключевые изменения**:
- Настроена общая сеть `common` для всех сервисов
- Добавлены healthcheck для всех Authentik сервисов
- Настроены `depends_on` с условиями `service_healthy` для правильного порядка запуска
- Все пути к volumes и env_file относительно корня проекта (`./Authentic/...`)

### 2. Создание `.env` файла для Authentik

**Файл**: `c:\Develop\BusinessEntity\Authentic\.env`

```env
# Authentik PostgreSQL credentials
PG_DB=authentik
PG_USER=authentik
PG_PASS=authentik_db_password_change_me

# Authentik secret key (used for encryption, sessions, etc.)
AUTHENTIK_SECRET_KEY=5f81e1c4b8c0417c9f7b01a6d83a44dbdf18a27c29b93c4c9b71a9e2e06d1d3c1234567890abcdef

# Authentik bootstrap token for API access
AUTHENTIK_BOOTSTRAP_TOKEN=5f81e1c4b8c0417c9f7b01a6d83a44dbdf18a27c29b93c4c9b71a9e2e06d1d3c

# Authentik image version
AUTHENTIK_IMAGE=ghcr.io/goauthentik/server
AUTHENTIK_TAG=2026.2.1

# HTTP/HTTPS ports
COMPOSE_PORT_HTTP=9000
COMPOSE_PORT_HTTPS=9443
```

### 3. Создание админ-пользователя и API токена

**Recovery key для первого входа**:
```bash
docker compose exec authentic_server ak create_recovery_key 10 akadmin
# URL: http://localhost:9000/recovery/use-token/bq3DXz7zAXsR6Eq28HlZRx99Fr61OVt1JKgtiDO329xISqjj7qXhX0DMiow6/
```

**API Token (создан через Django shell)**:
```bash
docker compose exec authentic_server python manage.py shell -c "from authentik.core.models import Token, TokenIntents, User; u=User.objects.get(username='akadmin'); t=Token.objects.create(user=u, identifier='BusinessEntity API Bootstrap', intent=TokenIntents.INTENT_API, expiring=False); print(t.key)"
```

**Токен**: `ecm2ragMCo5vRjIqtdzXXnFARVbeczUyZtkCYrpgBD4dcz4JzfjdLQrL5ksZ`

### 4. Исправления конфигурации

#### `docker-compose.yml`

**Проблема**: Healthcheck использовал `wget`, которого нет в образе Authentik

**Решение**:
```yaml
healthcheck:
  test: ["CMD-SHELL", "curl -f http://localhost:9000/-/health/live/ || exit 1"]
  interval: 30s
  timeout: 10s
  retries: 5
  start_period: 90s  # Увеличен для надёжности
```

**Добавлена переменная для отключения bootstrap**:
```yaml
environment:
  EnsureAuthentikOnStartup: "false"  # Временно отключён bootstrap
```

#### `BusinessEntity/Dockerfile`

**Проблема**: Ссылки на несуществующие проекты `SampleOnlineMall.*`

**Решение** — заменены на правильные зависимости:
```dockerfile
COPY ["BusinessEntity/BusinessEntity.csproj", "BusinessEntity/"]
COPY ["BusinessEntity.Service/BusinessEntity.Service.csproj", "BusinessEntity.Service/"]
COPY ["BusinessEntity.Core/BusinessEntity.Core.csproj", "BusinessEntity.Core/"]
COPY ["BusinessEntity.DataAccess/BusinessEntity.DataAccess.csproj", "BusinessEntity.DataAccess/"]
RUN dotnet restore "./BusinessEntity/BusinessEntity.csproj"
```

### 5. Попытки исправить Authentik Bootstrap

**Проблема**: API endpoints Authentik возвращают 404 Not Found при использовании токена

**Протестированные endpoints**:
- ❌ `/api/v3/core/system/version/` — 404
- ❌ `/api/v3/admin/system/` — 404  
- ❌ `/api/v3/root/config/` — 404

**Изменения в коде**:

**`AuthentikClient.cs`**:
- Обновлён endpoint с `/api/v3/core/system/version/` → `/api/v3/root/config/`
- Упрощена логика проверки версии (возвращает dummy version)

**`Models.cs`**:
- Упрощён `VersionDto` (убрана вложенная структура `RuntimeInfo`)

**`AuthentikBootstrapService.cs`**:
- Обновлены сообщения логов (`admin/system` → `API check`)

---

## 🚀 Текущий статус системы

### ✅ Работает

**Команда запуска**:
```bash
docker compose up -d
```

**Работающие сервисы**:

| Сервис | URL | Порт | Статус |
|--------|-----|------|--------|
| Authentik UI | http://localhost:9000 | 9000, 9443 | ✅ Healthy |
| BusinessEntity | http://localhost:7000 | 7000 | ✅ Running |
| Web Logger | http://localhost:5080 | 5080 | ✅ Running |
| PostgreSQL (BE) | localhost:5470 | 5470 | ✅ Healthy |
| PostgreSQL (Authentik) | internal | 5432 | ✅ Healthy |
| Authentik Worker | internal | - | ✅ Healthy |

**Проверка статуса**:
```bash
docker compose ps
docker compose logs -f business-entity
docker compose logs -f authentic_server
```

### ⚠️ Проблема: Автоматический Bootstrap не работает

**Симптомы**:
- При `EnsureAuthentikOnStartup=true` приложение зависает на попытках достучаться до Authentik API
- Все API endpoints возвращают `404 Not Found` с пустым Body
- Токен создан правильно (Intent: API, User: akadmin/superuser)

**Текущее решение**:
- Отключён автоматический bootstrap (`EnsureAuthentikOnStartup: "false"`)
- OIDC Provider и Application создаются **вручную** в Authentik UI

**Последствия**:
- Приложение запускается и работает
- Требуется ручная первичная настройка в Authentik UI

---

## 📂 Изменённые файлы

```
c:\Develop\BusinessEntity\
├── docker-compose.yml                                    ← Инлайнены сервисы Authentik
├── Authentic\
│   └── .env                                              ← Создан (новый файл)
└── BusinessEntity\
    ├── Dockerfile                                        ← Исправлены зависимости
    └── Authentik\
        ├── AuthentikClient.cs                            ← Обновлён endpoint
        ├── Models.cs                                     ← Упрощён VersionDto
        └── AuthentikBootstrapService.cs                  ← Обновлены логи
```

---

## 🔍 Анализ нерешённой проблемы

### Проблема: API токен не даёт доступ к Authentik API

**Факты**:
1. Токен создан с правильным `Intent` (`TokenIntents.INTENT_API`)
2. Пользователь `akadmin` имеет права `is_superuser=True, is_staff=True`
3. Healthcheck endpoint `/-/health/live/` работает (без токена)
4. Токен работает при запросах из контейнера `authentic_server` к `localhost:9000/api/v3/root/config/`
5. Тот же токен НЕ работает при запросах из `business-entity` к `authentic_server:9000/api/v3/root/config/`

**Возможные причины**:

1. **Изменения в Authentik API 2026.2.1**:
   - Endpoints могли быть переименованы/перемещены
   - Изменились требования к аутентификации
   - Документация на goauthentik.io может быть устаревшей

2. **Проблема с Cross-container аутентификацией**:
   - Токен может требовать дополнительных заголовков при межконтейнерных запросах
   - Возможна проблема с CORS/Host validation

3. **Permissions/Scopes**:
   - API Token может требовать дополнительных permissions/scopes
   - Даже superuser может не иметь доступ к некоторым admin endpoints через API token

4. **Networking**:
   - DNS-резолвинг работает (healthcheck проходит)
   - HTTP-запросы доходят (получаем 404, а не connection refused)
   - Проблема на уровне application layer

**Протестированные решения**:
- ✅ Проверка токена через Django shell — токен существует и валиден
- ✅ Проверка superuser статуса — `is_superuser=True`
- ❌ Использование разных endpoints — все возвращают 404
- ❌ Проверка с заголовком `Accept: application/json` — не помогло

---

## 🎯 Рекомендации для дальнейшей работы

### Вариант 1: Ручная настройка (текущий подход)

**Преимущества**: Работает прямо сейчас  
**Недостатки**: Требует ручной настройки при первом запуске

**Шаги**:
1. Запустить стек: `docker compose up -d`
2. Открыть Authentik UI: http://localhost:9000
3. Создать OIDC Provider вручную
4. Создать Application вручную
5. Скопировать Client ID и Client Secret в конфиг приложения

### Вариант 2: Исследовать Authentik API 2026.2.1

**Действия**:
1. Изучить официальную документацию Authentik 2026.2.1
2. Проверить Swagger UI: http://localhost:9000/api/v3/schema/swagger-ui/
3. Посмотреть логи `authentic_server` на предмет ошибок авторизации:
   ```bash
   docker compose logs authentic_server | grep -i "auth\|token\|403\|401"
   ```
4. Попробовать Session-based authentication вместо Bearer token

### Вариант 3: Downgrade на стабильную версию

**Рассмотреть переход на**:
- Authentik 2024.10.x (LTS)
- Или другую проверенную версию с работающим API

**Изменить в `.env`**:
```env
AUTHENTIK_TAG=2024.10.3
```

### Вариант 4: Использовать Blueprints

Authentik поддерживает декларативную конфигурацию через YAML blueprints:
- Создать YAML с описанием Provider и Application
- Положить в `./Authentic/blueprints/`
- Authentik автоматически применит при старте

---

## 📝 Полезные команды

### Docker Compose
```bash
# Запуск всего стека
docker compose up -d

# Остановка всего стека
docker compose down

# Остановка с удалением volumes (полная очистка)
docker compose down -v

# Просмотр логов
docker compose logs -f business-entity
docker compose logs -f authentic_server

# Проверка статуса
docker compose ps

# Пересборка конкретного сервиса
docker compose build --no-cache business-entity
docker compose up -d business-entity
```

### Authentik CLI
```bash
# Создать recovery key
docker compose exec authentic_server ak create_recovery_key 10 akadmin

# Django shell
docker compose exec authentic_server python manage.py shell

# Проверить логи
docker compose logs authentic_server --tail 100
```

### Отладка API
```bash
# Проверить endpoint из контейнера authentic_server
docker compose exec authentic_server curl -s -H "Authorization: Bearer <TOKEN>" http://localhost:9000/api/v3/root/config/

# Проверить DNS резолвинг
docker compose exec business-entity nslookup authentic_server

# Проверить подключение
docker compose exec business-entity ping authentic_server
```

---

## 📚 Дополнительные ресурсы

- **Authentik Documentation**: https://docs.goauthentik.io/
- **Authentik API Schema**: http://localhost:9000/api/v3/schema/
- **Swagger UI**: http://localhost:9000/api/v3/schema/swagger-ui/
- **Docker Compose Docs**: https://docs.docker.com/compose/

---

## 🏁 Итого

**Что работает**:
- ✅ Все контейнеры запускаются и работают стабильно
- ✅ Authentik UI доступен и функционален
- ✅ BusinessEntity приложение стартует
- ✅ Healthchecks проходят
- ✅ Сеть настроена правильно

**Что требует доработки**:
- ⚠️ Автоматический bootstrap (временно отключён)
- ⚠️ API токен не даёт доступ к endpoints (требует исследования)

**Рекомендация**: Продолжить с ручной настройкой или исследовать Authentik API 2026.2.1 в отдельной ветке.

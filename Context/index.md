# BotManager01 (BotFactory) — Описание приложения

## 1. Бизнес-цели и назначение

**BotManager01** (торговое название **BotFactory**) — платформа для создания, настройки и эксплуатации AI-ботов, интегрированных с корпоративными мессенджерами (Яндекс.Мессенджер, с заделом под Telegram).

Основные бизнес-задачи:

- **Автоматизация общения** — боты принимают сообщения из мессенджера, классифицируют их (квалификация), выбирают подходящий сценарий обработки и формируют ответ с помощью LLM-моделей.
- **Мульти-бот управление** — из единого GUI можно создавать, настраивать, запускать и останавливать произвольное количество независимых ботов, каждый со своими настройками, моделями и сценариями.
- **Мульти-шаговые сценарии** — бизнес-логика оформляется как цепочки шагов (Custom Agent Scenarios), каждый из которых может вызывать AI-модель, делегировать выполнение другому сценарию, работать с файлами и репозиториями.
- **Model Warehouse** — централизованное управление каталогом AI-моделей (OpenAI, Claude, DeepSeek, Cursor CLI, Codex CLI) с версионированием наборов моделей и round-robin выбором.
- **Работа с кодом** — боты умеют клонировать репозитории, запускать агентные CLI (cursor-agent, codex) через WSL, анализировать вложения и генерировать отчёты.

Технологический стек: **.NET 6**, **WPF**, **Topshelf**, **PostgreSQL** (EF Core 7), **Serilog**, **ReactiveUI**, **OpenAI SDK 2.x**, **WSL** (для агентных CLI).

---

## 2. Глобальная архитектура

Система состоит из **трёх уровней** (три отдельных процесса), а также разделяемых библиотек:

```
┌─────────────────────────────────────────────────────┐
│                   GUI (WPF)                         │
│            BotManager01.Gui.csproj                  │
│         (WinExe, net6.0-windows)                    │
└──────────────────┬──────────────────────────────────┘
                   │  Named Pipes (app2service / service2app)
                   ▼
┌─────────────────────────────────────────────────────┐
│            Worker Service (TopShelf)                 │
│    BotManager01.WorkerService.TopShelf.csproj        │
│          (Exe, net6.0, Windows Service)              │
└──────────────────┬──────────────────────────────────┘
                   │  Named Pipes (runner_{botId}_in / runner_{botId}_out)
                   │  + запуск процесса BotRunner.exe
                   ▼
┌─────────────────────────────────────────────────────┐
│              BotRunner (Console)                     │
│            BotRunner.csproj                          │
│       (Exe, net6.0, по одному на бота)               │
└─────────────────────────────────────────────────────┘
```

### Разделяемые библиотеки

| Проект | Назначение |
|--------|-----------|
| `BotManager01.Shared` | Утилиты, версия приложения, конфигурация БД, сжатие, хелперы файлов, ReactiveMessageBus |
| `BotManager01.Shared.Pipes` | IPC-инфраструктура: Named Pipes (PipeServer/PipeClient), `NamedPipeCommunicationStation`, `CommunicationMessageBase` |
| `BotManager01.Domain` | Доменные модели (Bot, BotSettings, SystemSettings, Messenger, ModelWarehouse, Scenarios), репозитории, сервисы |
| `BotManager01.DataAccess` | EF Core контекст (`EFPostgresDbContext`), generic-репозиторий `EfAsyncRepository<T>`, миграции PostgreSQL |

### Граф зависимостей проектов

```
BotManager01.Gui
  ├── BotManager01.Domain
  │     ├── BotManager01.DataAccess
  │     │     ├── BotManager01.Shared.Pipes
  │     │     │     └── BotManager01.Shared
  │     │     └── BotManager01.Shared
  │     ├── BotManager01.Shared.Pipes
  │     └── BotManager01.Shared
  └── BotManager01.Shared

BotManager01.WorkerService.TopShelf
  ├── BotManager01.Domain
  ├── BotManager01.Shared.Pipes
  └── BotRunner (project reference + post-build copy BotRunner.exe)

BotRunner
  ├── BotManager01.Domain
  ├── BotManager01.DataAccess
  ├── BotManager01.Shared.Pipes
  └── BotManager01.Shared
```

---

## 3. Архитектура каждого уровня

### 3.1. GUI (BotManager01.Gui)

**Тип:** WPF-приложение (WinExe, net6.0-windows).
**Точка входа:** `App.xaml.cs` → DI через `Microsoft.Extensions.DependencyInjection`.

#### Основные компоненты

- **App.xaml.cs** — bootstrap: регистрация всех сервисов в DI, настройка логирования (Serilog → файл), подключение к PostgreSQL (EF Core миграции при старте), создание `NamedPipeCommunicationStation` для связи с Worker, запуск tray-иконки.
- **MainWindow / MainWindowViewModel** — главное окно с вкладками:
  - **Боты** — список ботов (CRUD, старт/стоп, клонирование, редактирование, версии).
  - **Системные настройки** — SecretNumber, ServiceName, RunAs, каталог моделей (ModelsPriceData).
  - **Сценарии (SystemScenariosTabViewModel)** — дерево мульти-шаговых сценариев с папками, экспорт/импорт (.scenexp), drag-and-drop.
  - **Логи GUI (GuiLoggerManager)** — панель диагностических логов прямо в окне.
- **BotEditor (BotEditorWindow / BotEditorViewModel)** — окно редактирования конкретного бота:
  - Вкладки: Общее, Мессенджер, Квалификация, КвалификацияБиз, Сценарии бота, UseCases, Чаты.
  - Сохранение настроек бота с версионированием (каждое сохранение создаёт историческую копию).
  - Просмотр истории версий бота (BotVersionsWindow).
- **ModelWarehouse (ModelWarehouseWindow / ModelWarehouseViewModel)** — отдельное окно управления наборами моделей:
  - Вкладки: Модел-сеты, Модели (загрузка списка моделей от провайдеров через CLI), Версии, Промпт.
  - Также доступно как standalone-приложение (`ModelWarehouseStandalone`).
- **WorkerServiceManager** — управление жизненным циклом Windows-сервиса (install/uninstall/start/stop через Topshelf и sc.exe), RunAs/SeServiceLogonRight через LSA API.
- **AliveWatcher** — отслеживание heartbeat от Worker и ботов, обновление статусов в UI.

#### Архитектурные паттерны GUI

- **MVVM** — ViewModel классы (`*ViewModel`) с `INotifyPropertyChanged`, команды через `ICommand` / `ActionCommand`.
- **ReactiveUI / System.Reactive** — подписки на события (AliveWatcher, AppMessageBus) через `Observable.Subscribe`.
- **DI** — `ServiceProvider` создаётся в `App.xaml.cs`, все зависимости инжектируются через конструкторы.

---

### 3.2. Worker Service (BotManager01.WorkerService.TopShelf)

**Тип:** Console-приложение, устанавливаемое как Windows-сервис через Topshelf.
**Точка входа:** `Program.cs` → `HostFactory.Run` → `WorkerContainer` → `Worker`.

#### Основные компоненты

- **Program.cs** — настройка Topshelf: имя сервиса из SystemSettings (БД), RunAs из аргументов / env vars, логирование в файл (`LogsWorker/`).
- **Worker** — бизнес-логика сервиса:
  - Два таймера: `_timer` (1с, heartbeat `WorkerAlive` → GUI) и `_checkMailTimer` (1с, чтение команд от GUI).
  - Обработка команд: `RunBot` (запуск процесса BotRunner.exe), `StopBot` (мягкая остановка + kill), `SettingsChanged` (перезагрузка настроек), `Ping` (проброс в Runner).
  - **Управление процессами Runner:** словари `_botIdToProcess` (Process) и `_botIdToRunnerOut` (NamedPipeCommunicationStation). Каждый Runner запускается как отдельный процесс с аргументами `--botId=... --pipeBase=runner_{botId}`.
  - **Проброс сообщений:** сообщения от Runner (BotAlive, BotOperationResult, PingReply, GotYaMail, JobsListChanged) перенаправляются в GUI через `_guiStation`.
- **ActivitySchedulerWorkerApp** — пути и директории Worker-процесса.

#### Роль Worker

Worker — **диспетчер процессов**. Он не содержит бизнес-логики ботов, а управляет их жизненным циклом:
1. Принимает команды от GUI по Named Pipes.
2. Запускает/останавливает процессы BotRunner.exe.
3. Пробрасывает сообщения между GUI и Runner-ами.
4. Отправляет heartbeat в GUI для мониторинга здоровья.

---

### 3.3. BotRunner

**Тип:** Console-приложение (Exe, net6.0), запускается Worker-ом по одному экземпляру на каждого бота.
**Точка входа:** `Program.cs` (async Main).

#### Инициализация

1. Парсинг аргументов: `--botId`, `--pipeBase`, `--console`.
2. Загрузка бота из PostgreSQL через `BotRepository`.
3. Настройка логирования: per-bot папка `LogsRunner/<BotName>_<AliasGuid>/`.
4. Создание `NamedPipeCommunicationStation` для двустороннего IPC с Worker.
5. Создание и запуск `YandexMessengerManager`.
6. Главный цикл: отправка `BotAlive` heartbeat раз в секунду + слушатель команд (StopRunner, BotSettingsChanged, Ping).

#### Компоненты Messenger

- **YandexMessengerManager** — оркестратор верхнего уровня, связывает:
  - `YandexMessengerDriver` — HTTP-опрос Яндекс.Мессенджер API (long-polling updates), сохранение истории в PostgreSQL, отправка ответов.
  - `ChatsManager` — in-memory состояние чатов и сообщений, синхронизация с БД.
  - `TalkManager` — принятие решений об ответе (через `RespondService`), постановка в очередь, запуск Workflow.
  - `AttachmentsHolder` / `ReceivedMessagesAttachmentsTracker` — управление вложениями.
  - `JobManager` — управление Jobs (задачами бота), периодическая отправка снапшота Jobs в GUI.

#### Система Workflow

Центральное понятие — **Workflow** (реализация `IBotWorkflow`):

```csharp
public interface IBotWorkflow
{
    ChatMessage Message { get; }
    Task<WorkflowResult> ExecuteAsync(CancellationToken ct = default);
}
```

**BotWorkflowFactory** — фабрика, создающая нужный Workflow по `BotUseCaseEnum`:

| Workflow | Назначение |
|----------|-----------|
| `QualificationWorkflow` | Классификация входящего сообщения (Biz/Fun/Fraud/Misc/Unpolite) через LLM |
| `BizBotWorkflow` | Бизнес-сценарий: квалификация-биз, анализ вложений, выбор репозитория, запуск цепочки сценариев |
| `OneMessageBotWorkflow` | Простой однократный ответ через LLM |
| `CustomMultiStepScenarioWorkflow` | Мульти-шаговый сценарий: последовательное выполнение шагов с подстановкой переменных |
| `AttachmentsWorkflow` | Анализ вложений через AI |
| `FileCopyWorkflow` | Копирование файлов пользователя в рабочую папку Job |
| `JobFolderWorkflow` | Создание файловой структуры Job (Tmp, Result) |
| `RepositorySelectionWorkflow` | Выбор целевого репозитория из списка |
| `CodeActionsWorkflow` / `CodeAnswersWorkflow` | Работа с кодом: модификация / ответы на вопросы |
| `RepoAnswersWorkflow` / `RepoModificationWorkflow` | Ответы по репозиторию / модификация кода |
| `SimpleSayWorkflow` | Отправка текста в мессенджер |
| `PullRequestReadyNotificationWorkflow` | Уведомление о готовности PR с ожиданием ответа |
| `VarDumpWorkflow` | Дамп переменных сценария |

Все бизнес-Workflow наследуют `BotWorkflowBase`, которая предоставляет общую инфраструктуру: историю чата, транспорт (драйвер мессенджера), Job, JobManager.

#### AI-интеграция

- **ChatGptApiDriver** — потокобезопасный драйвер OpenAI Responses API (SDK 2.x). Паттерн: System → History → ContextBlock → CurrentUser. Возвращает `ChatReply` с метриками (токены, время, стоимость).
- **AiRespondService** — обёртка над `ChatGptApiDriver` для простых запросов.
- **OpenAiCostCalculator** — расчёт стоимости вызовов по каталогу моделей.

#### WSL-интеграция

Для запуска агентных CLI (cursor-agent, codex, pwsh) используется **WSL** (Windows Subsystem for Linux):

- **IWslRunner** — единый интерфейс: `Task<WslRunResponse> RunAsync(WslRunRequest req, CancellationToken ct)`.
- **WslRunnerFactory** — фабрика: по алиасу модели из каталога определяет провайдера (`CursorCLI`, `CodexCli`, `pwsh`) и создаёт соответствующий Runner.
- Реализации: `CursorAgentWslRunner`, `CodexAgentWslRunner`, `PwshWslRunner`.
- WSL вызывается как login-shell: `/bin/bash -lc "..."`.
- **WslProcessManager** — глобальный менеджер WSL-процессов, гарантирует их завершение при выходе Runner.

#### Система Jobs

**Job** — единица работы бота. Создаётся через `JobFactory` с уникальным 5-значным алиасом (00001, 00002, ...).

Файловая структура Job:
```
Jobs/Bot_<AliasGuid>/Job_<Alias>_<login>/
  ├── Tmp/          — временные файлы
  ├── Result/       — результаты
  ├── _stream/      — stream-файлы (success, progress)
  └── _vars/        — переменные сценария (WorkflowStepVariablesPack.json)
```

#### Переменные сценариев

**ScenarioVariablesHelper** — подстановка плейсхолдеров в промпты шагов:
- **Системные плейсхолдеры** (case-insensitive): `{TmpFolder}`, `{ResultFolder}`, `{JobNumber}`, `{JobDescription}`, `{WorkflowStepVariablesPackPath}` и др.
- **Локальные переменные** (case-insensitive): `{{VarName}}` — подставляются после системных.
- **Глобальные переменные**: `{{{GlobalVarName}}}` — общие для всех шагов сценария.
- Шаги могут экспортировать переменные через JSON-файл `WorkflowStepVariablesPack.json`.

---

## 4. Коммуникация между уровнями

### 4.1. IPC: Named Pipes

Все три уровня общаются через **Named Pipes** с помощью `NamedPipeCommunicationStation<CommunicationMessageBase>`:

```
GUI ←→ Worker:    пайпы "app2service" (GUI→Worker) и "service2app" (Worker→GUI)
Worker ←→ Runner: пайпы "runner_{botId}_in" (Worker→Runner) и "runner_{botId}_out" (Runner→Worker)
```

`NamedPipeCommunicationStation` инкапсулирует:
- `ServerCommunicationObjectT<T>` — серверный пайп для отправки (outbound).
- `ClientCommunicationObjectT<T>` — клиентский пайп для приёма (inbound).
- `ConcurrentQueue<T>` — внутренняя очередь принятых сообщений.
- Фоновый `Task` для чтения входящих сообщений.

### 4.2. Протокол сообщений

Все сообщения — JSON-сериализованные объекты `CommunicationMessageBase`:

| Поле | Назначение |
|------|-----------|
| `MessageType` | Тип сообщения (enum) |
| `Command` | Команда (RunBot, StopBot, StopRunner, ...) |
| `FromBotId` / `ToBotId` | Маршрутизация по идентификатору бота |
| `SenderType` | Отправитель: Gui / Worker / Bot |
| `SecretNumber` / `BotSecretNumber` | Числовой идентификатор для верификации |
| `BotStatus` | Текущий статус бота |
| `AppVersion` | Версия приложения отправителя |
| `Status` / `Message` | Текстовый статус / произвольные данные |

Основные типы сообщений (`MessageType`):

| Тип | Направление | Назначение |
|-----|-------------|-----------|
| `Command` | GUI→Worker, Worker→Runner | Команды (RunBot, StopBot, StopRunner) |
| `SettingsChanged` | GUI→Worker | Системные настройки изменены, перечитать из БД |
| `BotSettingsChanged` | GUI→Worker→Runner | Настройки конкретного бота изменены |
| `WorkerAlive` | Worker→GUI | Heartbeat Worker (раз в 1с) |
| `BotAlive` | Runner→Worker→GUI | Heartbeat бота (раз в 1с) |
| `Status` | Runner→Worker→GUI | Статус Runner (stopped, settings-reloaded) |
| `Ping` / `PingReply` | GUI→Worker→Runner→Worker→GUI | Проверка связи с конкретным ботом |
| `BotOperationResult` | Runner→Worker→GUI | Результат операции бота (OK / ERROR) |
| `GotYaMail` | Runner→Worker→GUI | Получена новая почта из мессенджера |
| `JobsListChanged` | Runner→Worker→GUI | Обновлённый снапшот списка Jobs бота |

### 4.3. Внутренняя шина событий (IAppMessageBus)

Внутри каждого процесса (GUI, Runner) используется **ReactiveMessageBus** (обёртка над `System.Reactive`):
- `Publish<T>(T event)` — публикация события.
- `Listen<T>() → IObservable<T>` — подписка на события определённого типа.

Ключевые события внутри Runner:
- `ChatsUpdatedEvent` — обновились данные чатов (триггер для TalkManager).
- `GotYaMailEvent` — получена новая пачка сообщений из мессенджера.
- `KvalCompletedEvent` — завершена квалификация сообщения.
- `ReplyToMessageReceivedEvent` — получен ответ на конкретное сообщение бота.
- `AttachmentsDownloadCompletedEvent` — загрузка вложений завершена.
- `SystemScenariosChangedEvent` — системные сценарии обновлены (в GUI).

### 4.4. Общая база данных

Все три уровня имеют доступ к одной **PostgreSQL** базе данных через EF Core:

| Таблица | Назначение |
|---------|-----------|
| `Bots` | Боты (все версии, AliasGuid + Version + Actual) |
| `BotActivities` | Журнал активности ботов (Start/Stop) |
| `SettingStorage` | Системные настройки (JSON в сжатом виде) |
| `ModelSetStorage` | Версии наборов моделей Model Warehouse |
| `Chats` | Чаты мессенджера (BotId + ForeignChatID) |
| `ChatHistory` | История сообщений чатов |

Конфигурация подключения — файл `Config/database.json`, единый для всех процессов.

---

## 5. Доменная модель

### 5.1. Bot и версионирование

`Bot` — основная сущность. Ключевые поля:
- `Id` (Guid) — уникальный идентификатор записи в БД.
- `AliasGuid` (Guid) — устойчивый идентификатор логического бота (общий для всех версий).
- `Version` (int) — номер версии (инкрементируется при каждом сохранении).
- `Actual` (bool) — флаг актуальной версии.
- `SettingsData` (byte[]) — настройки бота в сжатом JSON (GZip).
- `Settings` (NotMapped, `BotSettings`) — десериализованные настройки.

При сохранении бота текущая запись копируется как историческая (`Actual=false`), а в текущей записи обновляются настройки и инкрементируется версия.

### 5.2. BotSettings

Настройки конкретного бота: Messenger (тип, токен, URL), квалификация (модель, системный промпт, контекстный промпт), квалификация-биз, вложения, сценарии (CodeReports), UseCases, параметры PDF-отчётов, настройки репозиториев и др.

### 5.3. SystemSettings

Общесистемные настройки: SecretNumber, ServiceName, RunAs, каталог моделей (`ModelsPriceData`), коллекция пользовательских сценариев (`CustomAgentScenarios`).

### 5.4. CustomAgentScenario / CustomAgentScenarioStep

Мульти-шаговые сценарии:
- Сценарии организованы в дерево (Parent/ScenarioType=Folder).
- Каждый шаг содержит: модель (ModelAlias), запасную модель (FallbackModelAlias), промпт, ссылку на другой сценарий (ScenarioRefAlias), флаги (IsActive, CheckSuccess, PostToMessenger).
- Экспорт/импорт через файлы `.scenexp` (JSON).

### 5.5. Model Warehouse

- `ModelSourceInfo` — источник модели (alias, provider, techName).
- `ModelCallableSet` — именованный набор моделей для round-robin.
- `ModelSetSettings` — коллекция сетов и source info.
- `ModelSetStorageDto` — версионированная запись в БД (version, comment, current).
- `ModelSetHelper` — сервис: `GetNextModel(setName)` — round-robin выбор.

---

## 6. Дополнительные подсистемы

### 6.1. Квалификация сообщений

Двухэтапная классификация входящих сообщений:
1. **Основная квалификация** — определяет `BotUseCaseEnum` (Biz, Fun, Fraud, Unpolite, Misc, Analyze) через LLM-вызов с системным промптом `KvalSysPrompt`.
2. **Бизнес-квалификация** (если UseCase=Biz) — определяет подтип: BizAnalyzeAttach, BizCodeQuestion, BizCodeAction через `KvalSysPromptBiz`.

Результат квалификации (`KvalSettings`) содержит: Purpose, Attachment, Politeness, Harm, UseCase.

### 6.2. Обработка вложений

- `AttachmentsHolder` — аккумулирует вложения для бота.
- `ReceivedMessagesAttachmentsTracker` — окно ожидания вложений (настраивается в BotSettings).
- `AttachmentsWorkflow` — анализ загруженных вложений через AI.

### 6.3. Логирование

- **Serilog** — основной фреймворк логирования.
- GUI: файл-лог `ActivitySchedulerLogs_N.txt` + in-app панель `GuiLoggerManager`.
- Worker: файл-лог `Employee01_worker_logs_{guid}.txt` в папке `LogsWorker/`.
- Runner: файл-лог `runner_YYYYMMDD_HHMMSS.txt` в per-bot папке `LogsRunner/<BotName>_<AliasGuid>/`.
- Job: per-job логирование через `JobLoggerFactory`.
- Ротация: `LogFilesShrinker` оставляет только 10 последних файлов.

### 6.4. Standalone-приложения

- **ModelWarehouseStandalone** — отдельное WPF-приложение для отладки Model Warehouse без запуска основного GUI.

---

## 7. Структура проектов в Solution

```
BotManager01.sln
│
├── BotManager01.Gui.csproj          — GUI (WPF, главное приложение)
│   ├── App.xaml.cs                  — точка входа, DI, bootstrap
│   ├── Core/                        — WorkerServiceManager, FullStopService, GuiLoggerManager
│   ├── Gui/
│   │   ├── MainWindow/              — главное окно, MainWindowViewModel, SystemScenariosTabViewModel
│   │   ├── BotEditor/               — редактор бота, BotEditorViewModel
│   │   ├── ModelWarehouse/           — Model Warehouse UI, ModelWarehouseViewModel
│   │   └── Dialogs/                 — диалоговые окна (RunAsCredentials, ConfirmCountdown)
│   └── Properties/                  — ресурсы
│
├── BotManager01.WorkerService.TopShelf.csproj — Windows-сервис (Worker)
│   ├── Program.cs                   — Topshelf bootstrap
│   ├── Worker.cs                    — бизнес-логика: таймеры, IPC, управление Runner
│   └── ActivitySchedulerWorkerApp.cs — пути и каталоги Worker
│
├── BotRunner.csproj                 — процесс бота (Runner)
│   ├── Program.cs                   — точка входа, инициализация Messenger
│   ├── AI/                          — ChatGptApiDriver, AiRespondService, OpenAiCostCalculator
│   ├── Messenger/
│   │   ├── Yandex/                  — YandexMessengerManager, YandexMessengerDriver
│   │   ├── TalkManager.cs           — принятие решений об ответе
│   │   ├── ChatsManager.cs          — in-memory состояние чатов
│   │   ├── JobManager.cs            — управление Jobs
│   │   ├── Workflows/               — все Workflow (29 файлов)
│   │   └── Events/                  — события Runner
│   └── Wsl/                         — WSL-интеграция
│       ├── Cursor/                  — CursorAgentWslRunner
│       ├── Codex/                   — CodexAgentWslRunner
│       ├── Pwsh/                    — PwshWslRunner
│       └── Common/                  — WslRunRequest/Response, WslCommandBuilder
│
├── BotManager01.Domain.csproj       — доменные модели и сервисы
│   ├── Models/
│   │   ├── Bots/                    — Bot, BotActivityDto, enums
│   │   ├── Settings/                — BotSettings, SystemSettings, CustomAgentScenario, MessengerSettings
│   │   ├── Messenger/               — ChatDto, ChatHistoryItemDto, Runtime (Chat, ChatMessage)
│   │   └── ModelWarehouse/          — ModelCallableSet, ModelSourceInfo, ModelSetStorageDto
│   ├── Repositories/                — BotRepository, ChatRepository, ModelSetStorageRepository
│   └── Services/                    — BotManager, ModelProviderManager, ModelSetHelper, ModelWarehouseManager
│
├── BotManager01.DataAccess.csproj   — EF Core, PostgreSQL
│   ├── DataAccess/                  — EFPostgresDbContext, EfAsyncRepository, SimpleDbContextFactory
│   ├── Contracts/                   — IAsyncRepositoryT<T>
│   └── Migrations/                  — EF Core миграции
│
├── BotManager01.Shared.Pipes.csproj — IPC (Named Pipes)
│   ├── CommunicationObjects/        — CommunicationMessageBase, NamedPipeCommunicationStation
│   └── Pipe/                        — PipeBase, PipeClient, PipeServer
│
├── BotManager01.Shared.csproj       — общие утилиты
│   ├── Service/                     — Functions, DatabaseConfigProvider, ReactiveMessageBus
│   └── Models/                      — ModelPriceData, GptModelTypeEnum
│
└── ModelWarehouseStandalone.csproj   — отдельное WPF-приложение для Model Warehouse
```

---

## 8. Ключевые архитектурные решения и политики

1. **Процессная изоляция** — каждый бот работает в отдельном процессе (BotRunner.exe). Падение одного бота не влияет на остальных и на Worker.
2. **IPC через Named Pipes** — лёгкий, надёжный механизм без сетевых зависимостей. Каждая пара процессов использует выделенную пару пайпов.
3. **Единая БД** — все уровни читают/пишут в одну PostgreSQL базу. Миграции применяются автоматически при старте каждого процесса.
4. **Версионирование ботов** — каждое сохранение настроек бота создаёт историческую копию. Можно откатиться на любую предыдущую версию.
5. **Сценарии как data** — мульти-шаговые сценарии хранятся в SystemSettings (JSON), а не в коде. Это позволяет создавать, редактировать и экспортировать сценарии без перекомпиляции.
6. **WSL для агентных CLI** — cursor-agent и codex запускаются в WSL, что позволяет использовать Linux-окружение для AI-агентов на Windows-машине.
7. **Reactive Extensions** — внутренняя шина событий на базе `System.Reactive` обеспечивает слабую связанность компонентов.
8. **Serilog everywhere** — единый фреймворк логирования с per-process и per-bot ротацией файлов.
9. **Heartbeat-мониторинг** — Worker и каждый Runner отправляют heartbeat раз в секунду. AliveWatcher в GUI определяет живость компонентов.
10. **Model Warehouse** — версионированное хранилище наборов моделей с round-robin выбором, поддержкой нескольких провайдеров (OpenAI, Claude, DeepSeek, Cursor CLI, Codex CLI).

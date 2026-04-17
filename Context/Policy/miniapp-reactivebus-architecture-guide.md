# Инструкция для генерации приложения на C# / WPF / ASP.NET Core / Blazor с архитектурой MiniApp + ReactiveBus

Ты проектируешь и пишешь приложение на **C#**. Возможны два хоста:

- **WPF desktop application**
- **ASP.NET Core / Blazor application**

Главное архитектурное требование:  
приложение **нельзя** строить как классический большой DI-граф, где сервисы напрямую инжектят друг друга по всему приложению и образуют разрастающийся монолит с огромными конструкторами.

Вместо этого приложение должно строиться по принципу:

- приложение состоит из **MiniApp**
- каждый **MiniApp** — это изолированный функциональный блок
- внутри MiniApp допустим свой внутренний DI и свои внутренние сервисы
- снаружи MiniApp не должен светить всей своей внутренней структурой
- взаимодействие между MiniApp должно идти **не через прямые зависимости**, а **через шину сообщений**
- если нужен более направленный вызов между MiniApp, допускается использовать **Connector**
- для обмена использовать **ReactiveBus** на базе **ReactiveUI / Rx**

---

## 1. Главная идея архитектуры

Каждый крупный кусок бизнес-функциональности оформляется как **MiniApp**.

Примеры MiniApp:

- DataProviderMiniApp
- UserSessionMiniApp
- DocumentEditorMiniApp
- SearchMiniApp
- NotificationsMiniApp
- AuthMiniApp
- ReportsMiniApp

MiniApp — это не просто “еще один сервис”.  
Это **локальная функциональная подсистема** со своими:

- контрактами
- внутренними сервисами
- обработчиками сообщений
- состоянием
- правилами инициализации
- публичными точками интеграции

MiniApp должен выглядеть как **микросервис внутри одного процесса**, но без сетевого взаимодействия.  
Коммуникация между MiniApp строится в первую очередь через **события, команды, запросы, ответы**, проходящие через ReactiveBus.  
Для узких интеграционных сценариев между двумя MiniApp можно вводить **Connector** — маленький адаптерный класс, который знает только публичный контракт другого MiniApp и не тянет его внутренности.

---

## 2. Каких целей нужно достичь

Архитектура должна решать следующие проблемы:

### 2.1. Не допускать раздувания конструкторов
Если сервису нужно 8–15 зависимостей, это признак плохой декомпозиции.  
Вместо этого зависимости надо группировать внутри MiniApp.

### 2.2. Уменьшить связность
Один функциональный блок не должен знать детали реализации другого блока.

### 2.2.1. Сохранить управляемые интеграции
Если между двумя MiniApp нужна более точная координация, она должна идти через маленький Connector или через Bus, а не через прямое сцепление внутренних сервисов.

### 2.3. Ограничить монолитность
Даже если приложение физически одно, логически оно должно состоять из слабо связанных подсистем.

### 2.4. Упростить сопровождение
Новая фича должна добавляться как новый MiniApp или как расширение существующего MiniApp, а не как расползание зависимостей по всему приложению.

### 2.5. Упростить тестирование
MiniApp должен быть тестируем как самостоятельный блок.

---

## 3. Основные правила архитектуры

### 3.1. Запрещено
Запрещено строить приложение так, чтобы:

- любой сервис мог напрямую инжектить любой другой сервис
- ViewModel / Page / Component тянули пол-application через конструктор
- ApplicationService выступал “бог-объектом”
- один сервис знал внутренности другого MiniApp
- обмен между подсистемами строился на прямых вызовах методов по всему коду
- Connector превращался в обход границ MiniApp и тащил внутренние сервисы чужого модуля

### 3.2. Разрешено
Разрешено:

- внутри MiniApp иметь внутренние сервисы и внутренний DI
- снаружи публиковать только ограниченный контракт
- общаться между MiniApp через шину сообщений
- использовать маленькие Connector-классы для адресного взаимодействия между MiniApp
- подписываться на типизированные сообщения Rx
- иметь отдельные message contracts для команд, событий, запросов и ответов
- иметь отдельные корневые app-level сервисы в `Program.cs` / `App.xaml.cs`, если они действительно общие, инфраструктурные или orchestration-level и не размывают границы MiniApp

---

## 4. Что такое MiniApp

Каждый MiniApp должен иметь примерно такую структуру:

- **Module / Registration**
- **Public contract**
- **Public connectors** (если нужны)
- **Internal services**
- **Message handlers**
- **State holder**
- **Facade / Entry point**
- **Message contracts**

Примерно так:

```text
MiniApps/
 └── DataProviderMiniApp/
     ├── Contracts/
     │   ├── IDataProviderMiniApp.cs
     │   ├── Connectors/
     │   │   ├── IDataProviderConnector.cs
     │   ├── Messages/
     │   │   ├── DataRequested.cs
     │   │   ├── DataLoaded.cs
     │   │   ├── DataLoadFailed.cs
     ├── Connectors/
     │   ├── DataProviderConnector.cs
     ├── Internal/
     │   ├── DataProviderService.cs
     │   ├── DataQueryExecutor.cs
     │   ├── DataProviderState.cs
     │   ├── DataProviderMessageHandler.cs
     ├── Registration/
     │   ├── DataProviderMiniAppRegistration.cs
     └── Facade/
         ├── DataProviderMiniApp.cs
```

---

## 5. Правила DI

### 5.1. DI используется, но локально и осмысленно
DI-контейнер нужен, но не как “всем всё доступно”.

Нужно соблюдать правило:

- **внешний уровень** знает только MiniApp как целое
- **внутренний уровень** MiniApp знает свои внутренние сервисы

### 5.2. На верхнем уровне Program.cs / App.xaml.cs / Startup
На верхнем уровне регистрируются:

- ReactiveBus
- общие инфраструктурные сервисы
- общие app-level orchestrator / coordinator / bootstrap сервисы
- общие Connector-регистрации, если они используются как публичные адаптеры между MiniApp
- MiniApp-регистрации

Пример идеи:

```csharp
services.AddReactiveBus();

services.AddSingleton<IAppClock, SystemClock>();
services.AddSingleton<IUserContextAccessor, UserContextAccessor>();
services.AddSingleton<IAuthConnector, AuthConnector>();

services.AddMiniApp<DataProviderMiniApp>();
services.AddMiniApp<UserSessionMiniApp>();
services.AddMiniApp<SearchMiniApp>();
services.AddMiniApp<DocumentEditorMiniApp>();
```

Не надо регистрировать на верхнем уровне все внутренние кишки каждого модуля как публично используемые зависимости.  
Но **можно** держать на верхнем уровне отдельные “висящие” сервисы, если это:

- общий инфраструктурный сервис
- общий app-level orchestration service
- bootstrap / startup coordinator
- connector между MiniApp
- cross-cutting abstraction, которая не принадлежит одному конкретному MiniApp

Запрещено не наличие корневых сервисов само по себе, а превращение верхнего уровня в бесконтрольный монолит.

### 5.3. Нельзя делать так
Плохо:

```csharp
public class HugeService
{
    public HugeService(
        IDataProvider dataProvider,
        IUserSession userSession,
        ISearchService searchService,
        INotificationService notifications,
        IAuthService auth,
        ICacheService cache,
        IReportService reports,
        ISettingsService settings,
        ILoggingService logging)
    {
    }
}
```

Это признак того, что сервис стал центром монолита.

### 5.4. Надо делать так
Лучше:

- либо выделить отдельный MiniApp
- либо внедрить 1 фасад MiniApp вместо 8 мелких зависимостей
- либо внедрить 1 Connector вместо знания чужого MiniApp изнутри
- либо перейти на обмен сообщениями через Bus

### 5.5. Когда уместен отдельный корневой сервис
Отдельный сервис на уровне `Program.cs` / `App.xaml.cs` допустим, если он:

- не содержит доменную логику нескольких MiniApp вперемешку
- не знает внутренние классы MiniApp
- не становится универсальным “application brain”
- решает общую техническую или orchestration-задачу
- имеет маленький и понятный контракт

Хорошие примеры:

- `AppStartupCoordinator`
- `AppClock`
- `CurrentUserAccessor`
- `NavigationStateStore`
- `ConnectorRegistry`

Плохие примеры:

- `EverythingService`
- `GlobalBusinessLogicManager`
- `UniversalWorkflowService`, который напрямую дёргает половину приложения

---

## 6. ReactiveBus: принцип взаимодействия

ReactiveBus — это центральная внутренняя шина сообщений приложения.

Она нужна для:

- публикации событий
- отправки команд
- реактивных подписок
- развязки модулей

### 6.1. Типы сообщений
Разделяй сообщения на 4 группы:

#### Commands
Сообщения вида “сделай действие”.

Примеры:

- LoadUserProfileCommand
- RefreshDataCommand
- SaveDocumentCommand

#### Events
Сообщения вида “что-то уже произошло”.

Примеры:

- UserLoggedInEvent
- DataLoadedEvent
- DocumentSavedEvent

#### Requests
Сообщения вида “нужны данные / требуется операция”.

Примеры:

- GetDataRequest
- ResolveUserPermissionsRequest

#### Responses
Ответы на Requests.

Примеры:

- GetDataResponse
- ResolveUserPermissionsResponse

### 6.2. Где Connector, а где Bus
По умолчанию межмодульная коммуникация идёт через Bus.  
Connector нужен не вместо Bus вообще, а как **точечный интеграционный адаптер**.

Используй Connector, когда:

- одному MiniApp нужен маленький публичный capability другого MiniApp
- нужен синхронный или request/response-подобный вызов без разворачивания полноценного message workflow
- не хочется светить facade целиком ради 1-2 операций
- важно скрыть транспорт и детали вызова за компактным контрактом

Используй Bus, когда:

- на событие должны реагировать несколько MiniApp
- нужен fan-out
- важна реактивность и слабая связность
- это workflow, а не точечный capability call

---

## 7. Как должен работать DataProvider

DataProvider — хороший пример MiniApp.

Он не должен превращаться в объект, который все напрямую вызывают через методы.

Вместо этого:

- кто-то публикует запрос данных
- DataProviderMiniApp подписан на этот запрос
- он выполняет работу
- он публикует ответ / событие / ошибку

Пример логики:

1. Кто-то публикует `DataRequestedMessage`
2. DataProviderMiniApp принимает сообщение
3. Выполняет загрузку
4. Публикует:
   - `DataLoadedMessage`, если успех
   - `DataLoadFailedMessage`, если ошибка

---

## 8. Правило границ MiniApp

Каждый MiniApp должен иметь четкую границу.

### Снаружи MiniApp видно только:
- его регистрацию
- его публичные message contracts
- его публичные connectors, если они есть
- иногда его facade / public API, если это действительно нужно

### Внутри MiniApp скрыто:
- конкретная реализация
- внутренние сервисы
- внутреннее состояние
- детали orchestration

Нельзя, чтобы один MiniApp лазил во внутренние классы другого MiniApp.

---

## 9. Когда можно прямую зависимость, а когда только Bus

### Прямую зависимость можно:
- для инфраструктурных сервисов
- для низкоуровневых технических сервисов
- внутри одного MiniApp
- для вещей типа logger, clock, serializer, config, file system abstraction
- для connector-контрактов, если они обращаются только к публичному API чужого MiniApp
- для небольших корневых app-level сервисов, если они не размывают модульные границы

### Через Bus надо:
- общение между MiniApp
- кросс-функциональные уведомления
- реакцию на изменение состояния
- обмен, где важна слабая связность
- workflow между подсистемами

### Через Connector надо:
- адресный вызов одного MiniApp из другого
- сценарии “нужна одна операция, а не общий event flow”
- инкапсуляцию интеграции с чужим MiniApp за маленьким контрактом

Connector не должен заменять целиком message-модель системы.  
Это вспомогательный слой для точечных связей.

---

## 10. Не превращай Bus в помойку

Важно: ReactiveBus не должен становиться хаотичной свалкой.

Поэтому:

- все сообщения должны быть **строго типизированными**
- имена должны быть предметными
- payload должен быть явным
- не использовать “универсальное сообщение object”
- не передавать безликие словари
- не делать одно сообщение “на все случаи жизни”

Плохо:

```csharp
public class GenericMessage
{
    public string Type { get; set; }
    public object Payload { get; set; }
}
```

Хорошо:

```csharp
public sealed record DataRequested(Guid RequestId, string EntityType, string EntityId);
public sealed record DataLoaded(Guid RequestId, string EntityType, string EntityId, object Data);
public sealed record DataLoadFailed(Guid RequestId, string EntityType, string EntityId, string Error);
```

---

## 11. Рекомендуемая структура кода

Нужна структура примерно такого типа:

```text
src/
 ├── Host/
 │   ├── WpfHost/
 │   └── WebHost/
 ├── Infrastructure/
 │   ├── ReactiveBus/
 │   ├── Logging/
 │   ├── Configuration/
 │   └── Persistence/
 ├── Connectors/
 │   ├── Abstractions/
 │   └── Registration/
 ├── MiniApps/
 │   ├── DataProviderMiniApp/
 │   ├── UserSessionMiniApp/
 │   ├── SearchMiniApp/
 │   ├── NotificationsMiniApp/
 │   └── DocumentEditorMiniApp/
 ├── Shared/
 │   ├── Abstractions/
 │   ├── CommonMessages/
 │   ├── Result/
 │   └── Exceptions/
```

---

## 12. Что должно быть у каждого MiniApp

У каждого MiniApp желательно сделать:

### 12.1. Registration extension
Например:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataProviderMiniApp(this IServiceCollection services)
    {
        services.AddSingleton<DataProviderState>();
        services.AddSingleton<IDataProviderMiniApp, DataProviderMiniApp>();
        services.AddSingleton<IDataProviderConnector, DataProviderConnector>();
        services.AddSingleton<DataProviderMessageHandler>();
        services.AddSingleton<IDataQueryExecutor, DataQueryExecutor>();
        services.AddSingleton<IDataProviderService, DataProviderService>();

        return services;
    }
}
```

### 12.2. Инициализацию подписок
Подписки на Bus должны быть централизованы, а не размазаны в случайных местах.

Например, внутри `DataProviderMessageHandler` или `DataProviderMiniApp`.

### 12.3. Собственное состояние
Если MiniApp хранит состояние, пусть оно будет собрано в отдельном state object, а не раскидано по сервисам.

### 12.4. Явные contracts/messages
Все внешние сообщения MiniApp должны быть оформлены в Contracts/Messages.

### 12.5. Публичный Connector при необходимости
Если MiniApp предоставляет небольшой интеграционный capability для других MiniApp, можно оформить его отдельным connector-контрактом.

Например:

```csharp
public interface IDataProviderConnector
{
    Task<EntityData?> TryGetEntityDataAsync(Guid entityId, CancellationToken cancellationToken);
}
```

Важно:

- Connector маленький
- Connector работает только через публичный контракт MiniApp
- Connector не открывает наружу внутренние сервисы
- Connector не тащит наружу state object и внутреннюю orchestration-логику

---

## 13. Роли классов внутри MiniApp

### MiniApp Facade
Точка входа MiniApp.  
Отвечает за orchestration, инициализацию, запуск подписок.

### MessageHandler
Слушает Bus, маршрутизирует сообщения во внутренние сервисы.

### Internal Service
Содержит бизнес-логику.

### State
Хранит состояние MiniApp.

### Contracts
Определяет, чем MiniApp обменивается с внешним миром.

### Connector
Маленький публичный адаптер для адресного взаимодействия с другими MiniApp.  
Обычно скрывает либо facade, либо message roundtrip, либо интеграционную обвязку.

---

## 14. Для WPF

В WPF:

- ViewModel не должна знать весь мир
- ViewModel должна работать либо:
  - со своим MiniApp facade
  - либо с Bus
- UI не должен напрямую orchestrate кучу сервисов

Подход:

- UI публикует команду в Bus
- MiniApp реагирует
- MiniApp публикует событие/результат
- ViewModel подписывается на нужные события

Например:

- пользователь нажал кнопку
- ViewModel публикует `LoadReportCommand`
- ReportsMiniApp обрабатывает
- публикует `ReportLoadedEvent`
- ViewModel получает и обновляет UI

---

## 15. Для ASP.NET Core / Blazor

В Blazor / ASP.NET Core:

- Page / Component / Controller не должен напрямую собирать бизнес-граф из 10 сервисов
- он должен работать через:
  - MiniApp facade
  - Connector
  - Bus
  - Application-level orchestrator, если это очень нужно

Компонент должен быть тонким.

---

## 16. Ограничения на конструкторы

В генерируемом коде соблюдай правила:

- если у класса больше 4–5 зависимостей — это подозрительно
- если у класса 7+ зависимостей — почти наверняка нужна декомпозиция
- если сервис начинает тащить слишком много разных доменных ролей — выделяй новый MiniApp

---

## 17. Внутренний шаблон мышления при проектировании

Когда проектируешь новую функциональность, рассуждай так:

### Шаг 1
Это просто локальная логика внутри существующего MiniApp  
или это новый изолируемый функциональный блок?

### Шаг 2
Нужен ли прямой вызов  
или лучше событие / команда через Bus?

### Шаг 3
Не начнет ли новый сервис раздувать конструкторы других классов?

### Шаг 4
Не ломает ли решение границы модулей?

### Шаг 5
Можно ли скрыть детали за facade MiniApp?

---

## 18. Чего делать не надо

Не надо генерировать архитектуру, где:

- сервисы напрямую сцеплены друг с другом
- бизнес-логика размазана между UI и сервисами
- Bus используется бессистемно
- один MiniApp знает слишком много о другом
- Connector используется как backdoor во внутренности другого MiniApp
- все регистрации и orchestration сваливаются в один гигантский `Program.cs`
- MiniApp — только папка, а не реальная изолированная подсистема

---

## 19. Что нужно генерировать вместо этого

Нужно генерировать:

- четкие MiniApp
- их регистрации
- их сообщения
- их connectors, если нужны точечные интеграции
- их внутренние сервисы
- facade/entry point
- message handlers
- state containers
- тонкий UI
- взаимодействие через ReactiveBus и/или Connector, где это оправдано

---

## 20. Практическое правило проектирования

Если новая функция требует:

- много зависимостей
- отдельного состояния
- набора реакций на события
- нескольких сценариев взаимодействия
- слабой связности с остальной системой

то это кандидат на **отдельный MiniApp**.

---

## 21. Желаемый стиль кода

Код должен быть:

- чистым
- модульным
- расширяемым
- без бог-объектов
- без сервис-локатора
- без хаотичного DI-графа
- с понятными именами сообщений
- с явными границами подсистем

Используй:

- `record` для сообщений
- интерфейсы только там, где они реально дают смысл
- extension methods для регистрации
- Rx / ReactiveUI для подписок и стримов
- композицию вместо раздувания сервисов

---

## 22. Что нужно выдать при генерации

Когда ты генерируешь приложение или фичу в этой архитектуре, ты должен:

1. определить границы MiniApp
2. определить его public contracts
3. определить message contracts
4. определить connectors, если нужны адресные интеграции
5. определить внутренние сервисы
6. определить state object
7. определить message handler / subscriptions
8. определить регистрацию в DI
9. показать, как UI или другой MiniApp взаимодействует с ним через Bus или Connector

---

## 23. Краткая формула архитектуры

Формула такая:

- **DI — для внутренней сборки MiniApp**
- **Bus — основной способ общения между MiniApp**
- **Connector — точечный способ адресного общения между MiniApp**
- **UI — тонкий**
- **MiniApp — изолированный**
- **конструкторы — короткие**
- **зависимости — локализованные**
- **корневые app-level сервисы — допустимы, если они маленькие и оправданные**
- **архитектура — модульная, а не монолитная**

---

## 24. Дополнительное указание

Если в ходе генерации будет выбор между:

- “проще прямо заинжектить еще 3 сервиса”
- или
- “выделить границу MiniApp и отправить сообщение через Bus”

то по умолчанию предпочитай **MiniApp + Bus**, если это межмодульное взаимодействие.

Если же взаимодействие адресное, компактное и не требует fan-out, можно предпочесть **MiniApp + Connector**.

---

## 25. Итоговое требование

Генерируй приложение так, чтобы оно выглядело не как “один монолитный DI-граф”, а как набор **внутрипроцессных модулей-подсистем**, связанных в основном через **реактивную шину сообщений**, а в точечных сценариях — через **маленькие Connector-адаптеры**.

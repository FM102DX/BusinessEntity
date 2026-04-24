# Политика хранения данных

## 1. Назначение документа

Этот документ фиксирует фактическую модель хранения бизнес-объектов в системе `BusinessEntity`.

Документ описывает:

- базовые runtime-сущности
- DTO-слой хранения
- физическое хранение в Postgres
- канонический формат payload `BusinessEntityData`
- цепочку записи документа
- разделение основной базы и базы логгера

Документ является нормативным описанием текущего storage-контура. При изменении модели хранения он должен обновляться.

---

## 2. Базовые runtime-сущности

В текущей бизнес-модели есть три базовые runtime-сущности:

1. `BusinessEntity`
2. `BusinessEntityData`
3. `BusinessEntityRelation`

### `BusinessEntity`

`BusinessEntity` представляет сам объект в системе.

Это:

- узел дерева
- identity объекта
- объект, участвующий в связях
- runtime-модель, которая хранится как `BusinessEntityDto`

`BusinessEntity` содержит:

- `Id`
- `CreatedDate`
- `LastModifiedDate`
- `Name`
- `BusinessEntityType`
- `EntityType`

`BusinessEntity` может существовать без payload.

Примеры:

- `Space`
- `Folder`

### `BusinessEntityData`

`BusinessEntityData` представляет payload-часть объекта.

Это:

- подчиненный объект по отношению к `BusinessEntity`
- runtime-модель содержимого data-backed сущности
- базовый тип для тяжеловесных бизнес-объектов

Он содержит:

- `Id`
- `CreatedDate`
- `LastModifiedDate`
- `Name`
- `EntityType`
- `Tag`

`BusinessEntityData` не является самостоятельным корневым объектом.

### `BusinessEntityRelation`

`BusinessEntityRelation` представляет связь между двумя `BusinessEntity`.

Она содержит:

- `Id`
- `CreatedDate`
- `LastModifiedDate`
- `ObjectAId`
- `ObjectBId`
- `RelationType`
- `RelationParams`

---

## 3. Специализированные runtime-типы

### `Document`

`Document` является наследником `BusinessEntityData`.

В текущей модели документ содержит:

- `Text`
- `Tag`
- базовые поля `BusinessEntityData`

### `BusinessEntity<T>`

В системе существует обобщенный runtime-агрегат:

- `BusinessEntity<T> where T : IBusinessEntityData`

Его назначение:

- держать рядом базовую entity и typed payload
- упростить runtime-работу с data-backed объектами

Пример:

- `BusinessEntity<Document>`

---

## 4. DTO-слой хранения

В storage-слое используются ровно три DTO:

1. `BusinessEntityDto`
2. `BusinessEntityDataDto`
3. `BusinessEntityRelationDto`

Соответствие runtime -> storage:

- `BusinessEntity` <-> `BusinessEntityDto`
- `BusinessEntityData` <-> `BusinessEntityDataDto`
- `BusinessEntityRelation` <-> `BusinessEntityRelationDto`

### `BusinessEntityDto`

Хранит метаданные сущности:

- `Id`
- `CreatedDate`
- `LastModifiedDate`
- `Name`
- `BusinessEntityType`
- `EntityType`

### `BusinessEntityDataDto`

Хранит payload сущности:

- `Id`
- `CreatedDate`
- `LastModifiedDate`
- `BusinessEntityId`
- `Data : string`

Важно:

- `BusinessEntityDataDto.Data` хранит не raw text и не `byte[]`
- `Data` хранит minified JSON string
- JSON string обязан иметь форму versioned envelope

Канонический формат:

```json
{"schemaVersion":1,"kind":"Document","payload":{"text":"Документ","tag":"Document"}}
```

### `BusinessEntityRelationDto`

Хранит связь между сущностями:

- `Id`
- `CreatedDate`
- `LastModifiedDate`
- `ObjectAId`
- `ObjectBId`
- `RelationType`
- `RelationParams`

---

## 5. Физическое хранение в Postgres

### Разделение баз данных

Основное приложение и веб-логгер используют один Postgres-сервер, но разные базы данных.

Текущее разделение:

- `business_entity` — основная база приложения `BusinessEntity`
- `web_logger` — отдельная база `BlazorServerWebLogger`

Это обязательное правило архитектуры:

- бизнес-объекты не должны храниться в базе логгера
- таблицы логгера не должны смешиваться с таблицами `BusinessEntity`

### Таблицы основной базы `business_entity`

В основной базе используются три таблицы:

1. `BusinessEntities`
2. `BusinessEntityRelations`
3. `BusinessEntityDataItems`

Соответствие:

- `BusinessEntities` <- `BusinessEntityDto`
- `BusinessEntityRelations` <- `BusinessEntityRelationDto`
- `BusinessEntityDataItems` <- `BusinessEntityDataDto`

### Таблицы базы логгера `web_logger`

В базе логгера живут только таблицы логгера.

Например:

- `LogEntries`
- `AppSettingsDbStorable`

Они не относятся к storage-контуру бизнес-объектов.

---

## 6. Идентичность и подчиненность data-объекта

`BusinessEntityData` подчинен `BusinessEntity`.

Фактические правила текущей системы:

- `BusinessEntityData.Id == BusinessEntity.Id`
- `BusinessEntityDataDto.Id == BusinessEntity.Id`
- `BusinessEntityDataDto.BusinessEntityId == BusinessEntity.Id`

Payload-объект не имеет собственной независимой identity.

---

## 7. Канонический формат payload

### Общее правило

Payload хранится отдельно от `BusinessEntity` в `BusinessEntityDataDto.Data`.

Payload не лежит в `BusinessEntityDto`.

Payload не хранится как `byte[]`.

### Канонический формат

Канонический формат хранения payload — **Versioned JSON Envelope**.

Обязательная структура:

```json
{
  "schemaVersion": 1,
  "kind": "<LogicalPayloadKind>",
  "payload": { ... }
}
```

Где:

- `schemaVersion` — версия схемы хранимого payload
- `kind` — логический discriminator, независимый от CLR type name
- `payload` — сам сериализованный бизнес-объект

### Требования к формату

Нужно обеспечивать:

- minified JSON без форматирования
- читаемый Unicode в БД без принудительного `\uXXXX` escaping для кириллицы и другого non-ASCII текста
- стабильные ключи `schemaVersion`, `kind`, `payload`
- отсутствие double-encoding
- отсутствие зависимости от полного CLR type name
- готовность к повышению `schemaVersion`

### Централизованная JSON-сериализация

Storage-контур использует единые `StorageJsonOptions`.

Фактические правила текущей реализации:

- envelope сериализуется через `JsonSerializer.Serialize(..., StorageJsonOptions.Default)`
- raw payload сериализуется через `JsonSerializer.Serialize(..., StorageJsonOptions.Default)`
- envelope десериализуется через `JsonSerializer.Deserialize(..., StorageJsonOptions.Default)`
- payload десериализуется через `JsonSerializer.Deserialize(..., StorageJsonOptions.Default)`

`StorageJsonOptions.Default` задает:

- `Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)`
- `WriteIndented = false`
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- `DefaultIgnoreCondition = JsonIgnoreCondition.Never`

Это обязательная часть текущего storage-формата, потому что JSON в БД должен оставаться одновременно:

- minified
- валидным
- читаемым глазами
- независимым от `byte[]`-pipeline

### Как трактуется `kind`

`kind` — это не полное имя .NET-типа.

Правильно:

- `Document`
- `FolderData`
- `WikiPage`

Неправильно:

- `My.Namespace.Document, MyAssembly`

---

## 8. Как сейчас выполняется запись payload

### Общий принцип

Новый канонический pipeline:

```text
runtime object -> raw JSON -> versioned JSON envelope string -> DB
```

Payload сохраняется через `DataProviderMiniApp`.

### Порядок записи

1. `BusinessEntityHelper` вызывает `IDataProviderConnector.UpdateDataAsync(id, data)`
2. `DataProviderConnector` сериализует runtime-данные через `JsonSerializer.Serialize(...)`
3. `DataProviderMessageHandler` передает raw JSON в `DataProviderService`
4. `DataProviderService` читает `BusinessEntityDto`, определяет логический `kind`
5. `DataProviderService` заворачивает raw JSON в versioned envelope
6. строка envelope сохраняется в `BusinessEntityDataDto.Data`

Во всех шагах JSON-serialization используется `StorageJsonOptions.Default`.

### Специальный случай документа

Сейчас документ выше по стеку по-прежнему приходит как `string`.

Поэтому внутри `DataProviderMiniApp` при записи документа raw JSON-строка преобразуется в object-payload вида:

```json
{"text":"Документ","tag":"Document"}
```

После этого payload заворачивается в envelope:

```json
{"schemaVersion":1,"kind":"Document","payload":{"text":"Документ","tag":"Document"}}
```

---

## 9. Как сейчас выполняется чтение payload

Чтение идет в две стадии:

1. из `BusinessEntityDataDto.Data` читается JSON-envelope строка
2. envelope десериализуется в raw-model
3. проверяется `schemaVersion`
4. проверяется наличие `kind`
5. из поля `payload` выполняется десериализация в нужный runtime-тип

### Специальный случай документа

Для вызова `GetDataAsync<string>(id)` у документа storage-слой извлекает `payload.text`.

То есть runtime-код выше по стеку продолжает получать текст документа как `string`, хотя в БД уже лежит object-payload внутри envelope.

Чтение envelope и payload также использует те же `StorageJsonOptions.Default`, что и запись.

---

## 10. Как сейчас создается документ

Документ в текущей системе создается как три независимые storage-записи.

Алгоритм:

1. создается `BusinessEntity` типа `Document`
2. создается `BusinessEntityRelation` типа `Contains` между родителем и документом
3. сохраняется payload документа в `BusinessEntityDataDto`

### Runtime-порядок вызовов

Упрощенная цепочка:

`SampleDataService -> BusinessEntityHelper -> DataProviderConnector -> DataProviderMessageHandler -> DataProviderService -> EF/Postgres repository -> Postgres`

### Что записывается в БД

Для документа появляются:

1. запись в `BusinessEntities`
2. запись в `BusinessEntityRelations`
3. запись в `BusinessEntityDataItems`

Для папки:

1. запись в `BusinessEntities`
2. запись в `BusinessEntityRelations`

Для пространства:

1. запись только в `BusinessEntities`

---

## 11. Где живет storage-логика

Вся storage-логика живет внутри `DataProviderMiniApp`.

Это включает:

- `DataProviderConnector`
- `DataProviderMessageHandler`
- `DataProviderService`
- `DataPayloadEnvelopeSerializer`
- `DataProviderMapper`
- репозитории DTO

Слои разделены так:

- `BusinessEntityHelper` — бизнес-операции
- `DataProviderConnector` — bus request/response facade
- `DataProviderMessageHandler` — подписка на storage messages
- `DataProviderService` — прикладной CRUD DTO-слоя
- `IAsyncRepository<T>` — абстракция репозитория
- `EfPostgresAsyncRepositoryBase<T>` — текущая Postgres-реализация

---

## 12. Текущая реализация репозиториев

На текущий момент `DataProviderMiniApp` использует Postgres-репозитории.

В DI зарегистрированы:

- `BusinessEntityDtoEfPostgresRepository`
- `BusinessEntityDataDtoEfPostgresRepository`
- `BusinessEntityRelationDtoEfPostgresRepository`

Для CRUD-операций используется свежий `KmsBusinessEntityDbContext` на каждую операцию.

Это сделано для того, чтобы:

- не накапливать tracked entities между вызовами
- избегать конфликтов EF tracking при сидировании и runtime-операциях

---

## 13. Инициализация схемы

При старте `BusinessEntity` приложение явно гарантирует наличие таблиц:

- `BusinessEntities`
- `BusinessEntityRelations`
- `BusinessEntityDataItems`

Для `BusinessEntityDataItems.Data` используется текстовое поле `text`.

Причина:

- payload хранится как JSON string
- схема должна соответствовать string-based envelope storage

---

## 14. Текущая семантика связей

В дереве используется базовый тип связи:

- `Contains`

Тип `VisuallyContains` выведен из системы.

Следствие:

- дерево документов и папок строится на `Contains`
- удаление поддерева идет по `Contains`
- поиск детей идет по `Contains`
- смена родителя идет по `Contains`

---

## 15. Что считается правильным storage-повторением объекта

### Простая сущность

Пример:

- `Space`

Хранение:

- только `BusinessEntityDto`

### Контейнерная сущность

Пример:

- `Folder`

Хранение:

- `BusinessEntityDto`
- `BusinessEntityRelationDto`

### Data-backed сущность

Пример:

- `Document`

Хранение:

- `BusinessEntityDto`
- `BusinessEntityRelationDto`
- `BusinessEntityDataDto`

---

## 16. Ограничения и правила

Запрещено:

- хранить payload внутри `BusinessEntityDto`
- хранить payload как `byte[]` как канонический формат
- сериализовать payload в UTF-8 bytes как основной storage pipeline
- смешивать таблицы логгера и бизнес-объектов в одной базе данных
- использовать `BusinessEntityData` как независимую identity
- хранить связи внутри payload вместо `BusinessEntityRelation`

Допустимо:

- иметь `BusinessEntity` без `BusinessEntityData`
- хранить payload как minified JSON string
- расширять payload новыми typed-наследниками `BusinessEntityData`
- повышать `schemaVersion` и вводить адаптеры старых версий

---

## 17. Практический итог

На текущий момент система хранения устроена так:

- runtime-модель разделена на `BusinessEntity`, `BusinessEntityData`, `BusinessEntityRelation`
- storage-модель разделена на `BusinessEntityDto`, `BusinessEntityDataDto`, `BusinessEntityRelationDto`
- основное приложение хранит данные в базе `business_entity`
- веб-логгер хранит данные в базе `web_logger`
- payload `BusinessEntityData` хранится как minified JSON string
- канонический формат payload — versioned JSON envelope
- дерево и граф документов строятся через relation типа `Contains`
- запись и чтение идут через `DataProviderMiniApp`

Это и есть текущий канонический контур хранения бизнес-объектов в проекте.

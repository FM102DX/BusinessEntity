# Политика restore пространства из backup

Документ описывает, как восстанавливать пространство из backup, созданного по `entity-folder layout`.

Базовый сценарий restore:

```text
backup пространства A
        |
        v
создать новое пространство B
        |
        v
восстановить entities, relations, data, chunks, files
```

Restore в существующее пространство, merge двух пространств и "починка" пространства на месте пока не рассматриваются.

---

## 1. Главные решения

### 1.0. Space не является отдельной БД

`Space` не должен выделяться в отдельную PostgreSQL database.

Бизнес-позиционирование системы - небольшие команды, где несколько пространств живут в одной рабочей установке и обслуживаются как единый граф `BusinessEntity` + relations.

Это не временный технический компромисс restore-механики, а текущая продуктовая и архитектурная фиксация: схемы БД, `DataProvider`, backup/restore, UI администрирования и миграции не должны исходить из модели `1 Space = 1 DB`.

Поэтому текущая архитектурная позиция:

```text
одна installation/application DB
    содержит много Spaces
        каждый Space содержит свой подграф BusinessEntity
```

Изоляция на уровне отдельной БД может появиться позже для сущности более высокого уровня - `Tenant`.

```text
Tenant A -> отдельная DB
    Space 1
    Space 2

Tenant B -> отдельная DB
    Space 3
```

Но границы `Tenant`, правила миграций, каталога tenant-ов и cross-tenant ограничений пока не зафиксированы. До появления отдельной tenant-политики restore пространства должен исходить из того, что все spaces текущего tenant/application живут в одной БД.

Если позже появится физическая изоляция на уровне tenant database, она должна вводиться отдельной политикой `Tenant`, а не через изменение смысла `Space`.

Следствие: restore пространства обязан избегать ID-collisions внутри общей БД, поэтому базовая модель restore использует `RestoreIdMap`, а не прямое сохранение старых GUID.

### 1.1. Restore всегда создает новое пространство

Импорт backup не должен пытаться записывать данные обратно в исходное пространство.

Причины:

- в той же БД исходное пространство может продолжать существовать;
- старые `Guid` из backup могут конфликтовать с уже существующими строками;
- merge relations/properties/data versions сильно сложнее, чем полный импорт в новый root;
- пользователь должен иметь возможность сравнить старое и восстановленное пространство.

Поэтому результат restore - новый `Space` с новым `Id`.

### 1.2. Режим identity по умолчанию - remap

В обычном restore все persistent DB IDs создаются заново.

```text
old id из backup  ->  new id в текущей БД
```

Старые IDs не теряются: restore строит `RestoreIdMap`, а также может сохранить исходные IDs в технических properties/metadata восстановленных объектов.

Режим "preserve original ids" допустим только как будущий аварийный режим для пустой БД, когда гарантированно нет конфликтов.

### 1.3. Человеко-читаемые файлы не являются источником restore

Для restore используются canonical JSON и canonical files:

```text
manifest.json
entities/*/entity.json
entities/*/entity-properties.json
entities/*/data/*
entities/*/files/*
relations/index.json
relations/by-entity/*
```

Файлы вида:

```text
{entityName}--human-readable.md
{entityName}--human-readable.html
attachments/
```

являются диагностическим/человеческим export и не должны быть canonical source для restore.

---

## 2. RestoreIdMap

Restore начинается с построения карты соответствий старых и новых IDs.

Минимальная структура:

```json
{
  "restoreSessionId": "7f1e6e8c-7c3c-456e-8d5b-37d8a783c9e2",
  "sourceBackupRoot": "C:\\Backups\\Space--...",
  "sourceSpaceId": "e53cf5a7-11c6-4e13-8cb5-10370860059e",
  "targetSpaceId": "d4d3e8de-6d6a-44aa-8e02-94af63998791",
  "entities": {
    "oldEntityId": "newEntityId"
  },
  "dataItems": {
    "oldDataId": "newDataId"
  },
  "chunks": {
    "oldChunkId": "newChunkId"
  },
  "properties": {
    "oldPropertyId": "newPropertyId"
  },
  "relations": {
    "oldRelationId": "newRelationId"
  }
}
```

Карта существует минимум на время restore. Для диагностики ее можно сохранить в restore report.

---

## 3. Что remap-ится

| Объект | Restore policy |
|---|---|
| `Space` | создается новый `Space.Id`; имя задается пользователем или строится из backup manifest |
| `BusinessEntity` | создается новый `Id`; `EntityType`, имя, даты, тип сохраняются |
| `BusinessEntityData` | создается новый `Id`; `BusinessEntityId` remap-ится; `Version` сохраняется |
| `BusinessEntityDataChunk` | создается новый logical `Id`; `BusinessEntityId` remap-ится; `Version` и `SortOrder` сохраняются |
| Entity/Data/Chunk properties | создаются новые `Id`; `ParentEntityId` remap-ится на новый parent |
| Relations | создается новый relation `Id`; source/target endpoints remap-ятся |
| Rich-doc `imageId` | сохраняется как document-local id, потому что он scoped внутри документа |
| Физические файлы entity | копируются в storage folder нового entity id |
| Human-readable attachments | не участвуют в restore |
| Users/UserProperties | не входят в space restore, если отдельная политика явно не добавит их позже |

Важно: для versioned chunks все строки одного старого logical chunk должны получить один и тот же новый logical chunk id.

```text
old C1 v1 -> new C9 v1
old C1 v2 -> new C9 v2
old C1 v3 -> new C9 v3
```

---

## 4. Общая схема restore

```text
             backup root
                 |
                 v
        [1] validate manifest
                 |
                 v
        [2] read entities index
                 |
                 v
        [3] build RestoreIdMap
                 |
                 v
        [4] create new Space shell
                 |
                 v
        [5] restore entity shells
                 |
                 v
        [6] restore entity data/properties/files
                 |
                 v
        [7] restore relations
                 |
                 v
        [8] rebuild derived caches/indexes
                 |
                 v
        [9] verify and publish result
```

---

## 5. Этапы restore

### 5.1. Validate backup

Restore не стартует, если:

- нет `manifest.json`;
- `manifest.isComplete != true`;
- backup layout version не поддерживается текущей программой;
- отсутствуют обязательные folders `entities/` или `relations/`;
- entity folder не содержит `entity.json`;
- relation ссылается на entity, которой нет в backup;
- JSON не парсится.

Restore может стартовать с предупреждениями, если отсутствуют human-readable файлы, потому что они не canonical.

### 5.2. Build import plan

Restore service читает:

```text
manifest.json
entities/*/entity.json
entities/*/data/data-manifest.json
relations/index.json
```

На этом этапе создается полный `RestorePlan`:

- какой source space восстанавливаем;
- какое новое имя будет у target space;
- список entities;
- список relations;
- список files;
- old->new id mappings;
- список предупреждений.

### 5.3. Create new Space

Создается новый `Space`:

```text
Name = requestedName
Id   = new Guid
```

Старый source space id сохраняется как техническая metadata/property:

```json
{
  "originalEntityId": "e53cf5a7-11c6-4e13-8cb5-10370860059e",
  "restoreSessionId": "..."
}
```

Backup settings нового пространства:

- `DoBackup` по умолчанию `false`, чтобы restore случайно не перезаписал источник backup;
- `BackupFolder` очищается или пересчитывается под новый space id;
- пользователь может включить backup вручную после проверки restore.

### 5.4. Restore entity shells

Сначала создаются все `BusinessEntity` без relations.

Это нужно, чтобы на этапе relations все endpoints уже существовали.

Для каждого entity:

```text
oldEntity.Id        -> newEntity.Id
oldEntity.Name      -> newEntity.Name
oldEntity.EntityType -> newEntity.EntityType
```

Если entity является самим source space, она не восстанавливается как обычная вложенная entity, а используется как данные для создания нового target space.

### 5.5. Restore properties

Properties восстанавливаются после создания parent object.

```text
old property parent id -> RestoreIdMap -> new parent id
old property id        -> new property id
```

Если property data содержит ссылки на entity ids, их remap делает entity-specific restore handler.

### 5.6. Restore data versions

Для каждой entity восстанавливаются все data versions.

```text
BusinessEntityData.Id               -> new Guid
BusinessEntityData.BusinessEntityId -> mapped entity id
BusinessEntityData.Version          -> same value
BusinessEntityData.Data             -> handler-remapped payload
```

Version numbers сохраняются, потому что они являются частью истории документа.

### 5.7. Restore chunks

Chunks восстанавливаются с сохранением version history.

```text
old chunk logical id -> new chunk logical id
SortOrder            -> same value
Version              -> same value
BusinessEntityId      -> mapped entity id
```

Derived поля лучше пересчитать, если есть serializer для типа entity:

- `PlainText`;
- `HtmlCache`;
- `BlockCount`;
- `CharCount`;
- `DataSizeBytes`;
- `Checksum`.

Причина: `HtmlCache` может содержать URL с source document id. При restore в новый document id cache должен быть перестроен.

### 5.8. Restore files

Canonical files копируются из:

```text
entities/{entityFolder}/files/
```

в текущий storage root:

```text
{StorageRoot}/business-entities/{newEntityId}/
```

Для rich-doc images:

```text
files/images/{imageId}/metadata.json
files/images/{imageId}/original.png
```

копируется как:

```text
business-entities/{newRichDocEntityId}/images/{imageId}/metadata.json
business-entities/{newRichDocEntityId}/images/{imageId}/original.png
```

`imageId` сохраняется, потому что rich-doc content ссылается именно на document-local image id.

`attachments/` рядом с human-readable HTML не является canonical source. Она может отсутствовать и restore все равно должен быть валиден.

### 5.9. Restore relations

Relations восстанавливаются после entities.

Canonical source:

```text
relations/index.json
```

Endpoint copies в `relations/by-entity/*` являются удобством для человека и incremental update. При restore они могут использоваться для диагностики, но не должны создавать дубликаты.

Для каждой relation:

```text
oldRelation.Id       -> newRelation.Id
oldRelation.SourceId -> RestoreIdMap.Entities[oldSourceId]
oldRelation.TargetId -> RestoreIdMap.Entities[oldTargetId]
```

Если source или target отсутствует в map, restore должен остановиться с ошибкой, потому что восстановленное пространство будет неконсистентным.

---

## 6. Entity-specific restore handlers

Backup и restore должны быть симметричны.

Каждый тип entity имеет helper/handler:

```csharp
public interface IBusinessEntityBackupHandler
{
    Task WriteBackupAsync(...);
}

public interface IBusinessEntityRestoreHandler
{
    Task RestoreAsync(...);
}
```

Restore handler получает:

- source entity folder;
- target entity shell;
- `RestoreIdMap`;
- доступ к storage root;
- доступ к DataProvider;
- режим restore.

### 6.1. Generic handler

Generic handler восстанавливает:

- `entity.json`;
- `entity-properties.json`;
- `data/*`;
- `files/*`.

Он не должен пытаться понимать rich-doc chunk payload или специальные references.

### 6.2. Document handler

Document handler:

- восстанавливает все `BusinessEntityData` versions;
- remap-ит `BusinessEntityId`;
- не использует `{entityName}--human-readable.md` как source;
- может использовать markdown только для диагностики расхождений.

### 6.3. RichTextDocument handler

RichTextDocument handler:

- восстанавливает manifest/data versions;
- восстанавливает все chunk versions;
- сохраняет `Version`;
- remap-ит chunk logical ids consistently across versions;
- копирует embedded images в storage нового document id;
- сохраняет document-local `imageId`;
- перестраивает `HtmlCache` под новый document id;
- перестраивает outline/search/cache, если эти данные являются derived.

Если в будущем rich-doc content будет содержать ссылки на другие business entities, handler обязан remap-ить эти ссылки через `RestoreIdMap.Entities`.

### 6.4. Space handler

Space handler не создает source space id заново.

Он создает новый target space и переносит только допустимые properties.

Backup settings переносятся осторожно:

- `DoBackup`: default `false`;
- `BackupFolder`: empty/new default;
- `BackupIntervalMinutes`: можно сохранить;
- original backup source path сохраняется только в restore report.

---

## 7. Restore transactionality

Restore должен быть атомарным настолько, насколько это возможно при сочетании БД и файловой системы.

Рекомендуемая схема:

```text
create restore session
        |
        v
write DB rows in transaction
        |
        v
copy files to staging storage folder
        |
        v
commit DB transaction
        |
        v
publish files into final storage folders
        |
        v
mark restore completed
```

Если restore падает:

- DB transaction откатывается, если еще не commit;
- staging files удаляются;
- если commit уже произошел, restore report должен явно указать статус `FailedAfterCommit`, а UI должен предложить удалить созданное пространство.

Для MVP допустимо сделать более простой вариант:

```text
create new space
restore all
if failed -> mark space as RestoreFailed / write restore report
```

Но даже в MVP нельзя молча оставлять частично восстановленное пространство как будто оно успешно.

---

## 8. Restore report

После restore должен быть отчет:

```json
{
  "restoreSessionId": "...",
  "startedAtUtc": "...",
  "finishedAtUtc": "...",
  "status": "Completed",
  "sourceBackupRoot": "...",
  "sourceSpaceId": "...",
  "targetSpaceId": "...",
  "targetSpaceName": "...",
  "entityCount": 42,
  "relationCount": 41,
  "warnings": []
}
```

Отчет нужен для:

- диагностики;
- поддержки пользователя;
- будущего частичного restore;
- аудита old->new id mappings.

Полный `RestoreIdMap` может быть большим и чувствительным, поэтому место хранения надо выбрать отдельно:

- либо рядом с logs;
- либо в служебной таблице restore sessions;
- либо в папке target backup после первого backup нового пространства.

---

## 9. Verification после restore

Минимальные post-checks:

- target space существует;
- все restored entities принадлежат target space через relations;
- все relation endpoints существуют;
- у каждого entity восстановлены data versions;
- у rich-doc восстановлены chunks;
- у rich-doc первая страница открывается;
- все image markers текущей версии rich-doc имеют файл в storage;
- нет relation duplicates по semantic key.

Ошибки post-check должны переводить restore в статус `CompletedWithErrors` или `Failed`.

---

## 10. UI/API сценарий

В администрировании пространств отдельная кнопка:

```text
Import space backup
```

Поля:

- путь к backup root;
- имя нового пространства;
- dry-run checkbox;
- start restore button.

Dry-run выполняет validate + build plan, но не пишет в БД и storage.

После restore UI показывает:

- имя нового пространства;
- количество entities;
- количество relations;
- warnings/errors;
- ссылку перейти в новое пространство.

---

## 11. Partial import в будущем

Текущая политика описывает полный restore пространства.

Partial import отдельной entity возможен позже на той же архитектуре:

```text
select entity subtree
build RestoreIdMap for subset
restore selected entities
restore only internal relations
external relations -> skip or ask user
```

Но для MVP partial import запрещен, чтобы не потерять references.

---

## 12. Запреты

Запрещено:

- восстанавливать backup поверх существующего пространства без отдельной merge-политики;
- использовать human-readable HTML/Markdown как canonical restore source;
- сохранять старые IDs в текущую БД без collision-check;
- восстанавливать relations до создания endpoint entities;
- копировать files в storage old entity id;
- игнорировать restore errors и показывать пространство как успешно восстановленное;
- включать backup у нового пространства так, чтобы оно писало в source backup folder.

---

## 13. Короткий ответ на вопрос про ID

ID "соблюдаются" не через сохранение старых GUID в новой БД, а через строгий `RestoreIdMap`.

```text
backup relation:
  old Folder A -> old RichDoc B

restore map:
  old Folder A  -> new Folder A'
  old RichDoc B -> new RichDoc B'

restored relation:
  new Folder A' -> new RichDoc B'
```

Так мы можем импортировать backup в ту же систему рядом с оригиналом, не ломая уникальность IDs и не теряя структуру пространства.

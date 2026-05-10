# Политика backup пространства: entity-folder layout

## 1. Назначение

Этот документ фиксирует основной формат и механику backup пространства в системе `BusinessEntity`.

Система состоит из:

- `BusinessEntity`;
- отношений между `BusinessEntity`;
- payload/data/chunks/properties/files, принадлежащих конкретным `BusinessEntity`.

Поэтому backup пространства должен отражать эту же модель:

```text
Space backup =
  entities/
  relations/
  manifest.json
```

Главные цели:

- человекочитаемость;
- возможность восстановить все пространство;
- возможность импортировать отдельную сущность или поддерево;
- отсутствие зависимости от полного dump БД;
- возможность фонового incremental backup только измененных сущностей.

---

## 2. Главный принцип

Backup делается не как полный snapshot всего пространства при каждом запуске.

Основная модель:

```text
backup dirty BusinessEntity -> update entity folder
backup dirty relations       -> update relations folder
update manifest              -> publish backup state
```

То есть единица backup - конкретный dirty `BusinessEntity`, а не все пространство целиком.

При backup одной `BusinessEntity` ее опубликованная папка заменяется целиком:

```text
write new entity folder to temp
delete old published entity folder
move temp entity folder to published path
update manifest
```

Запрещено дописывать новую версию entity backup поверх старой папки частично. Если entity dirty, ее folder считается устаревшим полностью и пересоздается с нуля.

Полный обход пространства допустим:

- при первом backup пространства;
- при ручном rebuild backup;
- при диагностической пересборке;
- после изменения layout/schema version.

В обычном режиме background worker должен обрабатывать только сущности, помеченные dirty.

---

## 3. Root layout

Backup пространства хранится в root-каталоге:

```text
{backup-root}/
  manifest.json
  entities/
    {entityType}--{entityId}--{entityName}/
      entity.json
      entity-properties.json
      data/
      files/
  relations/
    index.json
    relation-properties-index.json
    by-entity/
      {entityType}--{entityId}--{entityName}/
        relation--{relationType}--{relationId}.json
```

Пример:

```text
Space--2f54b7a3-5fd0-49e8-a9af-c4da5ed2df0b/
  manifest.json
  entities/
    Space--2f54b7a3-5fd0-49e8-a9af-c4da5ed2df0b--Документы/
    Folder--7f8df62e-c0d6-4863-9a4f-34b2c9f5cf4d--Folder 1/
    RichTextDocument--06da7655-bb71-4503-b352-db6afef72af7--Путешествие/
  relations/
    index.json
    relation-properties-index.json
    by-entity/
      Space--2f54b7a3-5fd0-49e8-a9af-c4da5ed2df0b--Документы/
        relation--Contains--9b7d6d8a-ec74-4f13-89fe-f038e777bd8a.json
      RichTextDocument--06da7655-bb71-4503-b352-db6afef72af7--Путешествие/
        relation--Contains--9b7d6d8a-ec74-4f13-89fe-f038e777bd8a.json
```

Root backup folder является текущим опубликованным состоянием backup пространства.

Для записи dirty entity используется временная папка:

```text
entities/.in-progress/{jobId}/{entityType}--{entityId}--{entityName}/
```

После успешной записи временная папка атомарно заменяет опубликованную папку entity.

Замена означает полную замену папки конкретной `BusinessEntity`: старый каталог `{entityType}--{entityId}--{entityName}` удаляется, затем на его место публикуется новый каталог из `.in-progress`.

Если entity переименована, backup writer после успешной публикации нового каталога удаляет старые каталоги с тем же prefix `{entityType}--{entityId}--*`, чтобы в `entities/` не оставались дубликаты одной сущности.

---

## 4. Manifest

В корне backup лежит:

```text
manifest.json
```

Manifest читается людьми, роботами, индексаторами и backup-viewer.

Минимальный manifest:

```json
{
  "schemaVersion": 1,
  "kind": "SpaceBackupEntityFolderLayout",
  "layout": "entity-folder",
  "spaceId": "2f54b7a3-5fd0-49e8-a9af-c4da5ed2df0b",
  "spaceName": "Документы",
  "createdUtc": "2026-05-09T18:40:00Z",
  "lastUpdatedUtc": "2026-05-09T19:10:00Z",
  "applicationVersion": "0.12.0",
  "isComplete": true,
  "entityFolderNamePattern": "{entityType}--{entityId}--{entityName}",
  "counts": {
    "entities": 0,
    "relations": 0,
    "entityProperties": 0,
    "dataItems": 0,
    "dataProperties": 0,
    "chunks": 0,
    "chunkProperties": 0,
    "files": 0
  },
  "entities": [
    {
      "id": "06da7655-bb71-4503-b352-db6afef72af7",
      "entityType": "RichTextDocument",
      "name": "Путешествие",
      "folder": "entities/RichTextDocument--06da7655-bb71-4503-b352-db6afef72af7--Путешествие",
      "lastBackedUpUtc": "2026-05-09T19:10:00Z"
    }
  ]
}
```

`isComplete` означает, что опубликованный backup root не находится в состоянии незавершенной перестройки.

При incremental backup одной entity manifest обновляется после успешной публикации entity folder.

---

## 5. Entity folder names

Каждая business entity хранится в отдельной папке:

```text
entities/{entityType}--{entityId}--{entityName}/
```

Пример:

```text
entities/RichTextDocument--06da7655-bb71-4503-b352-db6afef72af7--Путешествие/
```

`entityType` должен быть стабильным storage-type, а не UI-лейблом.
`entityName` - sanitized имя `BusinessEntity.Name`, то есть имя, которое пользователь видит в дереве. Оно добавляется только для человекочитаемости. Identity всегда определяется по `entityId`, а не по имени папки.

Рекомендуемое правило:

```text
Использовать имя из BusinessEntityTypeEnum без локализации.
```

Если вводятся aliases вроде `RichDoc`, manifest должен содержать таблицу:

```json
{
  "entityTypeAliases": {
    "RichDoc": "RichTextDocument"
  }
}
```

---

## 6. Entity folder layout

Базовая структура одной entity:

```text
entities/{entityType}--{entityId}--{entityName}/
  entity.json
  entity-properties.json
  backup-metadata.json
  {entityName}--human-readable.md
  {entityName}--human-readable.html
  attachments/
    images/
    attachments/
  data/
    data-manifest.json
    business-entity-data--{dataId}--v{version}.json
    data-properties--{dataId}--v{version}.json
    chunks/
      chunk--{sortOrder}--{chunkId}--v{version}.json
      chunk-properties--{chunkId}--v{version}.json
  files/
    images/
    attachments/
    archives/
    generated/
```

Назначение:

- `entity.json` - сам `BusinessEntity`;
- `entity-properties.json` - `BusinessEntityProperties` этой entity;
- `backup-metadata.json` - технические сведения backup этой entity;
- `{entityName}--human-readable.md` - человекочитаемый экспорт для обычного `Document`;
- `{entityName}--human-readable.html` - человекочитаемый экспорт для `RichTextDocument`;
- `attachments/` - файлы для человекочитаемого HTML-export, в обычных расширениях (`.png`, `.jpg`, `.webp`, etc.);
- `data/` - payload, data-properties, chunks;
- `files/` - canonical копия файловых объектов entity в системной структуре.

Для `RichTextDocument` человекочитаемый экспорт пишется как один большой HTML-файл:

```text
{entityName}--human-readable.html
attachments/images/{imageId}/original.png
attachments/images/{imageId}/display.webp
```

HTML должен открываться прямо из файловой системы и показывать текст вместе с картинками. Поэтому все image markers внутри HTML должны ссылаться на локальные файлы из `attachments/`, а не на web endpoint приложения.

Rich document export отражает текущую последнюю версию документа: берется максимальная версия `BusinessEntityData`, затем для каждой позиции `SortOrder` выбирается последняя chunk-запись с `Version <= documentVersion`. JSON backup при этом продолжает хранить все версии chunk-записей.

Entity folder должна быть полезна сама по себе: ее можно открыть файловым viewer и понять, что это за сущность.

---

## 7. Entity-specific backup handlers

Каждый тип сущности знает, как себя backup-ить и восстанавливать.

Это реализуется через handler-ы, зарегистрированные по `BusinessEntityTypeEnum`.

Ориентировочный контракт:

```csharp
public interface IBusinessEntityBackupHandler
{
    BusinessEntityTypeEnum EntityType { get; }

    Task WriteBackupAsync(
        BusinessEntityBackupWriteContext context,
        CancellationToken ct = default);

    Task RestoreAsync(
        BusinessEntityBackupRestoreContext context,
        CancellationToken ct = default);

    Task ImportAsCopyAsync(
        BusinessEntityBackupImportContext context,
        CancellationToken ct = default);
}
```

Backup orchestrator отвечает за:

- dirty queue;
- debounce;
- выбор handler по entity type;
- временную папку записи;
- атомарную публикацию entity folder;
- обновление `manifest.json`;
- backup `relations/`;
- логирование.

Entity handler отвечает за:

- формат своей entity folder;
- запись `entity.json`;
- запись properties;
- запись payload/data/chunks;
- копирование файлов entity;
- восстановление entity из своей папки;
- remap ссылок при import as copy.

Примеры:

- `SpaceBackupHandler` - сохраняет настройки пространства и properties.
- `FolderBackupHandler` - сохраняет простую folder entity.
- `DocumentBackupHandler` - сохраняет обычный document payload.
- `RichTextDocumentBackupHandler` - сохраняет data versions, chunks, images, attachments, rich content references.

Если для типа нет специализированного handler, можно использовать generic handler для простых entity без сложного payload.

---

## 8. Dirty model

Backup запускается по dirty-флагу конкретной business entity.

Рекомендуемый storage/contract:

```text
BusinessEntityBackupState:
  SpaceId
  EntityId
  EntityType
  IsDirty
  FirstDirtyUtc
  LastDirtyUtc
  LastBackedUpUtc
  DirtyReason
  LastError
```

Entity помечается dirty при изменении:

- самой `BusinessEntity`;
- `BusinessEntityProperties`;
- `BusinessEntityDataDto`;
- `BusinessEntityDataPropertyDto`;
- `BusinessEntityDataChunkDto`;
- `BusinessEntityDataChunkPropertyDto`;
- файлов entity;
- любых references внутри payload entity.

Relations имеют отдельный dirty-state на уровне пространства:

```text
SpaceRelationsBackupState:
  SpaceId
  IsDirty
  LastDirtyUtc
  LastBackedUpUtc
```

Изменение relation не требует backup всех entities, но требует обновить:

```text
relations/index.json
relations/relation-properties-index.json
relations/by-entity/{entityType}--{entityId}--{entityName}/
manifest.json
```

---

## 9. Debounce и worker

Backup не должен выполняться синхронно внутри пользовательского save.

При изменении данных система только помечает entity dirty.

Background worker запускает backup по настройкам пространства:

```text
GenericSpaceProperties.DoBackup == true
and now >= NextScheduledBackupUtc
```

`NextScheduledBackupUtc` является относительной временной точкой:

```text
NextScheduledBackupUtc = CurrentBackupFinishedUtc + GenericSpaceProperties.BackupIntervalMinutes
```

То есть период считается не от абсолютных минут часа, а от фактического окончания предыдущего backup этого пространства.

Первая реализация может дополнительно иметь короткий technical polling interval, например 30 секунд, чтобы проверять наступление `NextScheduledBackupUtc`.

Ручная кнопка `Backup` в настройках пространства запускает backup немедленно и после завершения так же пересчитывает следующую фоновую точку запуска как `finish + interval`.

Кнопка `Очистить бэкап` в настройках пространства удаляет текущий опубликованный backup root данного пространства. Удаление допускается только внутри storage-root приложения, чтобы не стереть произвольный путь на диске.

При сохранении данных система обновляет timestamps dirty-объектов. Worker при наступлении таймера пишет только те entities/relations, watermark которых новее последнего `backup-metadata.json`.

При backup одного пространства worker может обработать несколько dirty entities пачкой, но все равно каждая entity пишется через свой handler и свою entity folder.

---

## 10. entity.json

Файл:

```text
entity.json
```

Содержит storage-представление `BusinessEntity`.

Пример:

```json
{
  "schemaVersion": 1,
  "kind": "BusinessEntity",
  "id": "06da7655-bb71-4503-b352-db6afef72af7",
  "entityType": "RichTextDocument",
  "name": "Путешествие",
  "createdDate": "2026-05-09T06:11:00Z",
  "lastModifiedDate": "2026-05-09T18:35:00Z"
}
```

---

## 11. Entity properties

Файл:

```text
entity-properties.json
```

Содержит `BusinessEntityProperties` entity.

Если property `Data` является JSON-строкой, backup writer должен по возможности сохранить ее как JSON object. Если распарсить нельзя, сохраняется string.

Пример:

```json
{
  "schemaVersion": 1,
  "kind": "BusinessEntityProperties",
  "parentEntityId": "2f54b7a3-5fd0-49e8-a9af-c4da5ed2df0b",
  "items": [
    {
      "id": "1d7fe2c5-4f7b-4d0c-bc37-50af5b99d4e5",
      "propertyType": "GenericSpaceProperties",
      "data": {
        "schemaVersion": 1,
        "kind": "GenericSpaceProperties",
        "doBackup": true,
        "backupFolder": "",
        "backupIntervalMinutes": 5
      },
      "metadata": "GenericSpaceProperties"
    }
  ]
}
```

---

## 12. Data и chunks

Каждая версия `BusinessEntityDataDto` хранится отдельным JSON-файлом:

```text
data/business-entity-data--{dataId}--v{version}.json
```

Chunked entity хранит chunks так:

```text
data/chunks/chunk--{sortOrder}--{chunkId}--v{version}.json
```

Пример:

```text
data/chunks/chunk--0000000120--7f0c31b2-a9e0-4e57-bdc8-eed89eb9823d--v4.json
```

`sortOrder` в имени нужен для человекочитаемой сортировки.

Для очень больших документов допустим будущий packed режим:

```text
data/chunks/chunks-v{version}.jsonl
```

Но packed режим не должен отменять entity-folder boundary.

---

## 13. Files inside entity

Файлы entity хранятся внутри ее папки:

```text
files/
  images/
  attachments/
  archives/
  generated/
```

Пример:

```text
entities/RichTextDocument--06da7655-bb71-4503-b352-db6afef72af7--Путешествие/
  files/
    images/
      68ce3b41-2f7d-4d5b-923d-5c11d8ec8165/
        metadata.json
        original.png
  attachments/
    images/
      68ce3b41-2f7d-4d5b-923d-5c11d8ec8165/
        original.png
```

Файл считается принадлежащим entity, если:

- entity content ссылается на этот file id;
- metadata файла указывает `BusinessEntityId` этой entity;
- attachment явно привязан к этой entity.

Если один файл используется несколькими entities, первая реализация может физически дублировать файл в каждой entity folder. Deduplication допускается позже через manifest-level object index.

---

## 14. Relations layout

Relations хранятся отдельно от entities:

```text
relations/
  index.json
  relation-properties-index.json
  by-entity/
    {entityType}--{entityId}--{entityName}/
      relation--{relationType}--{relationId}.json
```

Каждый relation пишется отдельным JSON-файлом. Relation дублируется в папках обеих участвующих entities:

- в папке `ObjectAId` файл считается `Outgoing`;
- в папке `ObjectBId` файл считается `Incoming`;
- если `ObjectAId == ObjectBId`, файл пишется один раз с направлением `Self`.

Такой layout нужен, чтобы при изменении relations конкретной entity можно было удалить только ее папку `relations/by-entity/{entityType}--{entityId}--{entityName}/` и записать актуальный набор relation-файлов для этой entity.

Файл relation:

```json
{
  "schemaVersion": 1,
  "kind": "BusinessEntityRelation",
  "endpointEntityId": "2f54b7a3-5fd0-49e8-a9af-c4da5ed2df0b",
  "endpointDirection": "Outgoing",
  "id": "9b7d6d8a-ec74-4f13-89fe-f038e777bd8a",
  "objectAId": "2f54b7a3-5fd0-49e8-a9af-c4da5ed2df0b",
  "objectBId": "06da7655-bb71-4503-b352-db6afef72af7",
  "relationType": "Contains",
  "relationParams": "",
  "createdDate": "2026-05-10T00:00:00Z",
  "lastModifiedDate": "2026-05-10T00:00:00Z"
}
```

`index.json` хранит список relation ids и путей к файлам, где relation представлен:

```json
{
  "schemaVersion": 1,
  "kind": "BusinessEntityRelationsIndex",
  "spaceId": "2f54b7a3-5fd0-49e8-a9af-c4da5ed2df0b",
  "layout": "by-entity-one-file-per-relation",
  "items": [
    {
      "id": "9b7d6d8a-ec74-4f13-89fe-f038e777bd8a",
      "objectAId": "2f54b7a3-5fd0-49e8-a9af-c4da5ed2df0b",
      "objectBId": "06da7655-bb71-4503-b352-db6afef72af7",
      "relationType": "Contains",
      "files": [
        "by-entity/Space--2f54b7a3-5fd0-49e8-a9af-c4da5ed2df0b--Документы/relation--Contains--9b7d6d8a-ec74-4f13-89fe-f038e777bd8a.json",
        "by-entity/RichTextDocument--06da7655-bb71-4503-b352-db6afef72af7--Путешествие/relation--Contains--9b7d6d8a-ec74-4f13-89fe-f038e777bd8a.json"
      ]
    }
  ]
}
```

`relation-properties-index.json` хранит index properties отношений, если для relations будет введена отдельная property-table.

При restore relation-файлы дедуплицируются по `id`. Дубликаты в endpoint-папках должны описывать один и тот же relation.

При partial import автоматически восстанавливаются только relations, где обе стороны входят в импортируемый набор.

---

## 15. Partial import

Entity-folder layout обязан поддерживать частичный импорт.

Для одной entity импортируются:

- `entity.json`;
- `entity-properties.json`;
- `data/`;
- `files/`;
- relations, где обе стороны входят в импортируемый набор.

Если импортируется entity без parent, UI должен попросить выбрать target parent/space.

Режимы:

1. Preserve identity - сохранить original ids.
2. Import as copy - создать новые ids.

Для `Import as copy` handler обязан remap-нуть внутренние ссылки:

```text
oldEntityId -> newEntityId
oldDataId -> newDataId
oldChunkId -> newChunkId
oldFileId -> newFileId
oldRelationId -> newRelationId
```

Rich document handler должен remap-нуть image/file markers внутри content.

---

## 16. Restore

Restore original identity:

- восстанавливает оригинальные ids;
- конфликт с существующей entity требует explicit overwrite;
- relations восстанавливаются после entities;
- files копируются до финальной активации данных.

Import as copy:

- создает новый root/parent;
- remap-ит все внутренние ids;
- сохраняет внутреннюю структуру дерева;
- не восстанавливает external references без подтверждения.

Restore выполняется через те же entity-specific handlers, которые пишут backup.

---

## 17. Человекочитаемость

Каждая entity folder должна быть пригодна для просмотра без БД:

- `entity.json` показывает имя, тип, даты;
- `data-manifest.json` показывает версии payload;
- chunk-файлы имеют `plainText` и/или `htmlCache`;
- `Document` имеет Markdown-export `{entityName}--human-readable.md`;
- `RichTextDocument` имеет HTML-export `{entityName}--human-readable.html`;
- images в `attachments/` лежат обычными файлами с родными расширениями;
- manifest содержит индекс entities.

Для rich document viewer может:

1. открыть root `manifest.json`;
2. выбрать entity типа `RichTextDocument`;
3. прочитать `data/data-manifest.json`;
4. прочитать chunks нужной версии;
5. заменить image markers на файлы из `attachments/images`.

---

## 18. Consistency

Incremental backup должен быть консистентным на уровне одной entity.

Правила:

- entity handler сначала пишет во временную папку;
- published entity folder полностью удаляется и заменяется только после успешной записи новой папки;
- manifest обновляется после публикации entity folder;
- dirty flag entity снимается только после успешного backup;
- если backup завершился ошибкой, старая опубликованная entity folder остается нетронутой;
- если во время backup entity снова изменилась, она остается dirty для следующего цикла.

Relations обновляются отдельно по dirty-state отношений. Текущая первая реализация может пересобирать весь каталог `relations/`, но целевой layout обязан поддерживать более узкую замену: удалить `relations/by-entity/{entityType}--{entityId}--{entityName}/` и записать заново только relation-файлы конкретной entity.

---

## 19. Запрещено

Запрещено:

- backup-ить все пространство при каждом save, если dirty только одна entity;
- писать entity folder прямо в published path без temporary folder;
- делать manifest complete до завершения записи;
- использовать локализованные имена типов в folder names;
- терять relation ids при restore original identity;
- смешивать файлы разных entities в одной папке без manifest/index;
- при partial import молча восстанавливать relations на отсутствующие entities;
- делать пользовательский просмотр rich document путем чтения всех chunks в память.

---

## 20. Статус политики

Эта политика является основной политикой backup layout.

Предыдущая snapshot-oriented политика удалена, чтобы не держать два конкурирующих подхода.

# Политика версионирования BusinessEntityData

## 1. Назначение

Этот документ фиксирует текущие правила версионирования payload-данных бизнес-сущностей в `BusinessEntity`.

Версионирование означает хранение нескольких storage-версий данных одного и того же `BusinessEntity`.

---

## 2. Главный принцип

Версионируется не сам `BusinessEntity`, а его payload-часть:

- `BusinessEntityDataDto`
- `BusinessEntityDataChunkDto`, если содержимое хранится чанками

`BusinessEntity` остается стабильной графовой сущностью с одним `Id`.

Каждая новая версия payload хранится отдельной storage-записью с:

- тем же logical `Id` payload-записи
- тем же `BusinessEntityId`
- увеличенным `Version`

Уникальность versioned storage-записи определяется парой logical `Id + Version`, а не одним `Id`.
Это правило нужно и для `BusinessEntityDataDto`, и для `BusinessEntityDataChunkDto`.

---

## 3. Когда версия создается

Новая версия создается при редактировании и сохранении business entity payload.

Для `RichTextDocument` импорт также считается правкой. Поэтому импорт всегда создает новую версию:

```text
targetVersion = currentLatestVersion + 1
```

После импорта новая запись `BusinessEntityDataDto` получает `targetVersion`, а все импортированные chunks получают тот же `targetVersion`.

Если payload-тип поддерживает версии, сохранение работает append-only:

```text
BusinessEntity.Id = A

BusinessEntityDataDto:
  Id = D, BusinessEntityId = A, Version = 1
  Id = D, BusinessEntityId = A, Version = 2
  Id = D, BusinessEntityId = A, Version = 3
```

Чтение обычного payload всегда возвращает актуальную версию с максимальным `Version`.

---

## 4. Когда версионирование не выполняется

Если business entity не имеет `BusinessEntityData`, версионирование не выполняется.

Если payload-тип имеет `HasVersions == false`, сохранение обновляет актуальную storage-запись in-place.

По умолчанию:

```text
BusinessEntityData.HasVersions = false
```

---

## 5. Runtime contract

В базовом контракте `IBusinessEntityData` есть:

- `Version`
- `HasVersions`
- `ChunkStorageType`

В базовом классе `BusinessEntityData`:

- `Version = 1`
- `HasVersions = false`
- `ChunkStorageType = None`

Конкретный payload-тип включает версионирование через override:

```csharp
public override bool HasVersions => true;
```

Текущие версионируемые payload-типы:

- `Document`
- `RichTextDocument`

`ChunkStorageType` описывает физическую модель хранения payload:

- `None` - payload не использует chunk-хранение
- `TextChunks` - payload хранится текстовыми чанками
- `ByteChunks` - payload хранится бинарными чанками

На текущий момент `ChunkStorageType == TextChunks` только у `RichTextDocument`. У остальных payload-типов используется значение по умолчанию `None`.

---

## 6. BusinessEntityDataDto

`BusinessEntityDataDto` хранит manifest/body payload без чанков.

Для версионируемых payload:

- при первом сохранении создается `Version = 1`
- при каждом следующем сохранении создается новая запись
- `Id` остается logical id payload-записи
- `BusinessEntityId` остается тем же
- чтение берет запись с максимальным `Version`

Для неверсионируемых payload:

- используется одна storage-запись
- `Version` нормализуется к `1`
- `Data` обновляется in-place

---

## 7. BusinessEntityDataChunkDto

`BusinessEntityDataChunkDto` хранит технические chunks rich-text документа.

Для rich-text dirty-save измененный chunk сохраняется как новая строка:

- тот же logical chunk `Id`
- тот же `BusinessEntityId`
- тот же `SortOrder`
- `Version = previous.Version + 1`

Старый chunk не удаляется, чтобы история chunk-содержимого оставалась в storage.

Чтение rich-text содержимого для версии `N` выбирает chunk-записи с `Version <= N`, затем оставляет последнюю запись по каждому logical chunk `Id`:

```text
Where BusinessEntityId = A
  and Version <= N
Group by Chunk.Id
Order by Version desc, LastModifiedDate desc
Take first
Order result by SortOrder
```

Открытие rich-text документа без явно выбранной версии использует максимальную версию `BusinessEntityDataDto`.

Импорт rich-text документа append-ит новые chunks. Все chunks, созданные импортом, должны иметь один и тот же `Version`, равный новой версии документа. После импорта документ перечитывается так же, как при обычном открытии.

---

## 8. Schema

Storage-таблицы должны содержать:

- `BusinessEntityDataItems.Version`
- `BusinessEntityDataChunks.Version`

Для существующих БД startup schema должна добавлять недостающие колонки через `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`.

Рекомендуемые indexes:

- `(BusinessEntityId, Version)` для `BusinessEntityDataItems`
- `(BusinessEntityId, Id, Version)` для выбора версий chunks
- `(BusinessEntityId, SortOrder, Version)` для оконного чтения chunks

---

## 9. Ограничения текущей реализации

Текущий базовый read-path возвращает актуальную версию. Для rich-text UI список версий показывается в виджете документа; восстановление старой версии пока не вводится.

Chunk-версии сейчас выбираются по logical chunk `Id` и ограничению `Version <= selectedDocumentVersion`. Отдельной связи chunk с конкретным `BusinessEntityDataDto.Id` пока нет.

Если в будущем понадобится строгий снимок всего rich-text документа на конкретную версию manifest-а, нужно будет добавить явную связь chunk-записей с data-version id или отдельный version-set/snapshot id.

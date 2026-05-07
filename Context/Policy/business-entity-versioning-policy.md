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

- новым `Id`
- тем же `BusinessEntityId`
- увеличенным `Version`

---

## 3. Когда версия создается

Новая версия создается при редактировании и сохранении business entity payload.

Если payload-тип поддерживает версии, сохранение работает append-only:

```text
BusinessEntity.Id = A

BusinessEntityDataDto:
  Id = D1, BusinessEntityId = A, Version = 1
  Id = D2, BusinessEntityId = A, Version = 2
  Id = D3, BusinessEntityId = A, Version = 3
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

В базовом классе `BusinessEntityData`:

- `Version = 1`
- `HasVersions = false`

Конкретный payload-тип включает версионирование через override:

```csharp
public override bool HasVersions => true;
```

Текущие версионируемые payload-типы:

- `Document`
- `RichTextDocument`

---

## 6. BusinessEntityDataDto

`BusinessEntityDataDto` хранит manifest/body payload без чанков.

Для версионируемых payload:

- при первом сохранении создается `Version = 1`
- при каждом следующем сохранении создается новая запись
- `BusinessEntityId` остается тем же
- `Id` новой записи генерируется заново
- чтение берет запись с максимальным `Version`

Для неверсионируемых payload:

- используется одна storage-запись
- `Version` нормализуется к `1`
- `Data` обновляется in-place

---

## 7. BusinessEntityDataChunkDto

`BusinessEntityDataChunkDto` хранит технические chunks rich-text документа.

Для rich-text dirty-save измененный chunk сохраняется как новая строка:

- новый `Id`
- тот же `BusinessEntityId`
- тот же `SortOrder`
- `Version = previous.Version + 1`

Старый chunk не удаляется, чтобы история chunk-содержимого оставалась в storage.

Чтение актуального rich-text содержимого выбирает последнюю chunk-запись по каждому `SortOrder`:

```text
Group by BusinessEntityId + SortOrder
Order by Version desc, LastModifiedDate desc
Take first
```

---

## 8. Schema

Storage-таблицы должны содержать:

- `BusinessEntityDataItems.Version`
- `BusinessEntityDataChunks.Version`

Для существующих БД startup schema должна добавлять недостающие колонки через `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`.

Рекомендуемые indexes:

- `(BusinessEntityId, Version)` для `BusinessEntityDataItems`
- `(BusinessEntityId, SortOrder, Version)` для `BusinessEntityDataChunks`

---

## 9. Ограничения текущей реализации

Текущий базовый read-path возвращает актуальную версию. API просмотра списка всех версий и восстановления старой версии пока не вводится.

Chunk-версии сейчас выбираются по `SortOrder`. Отдельной связи chunk с конкретным `BusinessEntityDataDto.Id` пока нет.

Если в будущем понадобится строгий снимок всего rich-text документа на конкретную версию manifest-а, нужно будет добавить явную связь chunk-записей с data-version id или отдельный version-set/snapshot id.

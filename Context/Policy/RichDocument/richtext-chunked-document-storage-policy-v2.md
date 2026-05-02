# Политика хранения больших RichText-документов в чанках

Версия: 2  
Статус: рабочая политика для MVP

---

## 1. Назначение документа

Этот документ фиксирует модель хранения больших `RichTextDocument` в системе `BusinessEntity`.

Политика приводит хранение больших rich-text документов в соответствие с двумя действующими правилами проекта:

1. storage-контур `BusinessEntity` / `BusinessEntityData` / `BusinessEntityRelation`;
2. архитектура `MiniApp + ReactiveBus`.

Цель модели — поддержать практически неограниченные rich-text документы без хранения всего документа как одного огромного HTML, одного огромного JSON или одного огромного DOM в браузере.

В этой версии документа намеренно ограничен MVP-набор rich-text возможностей:

- обычный текст;
- заголовки;
- жирный текст;
- курсив;
- подчеркивание;
- вставленные изображения.

Макросы, таблицы, кодовые блоки, цитаты, вложенные бизнес-объекты, ссылки, списки и другие сложные элементы пока исключены из MVP.

---

## 2. Короткая формула решения

Большой rich-text документ хранится так:

```text
BusinessEntity
    = сам документ как бизнес-объект и узел дерева

BusinessEntityDataDto.Data
    = versioned JSON envelope с manifest документа

BusinessEntityDataChunks
    = техническая дочерняя таблица чанков документа

BusinessEntityDataChunk.Data
    = versioned JSON envelope одного чанка

Chunk payload
    = массив структурных блоков

Block
    = paragraph / heading / image
```

Главное правило:

> `BusinessEntityDataDto.Data` не хранит тело большого rich-text документа.  
> Для `RichTextDocument` он хранит только manifest.  
> Тело документа хранится в технической таблице чанков.  
> Вставленные изображения хранятся не как `BusinessEntity`, а как локальные файлы документа.

---

## 3. Соответствие текущей storage-политике

Текущая storage-политика остается основной:

- `BusinessEntity` — identity объекта;
- `BusinessEntityData` — payload-часть объекта;
- `BusinessEntityRelation` — связь между бизнес-объектами;
- физически используются `BusinessEntityDto`, `BusinessEntityDataDto`, `BusinessEntityRelationDto`;
- `BusinessEntityDataDto.Data` хранит minified JSON string в формате versioned envelope;
- запись и чтение payload идут через `DataProviderMiniApp`;
- основная база — `business_entity`;
- база логгера не используется для бизнес-объектов.

Чанковое хранение не отменяет эту модель.

Оно добавляет техническое расширение для тяжелого payload:

```text
BusinessEntityDataChunks
```

Эта таблица не является новой бизнес-сущностью графа.

Чанк:

- не является `BusinessEntity`;
- не является самостоятельным `BusinessEntityData`;
- не участвует в дереве через `BusinessEntityRelation`;
- не имеет собственного места в графе;
- подчинен документу через `BusinessEntityId`;
- обслуживается только storage-контуром `DataProviderMiniApp`.

Если в коде появляется `BusinessEntityDataChunkDto`, он должен рассматриваться как технический DTO storage-слоя, а не как четвертая базовая runtime-сущность бизнес-модели.

---

## 4. Типы документов

В системе логически есть два разных сценария.

### 4.1. Plain text document

Обычный текстовый документ хранится по текущей модели:

```json
{"schemaVersion":1,"kind":"Document","payload":{"text":"Обычный текст","tag":"Document"}}
```

Такой документ можно читать через старый сценарий `GetDataAsync<string>(id)`, где storage-слой извлекает `payload.text`.

### 4.2. RichTextDocument

Большой rich-text документ хранится иначе:

```text
BusinessEntityDataDto.Data
    содержит manifest

BusinessEntityDataChunks
    содержит реальные чанки документа

RichDocumentData/{documentId}/
    содержит вставленные изображения документа
```

Для него нельзя использовать старую семантику:

```text
документ = одна строка text
```

Правильный логический kind:

```text
RichTextDocument
```

---

## 5. Runtime-модель

### 5.1. BusinessEntity

`BusinessEntity` представляет сам документ.

Пример:

```text
BusinessEntity
--------------
Id = doc_001
Name = "Большой документ"
BusinessEntityType = "Document"
EntityType = "RichTextDocument"
```

Этот объект:

- участвует в дереве;
- имеет parent через `BusinessEntityRelation` типа `Contains`;
- имеет права доступа;
- имеет имя;
- является корневой identity документа.

### 5.2. BusinessEntityData

`BusinessEntityData` для `RichTextDocument` хранит не текст, а manifest.

Правила identity сохраняются:

```text
BusinessEntityData.Id == BusinessEntity.Id
BusinessEntityDataDto.Id == BusinessEntity.Id
BusinessEntityDataDto.BusinessEntityId == BusinessEntity.Id
```

### 5.3. BusinessEntityRelation

Дерево документов и папок продолжает строиться через relation типа:

```text
Contains
```

Чанки не связываются через `BusinessEntityRelation`.

Вставленные изображения также не связываются через `BusinessEntityRelation`, потому что в MVP они не являются бизнес-объектами.

---

## 6. Физическая модель хранения

### 6.1. Базовые таблицы остаются

В основной базе `business_entity` остаются базовые таблицы:

```text
BusinessEntities
BusinessEntityRelations
BusinessEntityDataItems
```

Они продолжают соответствовать:

```text
BusinessEntities          <- BusinessEntityDto
BusinessEntityRelations   <- BusinessEntityRelationDto
BusinessEntityDataItems   <- BusinessEntityDataDto
```

### 6.2. Добавляется техническая таблица чанков

Для больших rich-text документов добавляется техническая таблица:

```text
BusinessEntityDataChunks
```

Рекомендуемые поля:

```text
Id                  uuid primary key
CreatedDate         timestamptz not null
LastModifiedDate    timestamptz not null
BusinessEntityId    uuid not null
SortOrder           bigint not null
Data                text not null
PlainText           text null
HtmlCache           text null
BlockCount          int not null default 0
CharCount           int not null default 0
DataSizeBytes       int not null default 0
Version             int not null default 1
Checksum            text null
```

Смысл полей:

| Поле | Назначение |
|---|---|
| `Id` | identity технической строки чанка |
| `BusinessEntityId` | id владельца-документа |
| `SortOrder` | порядок чанков внутри документа |
| `Data` | minified JSON envelope чанка |
| `PlainText` | извлеченный текст для поиска |
| `HtmlCache` | необязательный кеш готового HTML |
| `BlockCount` | количество блоков в чанке |
| `CharCount` | количество текстовых символов |
| `DataSizeBytes` | размер JSON-строки |
| `Version` | оптимистичная блокировка |
| `Checksum` | контроль изменения содержимого |

Индексы:

```sql
create index ix_business_entity_data_chunks_entity_sort
on "BusinessEntityDataChunks" ("BusinessEntityId", "SortOrder");

create unique index ux_business_entity_data_chunks_entity_sort
on "BusinessEntityDataChunks" ("BusinessEntityId", "SortOrder");

create index ix_business_entity_data_chunks_entity
on "BusinessEntityDataChunks" ("BusinessEntityId");
```

Если используется полнотекстовый поиск PostgreSQL:

```sql
create index ix_business_entity_data_chunks_plain_text_fts
on "BusinessEntityDataChunks"
using gin (to_tsvector('russian', coalesce("PlainText", '')));
```

---

## 7. Почему чанки не являются BusinessEntity

Запрещено делать так:

```text
Document = BusinessEntity
Chunk 1  = BusinessEntity
Chunk 2  = BusinessEntity
Chunk 3  = BusinessEntity
```

Причины:

- граф засоряется техническими объектами;
- дерево документов начинает содержать внутренние строки хранения;
- права доступа усложняются без необходимости;
- поиск бизнес-объектов начинает видеть технические чанки;
- `BusinessEntityRelation` начинает использоваться не для бизнес-связей, а для физической упаковки данных;
- документ из тысяч чанков превращается в тысячи бизнес-объектов.

Правильная модель:

```text
Document = BusinessEntity
Chunks   = технические строки BusinessEntityDataChunks
```

---

## 8. Почему вставленные изображения не являются BusinessEntity

Вставленные изображения в MVP являются не самостоятельными объектами системы, а локальными ресурсами конкретного `RichTextDocument`.

Запрещено делать так:

```text
RichTextDocument = BusinessEntity
Image 1          = BusinessEntity
Image 2          = BusinessEntity

RichTextDocument --UsesMedia--> Image 1
RichTextDocument --UsesMedia--> Image 2
```

Причины:

- обычная картинка внутри текста не является самостоятельным бизнес-объектом;
- граф `BusinessEntityRelation` должен описывать бизнес-связи, а не внутренние файлы документа;
- права доступа картинки наследуются от документа;
- удаление документа должно удалять его локальные ресурсы как технические файлы;
- поиск и дерево объектов не должны видеть внутренние картинки как отдельные элементы.

Правильная модель для MVP:

```text
RichTextDocument = BusinessEntity

ImageBlock внутри chunk_json
    -> imageId

RichDocumentData/{documentId}/images/{imageId}/
    -> original image
    -> adapted variants
    -> metadata.json
```

Встраивание настоящих `BusinessEntity` в документ будет отдельной будущей функцией.

Для будущего встраивания бизнес-объекта можно будет добавить отдельный block type, например:

```text
businessEntityEmbed
```

Но этот тип не входит в текущий MVP.

---

## 9. Manifest в BusinessEntityDataDto.Data

Для `RichTextDocument` поле `BusinessEntityDataDto.Data` содержит versioned JSON envelope.

Формат envelope сохраняется общий:

```json
{
  "schemaVersion": 1,
  "kind": "RichTextDocument",
  "payload": {
    "tag": "RichTextDocument",
    "contentStorage": "ChunkedBlocks",
    "editorFormat": "BlockJsonWithInlineHtml",
    "chunkPolicy": {
      "targetChunkSizeKb": 128,
      "maxChunkSizeKb": 512
    },
    "features": {
      "paragraphs": true,
      "headings": true,
      "bold": true,
      "italic": true,
      "underline": true,
      "images": true,
      "macros": false,
      "tables": false,
      "links": false,
      "lists": false,
      "codeBlocks": false,
      "quotes": false,
      "businessEntityEmbeds": false
    },
    "embeddedFileStorage": {
      "kind": "DocumentLocalFolder",
      "rootFolder": "RichDocumentData",
      "documentFolderTemplate": "{documentId}",
      "imageFolderTemplate": "images/{imageId}"
    }
  }
}
```

В БД это должно лежать minified:

```json
{"schemaVersion":1,"kind":"RichTextDocument","payload":{"tag":"RichTextDocument","contentStorage":"ChunkedBlocks","editorFormat":"BlockJsonWithInlineHtml","chunkPolicy":{"targetChunkSizeKb":128,"maxChunkSizeKb":512},"features":{"paragraphs":true,"headings":true,"bold":true,"italic":true,"underline":true,"images":true,"macros":false,"tables":false,"links":false,"lists":false,"codeBlocks":false,"quotes":false,"businessEntityEmbeds":false},"embeddedFileStorage":{"kind":"DocumentLocalFolder","rootFolder":"RichDocumentData","documentFolderTemplate":"{documentId}","imageFolderTemplate":"images/{imageId}"}}}
```

Manifest отвечает за:

- тип документа;
- схему хранения;
- политику чанков;
- включенные возможности MVP;
- формат редакторной модели;
- схему хранения локальных файлов документа;
- настройки совместимости.

Manifest не должен содержать весь текст документа.

---

## 10. Формат чанка

Поле `BusinessEntityDataChunks.Data` также хранит minified JSON string.

Для единообразия с текущей storage-политикой чанк хранится как versioned envelope.

Рекомендуемый kind:

```text
RichTextDocumentChunk
```

Пример в читаемом виде:

```json
{
  "schemaVersion": 1,
  "kind": "RichTextDocumentChunk",
  "payload": {
    "blocks": [
      {
        "id": "b_1000",
        "type": "heading",
        "level": 1,
        "html": "Архитектура хранения"
      },
      {
        "id": "b_1010",
        "type": "paragraph",
        "html": "Это <strong>важный</strong> абзац с <em>курсивом</em> и <u>подчеркиванием</u>."
      },
      {
        "id": "b_1020",
        "type": "image",
        "imageId": "img_01HVK2M8AGM7V75MYZG7F9V5AA",
        "displayVariant": "display",
        "attrs": {
          "alt": "Схема хранения",
          "caption": "Схема хранения",
          "width": 900,
          "align": "center"
        }
      }
    ]
  }
}
```

В БД это должно лежать minified:

```json
{"schemaVersion":1,"kind":"RichTextDocumentChunk","payload":{"blocks":[{"id":"b_1000","type":"heading","level":1,"html":"Архитектура хранения"},{"id":"b_1010","type":"paragraph","html":"Это <strong>важный</strong> абзац с <em>курсивом</em> и <u>подчеркиванием</u>."},{"id":"b_1020","type":"image","imageId":"img_01HVK2M8AGM7V75MYZG7F9V5AA","displayVariant":"display","attrs":{"alt":"Схема хранения","caption":"Схема хранения","width":900,"align":"center"}}]}}
```

Важные правила:

- `imageId` — локальный идентификатор изображения внутри документа;
- `imageId` не является `BusinessEntity.Id`;
- `imageId` не участвует в `BusinessEntityRelation`;
- фактический файл ищется по `documentId + imageId`;
- image block хранит позицию изображения в тексте, а не бизнес-связь.

---

## 11. JSON-сериализация

Для manifest и для chunk data используются те же правила, что и для основного storage-контура.

Обязательно:

```text
StorageJsonOptions.Default
```

Фактические требования:

- `Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)`;
- `WriteIndented = false`;
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`;
- `DefaultIgnoreCondition = JsonIgnoreCondition.Never`;
- кириллица и другой Unicode должны оставаться читаемыми;
- не должно быть double-encoding;
- не должно быть зависимости от CLR full type name.

Правильно:

```json
{"schemaVersion":1,"kind":"RichTextDocument","payload":{"title":"Большой документ"}}
```

Неправильно:

```json
"{\"schemaVersion\":1,\"kind\":\"RichTextDocument\"}"
```

Неправильно:

```json
{"$type":"My.Namespace.RichTextDocument, MyAssembly","payload":{}}
```

---

## 12. Блоки внутри чанка

Чанк содержит массив структурных блоков.

Минимальный набор блоков для MVP:

```text
paragraph
heading
image
```

Пока не входят в MVP:

```text
macro
codeBlock
quote
divider
table
list
taskList
callout
fileAttachment
mention
pageLink
embed
businessEntityEmbed
```

### 12.1. Paragraph

```json
{
  "id": "b_2000",
  "type": "paragraph",
  "html": "Текст с <strong>жирным</strong>, <em>курсивом</em> и <u>подчеркиванием</u>."
}
```

### 12.2. Heading

```json
{
  "id": "b_3000",
  "type": "heading",
  "level": 2,
  "html": "Архитектура чанков"
}
```

Правила для heading:

- `level` допускается от `1` до `3` в MVP;
- внутри `heading.html` лучше использовать plain text;
- если inline-разметка нужна, разрешаются только `strong`, `em`, `u`, `br`.

### 12.3. Image

```json
{
  "id": "b_4000",
  "type": "image",
  "imageId": "img_01HVK2M8AGM7V75MYZG7F9V5AA",
  "displayVariant": "display",
  "attrs": {
    "alt": "Схема",
    "caption": "Схема графового хранения",
    "width": 800,
    "align": "center"
  }
}
```

Картинка не хранится base64 внутри чанка.

Картинка не является `BusinessEntity`.

Картинка хранится как локальный файл документа в каталоге `RichDocumentData`.

---

## 13. HTML внутри текстовых блоков

HTML разрешен только как ограниченная inline-разметка внутри простых текстовых блоков:

```text
paragraph.html
heading.html
```

Разрешенный минимум для MVP:

```text
strong
b
em
i
u
br
```

Семантика:

| HTML | Смысл |
|---|---|
| `strong` / `b` | жирный |
| `em` / `i` | курсив |
| `u` | подчеркивание |
| `br` | перенос строки внутри блока |

Запрещено внутри inline HTML:

```text
script
style
iframe
object
embed
form
input
button
img
table
div
section
article
h1-h6
a
span
code
pre
blockquote
ul
ol
li
```

Причина:

```text
heading -> отдельный block
image   -> отдельный block
```

HTML не является канонической моделью всего документа.

Каноническая модель:

```text
JSON envelope -> payload.blocks[] -> block objects
```

Для будущего можно заменить `html` на более строгую модель:

```json
{
  "id": "b_2000",
  "type": "paragraph",
  "content": [
    { "text": "Текст с ", "marks": [] },
    { "text": "жирным", "marks": ["bold"] },
    { "text": ", ", "marks": [] },
    { "text": "курсивом", "marks": ["italic"] },
    { "text": " и ", "marks": [] },
    { "text": "подчеркиванием", "marks": ["underline"] }
  ]
}
```

Но для MVP допустим безопасный inline HTML.

---

## 14. Хранение вставленных изображений

### 14.1. Общий принцип

Вставленные изображения являются локальными ресурсами конкретного `RichTextDocument`.

Они:

- не являются `BusinessEntity`;
- не имеют `BusinessEntityDataDto`;
- не имеют `BusinessEntityRelation`;
- не лежат base64 внутри chunk JSON;
- физически хранятся на диске;
- логически адресуются через `documentId + imageId`;
- права доступа наследуют от документа;
- удаляются вместе с документом.

### 14.2. Корневая папка

В конфигурации приложения задается корневая папка:

```text
RichDocumentData
```

Реальный путь зависит от хоста:

```text
on-prem Windows:
D:\AppData\RichDocumentData

on-prem Linux:
/var/lib/app/RichDocumentData

dev:
./App_Data/RichDocumentData
```

В БД и в chunk JSON не надо хранить абсолютные пути.

В БД хранится только `imageId` и, при необходимости, имя display variant.

Абсолютный путь вычисляется сервером из конфигурации:

```text
RichDocumentDataRoot + documentId + imageId
```

### 14.3. Структура папок

Рекомендуемая структура:

```text
RichDocumentData/
 └── {documentId:N}/
     ├── images/
     │   └── {imageId}/
     │       ├── original/
     │       │   └── original.{ext}
     │       ├── variants/
     │       │   ├── display.webp
     │       │   ├── preview.webp
     │       │   └── thumb.webp
     │       └── metadata.json
     └── _tmp/
```

Где:

| Элемент | Назначение |
|---|---|
| `{documentId:N}` | GUID документа без дефисов |
| `{imageId}` | локальный id изображения, например `img_01HVK2M8AGM7V75MYZG7F9V5AA` |
| `original/original.{ext}` | исходный файл в полном разрешении |
| `variants/display.webp` | адаптированная версия для отображения в документе |
| `variants/preview.webp` | средняя версия для быстрых preview |
| `variants/thumb.webp` | маленькая миниатюра |
| `metadata.json` | техническое описание изображения |
| `_tmp` | временная зона загрузки и генерации файлов |

Пример:

```text
RichDocumentData/
 └── 0f8fad5bd9cb469fa16570867728950e/
     └── images/
         └── img_01HVK2M8AGM7V75MYZG7F9V5AA/
             ├── original/
             │   └── original.png
             ├── variants/
             │   ├── display.webp
             │   ├── preview.webp
             │   └── thumb.webp
             └── metadata.json
```

### 14.4. Размеры вариантов

Рекомендуемые варианты для MVP:

| Variant | Назначение | Правило |
|---|---|---|
| `original` | хранение исходника | полный размер, без ухудшения |
| `display` | показ в документе | максимум 1600 px по ширине или высоте |
| `preview` | быстрый просмотр / карточки | максимум 800 px по ширине или высоте |
| `thumb` | миниатюры | максимум 320 px по ширине или высоте |

Формат адаптированных вариантов:

```text
webp
```

Если `webp` по каким-то причинам не подходит для on-prem окружения, можно использовать:

```text
jpg для фотографий
png для схем с прозрачностью
```

Но политика по умолчанию:

```text
original = исходный формат
variants = webp
```

### 14.5. metadata.json

Пример `metadata.json`:

```json
{
  "schemaVersion": 1,
  "kind": "RichTextDocumentImage",
  "imageId": "img_01HVK2M8AGM7V75MYZG7F9V5AA",
  "originalFileName": "schema.png",
  "originalExtension": ".png",
  "mimeType": "image/png",
  "sizeBytes": 382910,
  "sha256": "b6f2d1...",
  "width": 2400,
  "height": 1350,
  "variants": {
    "original": {
      "relativePath": "images/img_01HVK2M8AGM7V75MYZG7F9V5AA/original/original.png",
      "width": 2400,
      "height": 1350,
      "mimeType": "image/png"
    },
    "display": {
      "relativePath": "images/img_01HVK2M8AGM7V75MYZG7F9V5AA/variants/display.webp",
      "width": 1600,
      "height": 900,
      "mimeType": "image/webp"
    },
    "preview": {
      "relativePath": "images/img_01HVK2M8AGM7V75MYZG7F9V5AA/variants/preview.webp",
      "width": 800,
      "height": 450,
      "mimeType": "image/webp"
    },
    "thumb": {
      "relativePath": "images/img_01HVK2M8AGM7V75MYZG7F9V5AA/variants/thumb.webp",
      "width": 320,
      "height": 180,
      "mimeType": "image/webp"
    }
  },
  "createdAtUtc": "2026-04-27T00:00:00Z"
}
```

`metadata.json` является техническим файлом, а не бизнес-объектом.

### 14.6. Image block в chunk JSON

В chunk JSON блок изображения хранит только позиционную ссылку на локальный ресурс:

```json
{
  "id": "b_4000",
  "type": "image",
  "imageId": "img_01HVK2M8AGM7V75MYZG7F9V5AA",
  "displayVariant": "display",
  "attrs": {
    "alt": "Схема хранения",
    "caption": "Схема хранения",
    "width": 900,
    "align": "center"
  }
}
```

Сервер при рендере вычисляет путь:

```text
documentId + imageId + displayVariant
```

Например:

```text
/document-files/{documentId}/images/{imageId}/display
```

Этот HTTP endpoint должен сам находить физический файл:

```text
RichDocumentData/{documentId}/images/{imageId}/variants/display.webp
```

### 14.7. Почему не хранить absolute path в блоке

Неправильно:

```json
{
  "type": "image",
  "path": "D:\\AppData\\RichDocumentData\\..."
}
```

Причины:

- путь зависит от сервера;
- перенос приложения ломает документы;
- бэкап и restore становятся сложнее;
- появляются риски path traversal;
- клиенту нельзя знать физическую файловую структуру сервера.

Правильно:

```json
{
  "type": "image",
  "imageId": "img_01HVK2M8AGM7V75MYZG7F9V5AA",
  "displayVariant": "display"
}
```

### 14.8. Алгоритм вставки изображения

Поток вставки:

```text
DocumentEditorMiniApp
    -> пользователь вставил / загрузил изображение
        -> IRichTextDocumentStorageConnector.StoreEmbeddedImageAsync
            -> DataProviderMiniApp
                -> RichTextDocumentFileStorageService
                    -> сохранить original во временную папку
                    -> проверить MIME и размер
                    -> вычислить sha256
                    -> сгенерировать display / preview / thumb
                    -> записать metadata.json
                    -> переместить из _tmp в images/{imageId}
                    -> вернуть EmbeddedImageDescriptor
        -> DocumentEditorMiniApp вставляет image block в текущий chunk
        -> сохранение dirty chunk
```

### 14.9. Транзакционность БД и файлов

Файловая система и Postgres не образуют одну общую транзакцию.

Поэтому используется компенсационная схема:

1. сначала файл пишется во временную папку `_tmp/{uploadId}`;
2. после успешной проверки и генерации вариантов создается финальная папка `images/{imageId}`;
3. редактор получает `imageId`;
4. image block сохраняется в chunk JSON;
5. если сохранение чанка не удалось, файл считается временно неиспользуемым;
6. периодическая cleanup-задача удаляет неиспользуемые image folders.

Для MVP допустимо не удалять orphan images немедленно.

Нужен отдельный cleanup-процесс:

```text
RichTextEmbeddedFileCleanupJob
```

Он может:

- пройти по `RichDocumentData/{documentId}/images`;
- собрать `imageId`;
- сравнить их с `imageId`, реально встречающимися в чанках документа;
- удалить неиспользуемые папки старше заданного возраста.

### 14.10. Удаление документа

При удалении `RichTextDocument` нужно:

1. удалить `BusinessEntityDataChunks`;
2. удалить `BusinessEntityDataItems`;
3. удалить `BusinessEntityRelations`, связанные с документом;
4. удалить `BusinessEntity`;
5. удалить файловую папку:

```text
RichDocumentData/{documentId:N}/
```

Удаление файловой папки должно быть частью storage-операции `DataProviderMiniApp`.

Если удалить файлы не удалось, нужно залогировать ошибку и поставить папку в очередь на повторное удаление.

### 14.11. Бэкап и восстановление

Бэкап rich-text документов должен включать две части:

```text
1. База business_entity
2. Папка RichDocumentData
```

Нельзя считать бэкап полным, если сохранена только база без `RichDocumentData`.

Для restore нужно восстановить:

```text
BusinessEntityDataChunks
RichDocumentData/{documentId}/images
```

Идентификаторы `imageId` должны сохраниться, иначе image block в chunk JSON потеряет файл.

---

## 15. PlainText

`PlainText` в `BusinessEntityDataChunks` — это производное поле.

Оно нужно для:

- полнотекстового поиска;
- быстрого preview в результатах поиска;
- подсветки фрагмента;
- перехода к `documentId + chunkId + blockId`;
- отсутствия необходимости парсить JSON при каждом поиске.

`PlainText` не является источником истины.

Источник истины:

```text
BusinessEntityDataChunks.Data
```

При каждом сохранении чанка нужно пересчитывать:

```text
PlainText
BlockCount
CharCount
DataSizeBytes
Checksum
```

Для `image`-блока в `PlainText` можно включать:

```text
alt
caption
```

---

## 16. HtmlCache

`HtmlCache` — необязательное производное поле.

Логика:

```text
Data       = источник истины
PlainText  = поисковый кеш
HtmlCache  = кеш рендера
```

На первом MVP `HtmlCache` можно не делать.

Если просмотр документов станет частым и тяжелым, можно кешировать готовый HTML по чанку.

Важно: HTML-кеш для изображений должен содержать не физический путь к файлу, а URL endpoint вида:

```text
/document-files/{documentId}/images/{imageId}/{variant}
```

---

## 17. SortOrder

Нельзя использовать простой `chunk_number = 1, 2, 3` как основной порядок.

Правильно:

```text
1000
2000
3000
4000
```

Вставка между `2000` и `3000`:

```text
2500
```

Если места между числами не хватает, выполняется локальная перенумерация соседних чанков.

На MVP достаточно:

```text
SortOrder bigint с шагом 1000
```

Позже можно заменить на fractional key / LexoRank-подобную схему.

---

## 18. Размер чанков

Рекомендуемые значения:

| Параметр | Значение |
|---|---|
| целевой размер `Data` | 64–256 КБ |
| жесткий максимум | 512 КБ или 1 МБ |
| граница разрезания | между блоками |
| обычный запрет | не резать внутри paragraph / heading / image |

Важно:

- изображения не увеличивают размер чанка самим бинарником;
- image block хранит только `imageId` и небольшие атрибуты;
- оригинальные изображения лежат на диске.

Если один текстовый блок становится слишком большим, нужно либо:

- запретить слишком большой одиночный блок;
- либо ввести специальный тип `largeParagraph`;
- либо разрешить внутренние части блока, но не в MVP.

---

## 19. Создание RichTextDocument

Создание большого rich-text документа должно выполняться через `DataProviderMiniApp`.

Минимальный алгоритм:

1. создать `BusinessEntity` с `EntityType = RichTextDocument`;
2. создать `BusinessEntityRelation` типа `Contains` между родителем и документом;
3. создать `BusinessEntityDataDto` с manifest envelope;
4. создать один или несколько начальных чанков в `BusinessEntityDataChunks`;
5. создать папку `RichDocumentData/{documentId:N}/`;
6. при необходимости создать записи outline/search-cache;
7. выполнить DB-операции в одной транзакции;
8. файловую папку при ошибке создания документа удалить компенсационной операцией.

Физически появляются:

```text
BusinessEntities
BusinessEntityRelations
BusinessEntityDataItems
BusinessEntityDataChunks
RichDocumentData/{documentId:N}/
```

Но на уровне бизнес-графа появляется только один документ.

---

## 20. Чтение RichTextDocument

Чтение происходит в два уровня.

### 20.1. Чтение manifest

```text
BusinessEntityDataItems.Data
    -> deserialize envelope
    -> kind == RichTextDocument
    -> payload == manifest
```

### 20.2. Чтение чанков

Первичная загрузка:

```sql
select *
from "BusinessEntityDataChunks"
where "BusinessEntityId" = @documentId
order by "SortOrder"
limit @count;
```

Загрузка следующих чанков:

```sql
select *
from "BusinessEntityDataChunks"
where "BusinessEntityId" = @documentId
  and "SortOrder" > @afterSortOrder
order by "SortOrder"
limit @count;
```

Загрузка предыдущих чанков:

```sql
select *
from "BusinessEntityDataChunks"
where "BusinessEntityId" = @documentId
  and "SortOrder" < @beforeSortOrder
order by "SortOrder" desc
limit @count;
```

### 20.3. Чтение изображений

Изображения читаются не из БД, а через отдельный файловый endpoint:

```text
GET /document-files/{documentId}/images/{imageId}/{variant}
```

Пример:

```text
GET /document-files/0f8fad5b-d9cb-469f-a165-70867728950e/images/img_01HVK2M8AGM7V75MYZG7F9V5AA/display
```

Endpoint обязан:

- проверить права доступа пользователя к документу;
- проверить, что `imageId` не содержит path traversal;
- найти файл только внутри `RichDocumentData/{documentId}/images/{imageId}`;
- отдать нужный variant;
- не раскрывать абсолютный путь на сервере.

---

## 21. Сохранение RichTextDocument

Редактор не сохраняет весь документ целиком.

Он сохраняет только измененные чанки.

Алгоритм сохранения одного чанка:

1. получить измененный `DocumentChunk` из редакторной модели;
2. нормализовать blocks;
3. очистить HTML по whitelist;
4. проверить, что block types входят в разрешенный MVP-набор;
5. проверить, что image blocks ссылаются только на локальные `imageId`;
6. сериализовать chunk envelope через `StorageJsonOptions.Default`;
7. извлечь `PlainText`;
8. пересчитать `BlockCount`, `CharCount`, `DataSizeBytes`, `Checksum`;
9. проверить `Version`;
10. обновить строку `BusinessEntityDataChunks`;
11. увеличить `Version`;
12. обновить `LastModifiedDate` чанка;
13. обновить `LastModifiedDate` документа;
14. пересчитать outline/search-cache для затронутого чанка.

Пример SQL:

```sql
update "BusinessEntityDataChunks"
set
    "Data" = @data,
    "PlainText" = @plainText,
    "HtmlCache" = @htmlCache,
    "BlockCount" = @blockCount,
    "CharCount" = @charCount,
    "DataSizeBytes" = @dataSizeBytes,
    "Checksum" = @checksum,
    "Version" = "Version" + 1,
    "LastModifiedDate" = now()
where "Id" = @chunkId
  and "BusinessEntityId" = @documentId
  and "Version" = @expectedVersion;
```

Если обновлено 0 строк, значит произошел конфликт версий.

---

## 22. Split / Merge / Rebalance чанков

### 22.1. Split

Если чанк превысил `maxChunkSizeKb`, он режется между блоками.

Было:

```text
Chunk 010
    Paragraph 1
    Paragraph 2
    Paragraph 3
    Paragraph 4
```

Стало:

```text
Chunk 010
    Paragraph 1
    Paragraph 2

Chunk 011
    Paragraph 3
    Paragraph 4
```

Новому чанку дается `SortOrder` между соседями.

### 22.2. Merge

Если после удаления или редактирования два соседних чанка стали слишком маленькими, их можно объединить.

### 22.3. Rebalance

Rebalance должен быть внутренней storage-операцией `DataProviderMiniApp`.

Редактор не должен напрямую управлять физической перекладкой строк в таблице.

Редактор сообщает:

```text
вот измененный диапазон блоков
```

Storage-слой решает:

```text
какие чанки обновить, разделить, объединить или перенумеровать
```

---

## 23. Версионирование

MVP-вариант:

```text
текущие чанки mutable
старая версия чанка перед обновлением копируется в history
```

Рекомендуемая техническая таблица:

```text
BusinessEntityDataChunkHistory
```

Поля:

```text
Id
ChunkId
BusinessEntityId
OldData
OldPlainText
Version
CreatedDate
CreatedBy
```

История чанков также не является частью бизнес-графа.

Она обслуживается `DataProviderMiniApp`.

Файлы изображений в MVP можно не версионировать.

Базовое правило:

```text
chunk history хранит историю ссылок imageId,
а файловое хранилище хранит текущие файлы imageId.
```

Если в будущем нужна полная история файлов, можно добавить immutable image storage:

```text
images/{imageId}/revisions/{revisionId}/...
```

Но это не входит в MVP.

---

## 24. Оглавление

Для больших документов нельзя каждый раз строить оглавление путем чтения всех чанков.

Рекомендуется технический кеш:

```text
RichTextDocumentOutlineItems
```

Поля:

```text
Id
BusinessEntityId
ChunkId
BlockId
Level
Title
SortOrder
```

При сохранении чанка:

1. удалить старые outline-записи этого чанка;
2. извлечь heading-блоки из нового chunk data;
3. записать новые outline-записи.

Outline-кеш не является источником истины.

Источник истины — chunk data.

---

## 25. Поиск

Поиск по большим rich-text документам должен идти по `PlainText`, а не по raw JSON.

Запрос должен возвращать как минимум:

```text
BusinessEntityId
ChunkId
BlockId, если возможно
Snippet
```

Ссылка на результат поиска должна вести не просто на документ:

```text
/document/doc_001
```

а на конкретное место:

```text
/document/doc_001?chunk=chunk_025&block=b_2504
```

При открытии такой ссылки редактор или просмотрщик загружает:

```text
chunk_024
chunk_025
chunk_026
```

и скроллит к `blockId`.

---

## 26. Соответствие архитектуре MiniApp + ReactiveBus

Чанковое хранение должно быть встроено в MiniApp-архитектуру.

Запрещено:

- давать Blazor-компоненту прямой доступ к EF-репозиториям;
- давать `DocumentEditorMiniApp` прямой доступ к Postgres;
- давать `DocumentEditorMiniApp` прямой доступ к физической файловой системе;
- инжектить в UI набор из 10 сервисов для документа, поиска, storage и файлов;
- делать `RichTextDocumentService`, который знает все внутренности всех MiniApp;
- делать универсальный `EverythingDocumentManager`.

Правильно:

```text
DocumentEditorMiniApp
    отвечает за редактор, viewport, dirty chunks, adapter

DataProviderMiniApp
    отвечает за физическое хранение manifest, chunks и локальных файлов документа

SearchMiniApp
    отвечает за поиск и индексацию

ReactiveBus
    переносит команды, события, запросы и ответы

Connector
    используется для компактных адресных вызовов storage capability
```

Отдельный `MediaMiniApp` для MVP не нужен, потому что вставленные изображения не являются бизнес-медиа-объектами.

Позже, когда появится полноценная библиотека медиа или встраивание бизнес-сущностей, можно будет выделить отдельный MiniApp.

---

## 27. Ответственность DataProviderMiniApp

`DataProviderMiniApp` владеет storage-контуром.

Он отвечает за:

- чтение и запись `BusinessEntityDto`;
- чтение и запись `BusinessEntityDataDto`;
- чтение и запись `BusinessEntityRelationDto`;
- чтение и запись `BusinessEntityDataChunkDto`;
- сериализацию manifest envelope;
- сериализацию chunk envelope;
- `StorageJsonOptions.Default`;
- optimistic concurrency по `Version`;
- split / merge / rebalance чанков;
- пересчет `PlainText`;
- пересчет `HtmlCache`, если используется;
- обновление outline-cache;
- обновление history;
- создание папки `RichDocumentData/{documentId}`;
- сохранение original image;
- генерацию display / preview / thumb variants;
- чтение embedded image variants через безопасный API;
- cleanup orphan image folders;
- транзакционность DB-операций и компенсационные файловые операции.

Внутри `DataProviderMiniApp` могут быть внутренние сервисы:

```text
RichTextDocumentStorageService
RichTextChunkRepository
RichTextChunkSerializer
RichTextPlainTextExtractor
RichTextChunkRebalancer
RichTextOutlineUpdater
RichTextChunkHistoryWriter
RichTextDocumentFileStorageService
RichTextImageVariantGenerator
RichTextEmbeddedFileCleanupService
```

Эти сервисы не должны напрямую инжектиться в UI или другие MiniApp.

---

## 28. Ответственность DocumentEditorMiniApp

`DocumentEditorMiniApp` отвечает за редакторную сторону.

Он отвечает за:

- открытие документа в редакторе;
- загрузку manifest;
- загрузку начального окна чанков;
- подгрузку следующих/предыдущих чанков;
- хранение `DocumentViewportState`;
- отслеживание dirty chunks;
- mapping `blockId -> chunkId`;
- адаптацию между editor state и chunk payload;
- команды UI: heading, bold, italic, underline, insert image;
- отправку изображения на сохранение через Connector;
- вставку image block после получения `imageId`;
- сохранение измененных чанков через Connector или Bus;
- работу с selection в пределах загруженного окна;
- запрос серверных операций для больших диапазонов.

`DocumentEditorMiniApp` не отвечает за:

- EF Core;
- SQL;
- физические таблицы;
- миграции;
- прямое обновление `BusinessEntityDataChunks`;
- физическое размещение image files на диске.

---

## 29. Connector для rich-text storage

Для адресных операций между `DocumentEditorMiniApp` и `DataProviderMiniApp` допустим маленький Connector.

Например:

```csharp
public interface IRichTextDocumentStorageConnector
{
    Task<RichTextDocumentManifestDto> LoadManifestAsync(
        Guid documentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RichTextChunkDto>> LoadInitialChunksAsync(
        Guid documentId,
        int count,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RichTextChunkDto>> LoadNextChunksAsync(
        Guid documentId,
        long afterSortOrder,
        int count,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RichTextChunkDto>> LoadPreviousChunksAsync(
        Guid documentId,
        long beforeSortOrder,
        int count,
        CancellationToken cancellationToken);

    Task<SaveRichTextChunksResult> SaveChunksAsync(
        Guid documentId,
        IReadOnlyList<SaveRichTextChunkRequest> chunks,
        CancellationToken cancellationToken);

    Task<EmbeddedImageDescriptorDto> StoreEmbeddedImageAsync(
        Guid documentId,
        Stream imageStream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken);

    Task<EmbeddedImageReadResult> ReadEmbeddedImageVariantAsync(
        Guid documentId,
        string imageId,
        string variant,
        CancellationToken cancellationToken);
}
```

Правила Connector:

- маленький контракт;
- только публичная storage capability;
- не раскрывает внутренние сервисы `DataProviderMiniApp`;
- не возвращает EF entities;
- не возвращает внутренние tracked DTO;
- не возвращает абсолютные пути файловой системы;
- не превращается в обход границ MiniApp.

---

## 30. Message contracts

Для ReactiveBus сообщения должны быть строго типизированными.

Примеры команд:

```csharp
public sealed record OpenRichTextDocumentCommand(Guid DocumentId);

public sealed record SaveRichTextChunksCommand(
    Guid DocumentId,
    IReadOnlyList<SaveRichTextChunkRequest> Chunks);

public sealed record LoadNextRichTextChunksCommand(
    Guid DocumentId,
    long AfterSortOrder,
    int Count);

public sealed record InsertEmbeddedImageCommand(
    Guid DocumentId,
    Stream ImageStream,
    string OriginalFileName,
    string ContentType);
```

Примеры событий:

```csharp
public sealed record RichTextDocumentOpenedEvent(Guid DocumentId);

public sealed record RichTextChunksLoadedEvent(
    Guid DocumentId,
    IReadOnlyList<RichTextChunkDto> Chunks);

public sealed record RichTextChunksSavedEvent(
    Guid DocumentId,
    IReadOnlyList<Guid> ChunkIds);

public sealed record RichTextChunkSaveFailedEvent(
    Guid DocumentId,
    Guid ChunkId,
    string Error);

public sealed record EmbeddedImageStoredEvent(
    Guid DocumentId,
    string ImageId);
```

Примеры request/response:

```csharp
public sealed record RichTextChunksRequest(
    Guid RequestId,
    Guid DocumentId,
    long? AfterSortOrder,
    long? BeforeSortOrder,
    int Count);

public sealed record RichTextChunksResponse(
    Guid RequestId,
    Guid DocumentId,
    IReadOnlyList<RichTextChunkDto> Chunks);
```

Запрещено:

```csharp
public sealed class GenericMessage
{
    public string Type { get; set; }
    public object Payload { get; set; }
}
```

---

## 31. Поток открытия документа

```text
UI / Blazor Component
    -> DocumentEditorMiniApp
        -> IRichTextDocumentStorageConnector.LoadManifestAsync
            -> DataProviderMiniApp
                -> BusinessEntityDataItems.Data
                -> deserialize RichTextDocument manifest
        -> LoadInitialChunksAsync
            -> DataProviderMiniApp
                -> BusinessEntityDataChunks
                -> deserialize chunk envelopes
        -> DocumentEditorAdapter
            -> chunks -> editor state
        -> UI показывает документ
```

Blazor-компонент остается тонким.

Он не знает:

- SQL;
- таблицы чанков;
- envelope serialization;
- split / merge / rebalance;
- физическую структуру папки `RichDocumentData`.

---

## 32. Поток сохранения документа

```text
Editor engine
    -> changed block b_1602
        -> DocumentEditorMiniApp определяет chunk_16
            -> dirtyChunks добавляет chunk_16
                -> autosave
                    -> IRichTextDocumentStorageConnector.SaveChunksAsync
                        -> DataProviderMiniApp
                            -> validate versions
                            -> sanitize html
                            -> validate allowed block types
                            -> serialize chunk envelope
                            -> update BusinessEntityDataChunks
                            -> update PlainText
                            -> update outline/search cache
                            -> publish RichTextChunksSavedEvent
```

Сохраняется не весь документ, а только dirty chunks.

---

## 33. Поток вставки изображения

```text
User вставил изображение
    -> DocumentEditorMiniApp
        -> StoreEmbeddedImageAsync(documentId, stream, fileName, contentType)
            -> DataProviderMiniApp
                -> RichTextDocumentFileStorageService
                    -> save original
                    -> generate variants
                    -> write metadata.json
                    -> return imageId
        -> DocumentEditorMiniApp
            -> insert image block with imageId
            -> mark current chunk dirty
            -> SaveChunksAsync
```

Image block появляется в chunk JSON только после того, как файловое хранилище вернуло `imageId`.

---

## 34. Поток скролла и подгрузки чанков

`DocumentEditorMiniApp` хранит состояние viewport.

Пример:

```csharp
public sealed class DocumentViewportState
{
    public Guid DocumentId { get; set; }

    public List<LoadedRichTextChunk> LoadedChunks { get; set; } = new();

    public HashSet<Guid> DirtyChunkIds { get; set; } = new();

    public long? MinLoadedSortOrder { get; set; }

    public long? MaxLoadedSortOrder { get; set; }

    public bool IsLoadingNext { get; set; }

    public bool IsLoadingPrevious { get; set; }
}
```

При скролле вниз:

```text
Browser bottom sentinel visible
    -> DocumentViewportService.LoadNextAsync
        -> LoadNextChunksAsync(documentId, maxSortOrder, count)
            -> DataProviderMiniApp читает следующие чанки
                -> DocumentEditorAdapter добавляет блоки в editor state
```

БД сама ничего не узнает.

Решение о подгрузке принимает клиентский/editor-side viewport service.

---

## 35. Редактор и чанки

Редактор не должен воспринимать документ как один огромный HTML.

Правильная модель:

```text
Редактор получает окно чанков
    chunk_15
    chunk_16
    chunk_17

Адаптер превращает их в editor state

Пользователь редактирует блок

Адаптер понимает:
    blockId -> chunkId

Сохраняется только измененный chunk
```

В редакторе границы чанков могут быть невидимыми для пользователя.

Но система должна знать:

```text
block b_1602 принадлежит chunk_16
```

---

## 36. MVP-стратегия редактора

Для первого MVP допустим упрощенный вариант:

```text
1. Грузим manifest.
2. Грузим первые 5 чанков.
3. Показываем их в редакторе.
4. При скролле вниз подгружаем еще 3 чанка.
5. При скролле вверх подгружаем предыдущие 3 чанка.
6. Пока не выгружаем старые чанки из DOM.
7. Сохраняем только dirty chunks.
8. Изображения вставляем как image block с локальным imageId.
```

Позже добавить:

```text
9. выгрузку дальних сохраненных чанков;
10. spacer-ы для компенсации высоты;
11. серверные операции для огромных диапазонов;
12. более умную виртуализацию.
```

---

## 37. Большие выделения

Обычное выделение внутри загруженного окна может обрабатываться редактором.

Большое выделение за пределами загруженных чанков должно превращаться в серверную операцию над диапазоном.

Пример:

```json
{
  "documentId": "doc_001",
  "range": {
    "startBlockId": "b_1502",
    "startOffset": 10,
    "endBlockId": "b_8901",
    "endOffset": 25
  },
  "operation": {
    "type": "applyMark",
    "mark": "bold"
  }
}
```

В MVP разрешенные операции над диапазоном:

```text
bold
italic
underline
delete
copy
```

Такая операция должна выполняться `DataProviderMiniApp` или отдельным внутренним сервисом документа, но не через DOM-команду браузера.

---

## 38. Вставка большого текста

Если пользователь вставляет большой HTML / Markdown / plain text:

1. `DocumentEditorMiniApp` принимает вставку;
2. importer разбирает вход в blocks;
3. HTML очищается;
4. разрешенные текстовые элементы сохраняются;
5. изображения сохраняются через `StoreEmbeddedImageAsync`;
6. все неподдерживаемые элементы удаляются или превращаются в plain text;
7. блоки передаются в storage-operation;
8. `DataProviderMiniApp` раскладывает блоки по чанкам;
9. создаются / обновляются строки `BusinessEntityDataChunks`;
10. пересчитываются `PlainText`, outline и search-cache.

Для больших вставок не нужно сначала собирать один огромный HTML.

---

## 39. Запреты

Запрещено:

- хранить большой rich-text документ целиком в `BusinessEntityDataDto.Data`;
- хранить большой документ как один HTML string;
- хранить большой документ как один огромный JSON payload;
- хранить картинки base64 внутри chunk data;
- делать каждый чанк отдельным `BusinessEntity`;
- делать вставленные изображения отдельными `BusinessEntity`;
- делать связи изображений через `BusinessEntityRelation`;
- делать макросы в MVP;
- делать `codeBlock`, `quote`, `divider`, `table`, `list`, `embed` в MVP;
- давать UI прямой доступ к `BusinessEntityDataChunks`;
- давать UI прямой доступ к `RichDocumentData` на диске;
- давать `DocumentEditorMiniApp` прямой доступ к EF/Postgres;
- сериализовать chunk data не через `StorageJsonOptions.Default`;
- хранить CLR full type name в `kind`;
- использовать generic bus messages с `object Payload`;
- превращать Connector в backdoor к внутренностям `DataProviderMiniApp`.

---

## 40. Разрешено

Разрешено:

- иметь техническую таблицу `BusinessEntityDataChunks`;
- иметь технический DTO storage-слоя для чанков;
- хранить manifest в `BusinessEntityDataDto.Data`;
- хранить chunk data как minified JSON envelope;
- хранить `PlainText` как производное поле;
- хранить `HtmlCache` как производный кеш;
- использовать `SortOrder` с промежутками;
- использовать optimistic concurrency по `Version`;
- хранить вставленные изображения в файловой папке документа;
- хранить original image в полном разрешении;
- хранить адаптированные варианты изображения;
- использовать `imageId` как локальный id изображения внутри документа;
- использовать `DataProviderMiniApp` как владельца storage-логики;
- использовать `DocumentEditorMiniApp` как владельца editor/viewport-логики;
- использовать Connector для адресных storage-вызовов;
- использовать ReactiveBus для событий, команд, запросов и ответов между MiniApp.

---

## 41. Рекомендуемая структура кода

```text
src/
 ├── Host/
 │   ├── WebHost/
 │   └── WpfHost/
 │
 ├── Infrastructure/
 │   ├── ReactiveBus/
 │   ├── Persistence/
 │   ├── Json/
 │   └── FileStorage/
 │
 ├── MiniApps/
 │   ├── DataProviderMiniApp/
 │   │   ├── Contracts/
 │   │   │   ├── Connectors/
 │   │   │   │   └── IRichTextDocumentStorageConnector.cs
 │   │   │   ├── Messages/
 │   │   │   └── Dtos/
 │   │   ├── Internal/
 │   │   │   ├── RichTextDocumentStorageService.cs
 │   │   │   ├── RichTextChunkSerializer.cs
 │   │   │   ├── RichTextPlainTextExtractor.cs
 │   │   │   ├── RichTextChunkRebalancer.cs
 │   │   │   ├── RichTextOutlineUpdater.cs
 │   │   │   ├── RichTextDocumentFileStorageService.cs
 │   │   │   ├── RichTextImageVariantGenerator.cs
 │   │   │   └── RichTextEmbeddedFileCleanupService.cs
 │   │   ├── Repositories/
 │   │   │   └── BusinessEntityDataChunkRepository.cs
 │   │   └── Registration/
 │   │
 │   ├── DocumentEditorMiniApp/
 │   │   ├── Contracts/
 │   │   ├── Internal/
 │   │   │   ├── DocumentViewportService.cs
 │   │   │   ├── DocumentEditorAdapter.cs
 │   │   │   └── DirtyChunkTracker.cs
 │   │   └── Registration/
 │   │
 │   └── SearchMiniApp/
```

---

## 42. Минимальные классы storage-слоя

### 42.1. Chunk DTO

```csharp
public sealed class BusinessEntityDataChunkDto
{
    public Guid Id { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime LastModifiedDate { get; set; }

    public Guid BusinessEntityId { get; set; }

    public long SortOrder { get; set; }

    public string Data { get; set; } = string.Empty;

    public string? PlainText { get; set; }

    public string? HtmlCache { get; set; }

    public int BlockCount { get; set; }

    public int CharCount { get; set; }

    public int DataSizeBytes { get; set; }

    public int Version { get; set; }

    public string? Checksum { get; set; }
}
```

### 42.2. Runtime chunk model

```csharp
public sealed class RichTextDocumentChunk
{
    public List<RichTextBlock> Blocks { get; set; } = new();
}
```

### 42.3. Block base

```csharp
public abstract record RichTextBlock(string Id, string Type);
```

Примеры:

```csharp
public sealed record ParagraphBlock(
    string Id,
    string Html) : RichTextBlock(Id, "paragraph");

public sealed record HeadingBlock(
    string Id,
    int Level,
    string Html) : RichTextBlock(Id, "heading");

public sealed record ImageBlock(
    string Id,
    string ImageId,
    string DisplayVariant,
    ImageBlockAttrs Attrs) : RichTextBlock(Id, "image");

public sealed record ImageBlockAttrs(
    string? Alt,
    string? Caption,
    int? Width,
    string? Align);
```

### 42.4. Embedded image descriptor

```csharp
public sealed record EmbeddedImageDescriptorDto(
    string ImageId,
    string OriginalFileName,
    string MimeType,
    long SizeBytes,
    int Width,
    int Height,
    IReadOnlyDictionary<string, EmbeddedImageVariantDto> Variants);

public sealed record EmbeddedImageVariantDto(
    string Variant,
    string RelativePath,
    int Width,
    int Height,
    string MimeType);
```

---

## 43. Минимальный pipeline записи

```text
DocumentEditorMiniApp
    -> SaveRichTextChunksCommand / IRichTextDocumentStorageConnector
        -> DataProviderMessageHandler
            -> RichTextDocumentStorageService
                -> RichTextChunkSerializer
                    -> StorageJsonOptions.Default
                -> RichTextPlainTextExtractor
                -> RichTextChunkRepository
                -> Postgres
```

Важно:

`DocumentEditorMiniApp` не формирует SQL и не знает таблицы.

---

## 44. Минимальный pipeline загрузки изображения

```text
DocumentEditorMiniApp
    -> StoreEmbeddedImageAsync
        -> DataProviderMiniApp
            -> RichTextDocumentFileStorageService
                -> RichDocumentData/{documentId}/images/{imageId}
                -> original
                -> display / preview / thumb
                -> metadata.json
            -> EmbeddedImageDescriptorDto
    -> insert image block
    -> save dirty chunk
```

---

## 45. Минимальный pipeline чтения

```text
DocumentEditorMiniApp
    -> LoadManifestAsync
        -> DataProviderMiniApp
            -> BusinessEntityDataItems
            -> envelope RichTextDocument

DocumentEditorMiniApp
    -> LoadInitialChunksAsync / LoadNextChunksAsync
        -> DataProviderMiniApp
            -> BusinessEntityDataChunks
            -> envelope RichTextDocumentChunk
            -> RichTextChunkDto

Browser
    -> GET /document-files/{documentId}/images/{imageId}/{variant}
        -> DataProviderMiniApp / file endpoint
            -> check permissions
            -> resolve safe path
            -> stream file
```

---

## 46. Совместимость со старым Document

Старый `Document` с payload:

```json
{"text":"...","tag":"Document"}
```

остается валидным для простого plain-text документа.

Новый `RichTextDocument` не должен притворяться старым `Document`.

Правильно разделить:

```text
Document
    = простой plain text

RichTextDocument
    = manifest + chunks + local embedded files
```

Если нужна миграция:

1. прочитать старый `payload.text`;
2. создать `RichTextDocument` manifest;
3. разбить text на paragraph-блоки;
4. создать чанки;
5. записать `BusinessEntityDataChunks`;
6. создать папку `RichDocumentData/{documentId}`;
7. изменить kind на `RichTextDocument` или создать новую сущность.

---

## 47. Минимальный MVP

Для первой версии достаточно:

### Таблицы

```text
BusinessEntities
BusinessEntityRelations
BusinessEntityDataItems
BusinessEntityDataChunks
```

### Файловая папка

```text
RichDocumentData/{documentId:N}/images/{imageId}/
```

### Блоки

```text
paragraph
heading
image
```

### Inline-форматирование

```text
bold
italic
underline
```

### Изображения

```text
original
display
preview
thumb
metadata.json
```

### Функции

```text
create rich document
load manifest
load initial chunks
load next chunks
save dirty chunks
extract plain text
optimistic version check
basic outline cache
insert image
store original image
generate adapted image variants
serve image variant through safe endpoint
delete document folder on document deletion
```

### Пока не делать

```text
macros
tables
code blocks
quotes
dividers
lists
links
business entity embeds
real-time collaboration
full virtualized editing window
large cross-document copy/paste
advanced history UI
server-side range formatting beyond MVP marks
image file revision history
global media library
```

---

## 48. Итоговое нормативное правило

Большой rich-text документ в системе хранится не как один payload.

Нормативная модель:

```text
BusinessEntity
    хранит identity документа и участвует в графе

BusinessEntityDataDto.Data
    хранит versioned JSON envelope manifest

BusinessEntityDataChunks
    хранит технические чанки документа

BusinessEntityDataChunk.Data
    хранит versioned JSON envelope chunk payload

Chunk payload
    хранит массив blocks

Разрешенные blocks MVP
    paragraph / heading / image

Разрешенное inline-форматирование MVP
    bold / italic / underline

Вставленные изображения
    не являются BusinessEntity
    не имеют BusinessEntityRelation
    хранятся в RichDocumentData/{documentId}/images/{imageId}

PlainText / HtmlCache / Outline
    являются производными кешами

DataProviderMiniApp
    владеет storage-логикой, chunk-логикой и файловым хранилищем документа

DocumentEditorMiniApp
    владеет editor/viewport-логикой

ReactiveBus / Connector
    обеспечивают взаимодействие между MiniApp
```

Это сохраняет текущую графовую модель `BusinessEntity`, не ломает envelope-storage, не превращает DI в монолит и дает основу для практически неограниченного rich-text документа с простым MVP-набором форматирования и локальным хранением вставленных изображений.

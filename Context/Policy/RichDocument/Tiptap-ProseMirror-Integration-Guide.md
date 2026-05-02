# Краткое руководство по интеграции Tiptap / ProseMirror в чанковую систему RichText-документов

## 1. Цель

Интегрировать редактор **Tiptap / ProseMirror** для редактирования одного чанка rich-text документа.

Редактор не должен открывать весь документ целиком.  
Редактор должен работать только с одним загруженным чанком или небольшой группой чанков.

```text
Document
└─ BusinessEntityData = manifest
   └─ BusinessEntityDataChunk[]
      ├─ chunk_json
      ├─ plain_text
      ├─ sort_order
      └─ version
```

Tiptap внутри работает со строгой ProseMirror-схемой: документ состоит из nodes и marks, а контент, не разрешённый схемой, может быть отброшен. Поэтому перед загрузкой чанка в редактор нужно привести наш формат к Tiptap JSON или валидному HTML, поддерживаемому зарегистрированными extensions.

---

## 2. Исходный формат чанка

Сейчас типовой чанк выглядит так:

```json
{
  "schemaVersion": 1,
  "kind": "RichTextDocumentChunk",
  "payload": {
    "blocks": [
      {
        "kind": "paragraph",
        "level": 0,
        "html": "Стр. 10, строка 38.",
        "imageId": "",
        "displayVariant": "original",
        "altText": ""
      }
    ]
  }
}
```

Для интеграции с Tiptap нужно добавить стабильный `id` каждому блоку.

Целевой формат:

```json
{
  "schemaVersion": 1,
  "kind": "RichTextDocumentChunk",
  "payload": {
    "blocks": [
      {
        "id": "b_000001",
        "kind": "paragraph",
        "level": 0,
        "html": "Стр. 10, строка 38."
      },
      {
        "id": "b_000002",
        "kind": "paragraph",
        "level": 0,
        "html": "<em>Вместо (в сноске):</em> отдали --- <em>в изд. 69 г.:</em> предали"
      }
    ]
  }
}
```

`id` нужен для комментариев, поиска, ссылок на место в документе, истории изменений и сохранения идентичности блока после редактирования.

---

## 3. Общая схема интеграции

```text
RichTextDocumentChunk
        ↓
chunkToTiptapJson(...)
        ↓
Tiptap Editor
        ↓
editor.getJSON()
        ↓
tiptapJsonToChunk(...)
        ↓
RichTextDocumentChunk
        ↓
Save chunk
```

Tiptap умеет принимать контент как HTML или JSON через `setContent`, а текущий документ можно получить через `editor.getJSON()` или `editor.getHTML()`. Для нашей системы предпочтительнее работать через JSON, потому что так проще сохранить block id, типы блоков и attrs.

---

## 4. Что именно редактирует Tiptap

Tiptap редактирует не весь документ, а только один чанк:

```text
ChunkEditor
├─ получает RichTextDocumentChunk
├─ конвертирует его в Tiptap JSON
├─ создаёт Tiptap editor
├─ пользователь редактирует
├─ по debounce / blur / save получает editor.getJSON()
├─ конвертирует обратно в RichTextDocumentChunk
└─ отправляет на сервер только изменённый chunk
```

Не отправлять изменения в Blazor Server на каждый keypress.  
Нужно держать состояние редактора в JS и отправлять данные на сервер только по debounce, blur, Ctrl+S или явной кнопке Save.

---

## 5. Минимальный набор Tiptap extensions

Для MVP использовать:

```text
StarterKit
Underline
Link
TableKit
CustomImageNode
UniqueID или собственная логика blockId
```

`TableKit` включает основные table nodes: `Table`, `TableRow`, `TableCell`, `TableHeader`; также есть команды вставки таблицы, добавления/удаления строк и колонок, merge/split cells.

`UniqueID` можно использовать для автоматического добавления ID к paragraph/heading/table/image nodes, но лучше явно синхронизировать эти ID с нашим `block.id`.

---

## 6. Маппинг блоков

### Наш block → Tiptap node

```text
paragraph
    -> paragraph
    -> attrs.blockId = block.id

heading
    -> heading
    -> attrs.level = block.level
    -> attrs.blockId = block.id

image
    -> customImage
    -> attrs.blockId = block.id
    -> attrs.imageId = block.imageId
    -> attrs.displayVariant = block.displayVariant
    -> attrs.altText = block.altText

table
    -> table / tableRow / tableCell
    -> attrs.blockId = block.id

codeBlock
    -> codeBlock
    -> attrs.blockId = block.id
```

---

## 7. Пример Tiptap JSON для нашего чанка

```json
{
  "type": "doc",
  "content": [
    {
      "type": "paragraph",
      "attrs": {
        "blockId": "b_000001"
      },
      "content": [
        {
          "type": "text",
          "text": "Стр. 10, строка 38."
        }
      ]
    },
    {
      "type": "paragraph",
      "attrs": {
        "blockId": "b_000002"
      },
      "content": [
        {
          "type": "text",
          "text": "Вместо (в сноске):",
          "marks": [
            {
              "type": "italic"
            }
          ]
        },
        {
          "type": "text",
          "text": " отдали --- "
        },
        {
          "type": "text",
          "text": "в изд. 69 г.:",
          "marks": [
            {
              "type": "italic"
            }
          ]
        },
        {
          "type": "text",
          "text": " предали"
        }
      ]
    }
  ]
}
```

---

## 8. JS-модуль для Blazor-интеграции

Создать файл:

```text
wwwroot/js/tiptap-editor.js
```

Минимальный публичный API модуля:

```js
export function createEditor(element, dotNetRef, initialChunk, options) {
  // 1. convert initialChunk -> tiptapJson
  // 2. create Editor
  // 3. subscribe to update / blur
  // 4. return editor handle/id
}

export function setChunk(editorId, chunk) {
  // replace current editor content with new chunk
}

export function getChunk(editorId) {
  // editor.getJSON()
  // convert Tiptap JSON -> RichTextDocumentChunk
  // return chunk
}

export function destroyEditor(editorId) {
  // editor.destroy()
}
```

---

## 9. Blazor-компонент

Создать компонент:

```text
Components/RichText/TiptapChunkEditor.razor
```

Ответственность компонента:

```text
- принять ChunkId и RichTextDocumentChunk;
- загрузить JS module через IJSRuntime;
- создать editor после первого render;
- принять callback OnChunkChanged;
- сохранить chunk через application service;
- уничтожить editor при DisposeAsync.
```

Пример API компонента:

```razor
<TiptapChunkEditor
    ChunkId="@chunk.Id"
    Chunk="@chunk.Json"
    ReadOnly="@false"
    OnChunkChanged="@HandleChunkChanged" />
```

---

## 10. Политика сохранения

Не сохранять весь документ.

Сохранять только изменённый чанк:

```text
on editor update:
    mark chunk as dirty

after debounce 1000-2000 ms:
    get current chunk from editor
    send SaveChunkCommand

on blur:
    force save dirty chunk

on Ctrl+S:
    force save dirty chunk
```

Save pipeline:

```text
1. editor.getJSON()
2. tiptapJsonToChunk(...)
3. sanitize HTML / attrs
4. extract plain_text
5. update chunk_json
6. update plain_text
7. increment version
8. update outline/search cache if needed
```

---

## 11. Санитизация HTML

Даже если Tiptap ограничивает схему, на сервере всё равно нужно санитайзить результат.

Разрешить для inline HTML:

```text
strong
b
em
i
u
s
code
a[href]
br
span[data-*] — только при необходимости
```

Запретить:

```text
script
style
iframe
object
embed
form
input
button
onclick / onload / on*
javascript:
base64 images
```

Картинки не хранить как `<img src="base64...">`.

Картинки должны быть отдельными блоками:

```json
{
  "id": "b_000010",
  "kind": "image",
  "imageId": "img_123",
  "displayVariant": "original",
  "altText": "Скриншот"
}
```

---

## 12. Политика переносов строк

В импортированных чанках могут встречаться `\n` внутри `html`.

Нужно явно решить:

```text
если перенос незначим:
    заменить \n на пробел

если перенос значим:
    заменить \n на <br>

если это codeBlock:
    хранить переносы как plain text внутри codeBlock
```

Не оставлять это на случайное поведение HTML-парсера.

---

## 13. Таблицы

Таблицу не хранить внутри paragraph.

Правильно:

```json
{
  "id": "b_000100",
  "kind": "table",
  "level": 0,
  "html": "<table><tbody><tr><td>...</td></tr></tbody></table>"
}
```

Для MVP допустимо хранить таблицу как sanitized `table.html`.

Позже лучше перейти к структурной модели:

```json
{
  "id": "b_000100",
  "kind": "table",
  "table": {
    "rows": [
      {
        "cells": [
          {
            "html": "Ячейка 1"
          },
          {
            "html": "Ячейка 2"
          }
        ]
      }
    ]
  }
}
```

---

## 14. Custom image node

Сделать кастомный Tiptap node:

```text
name: customImage
group: block
atom: true
selectable: true
draggable: true
attrs:
    blockId
    imageId
    displayVariant
    altText
```

Этот node не должен хранить binary/base64.

Он должен рендерить картинку по URL, который строится из `imageId` и `displayVariant`.

---

## 15. Правила для block id

Обязательные правила:

```text
- существующий block.id нельзя менять без причины;
- новый блок получает новый id;
- при split paragraph:
    старый блок сохраняет id;
    новый блок получает новый id;
- при merge paragraphs:
    результирующий блок сохраняет id первого блока;
- image/table/codeBlock всегда имеют собственный id;
- block.id должен переживать сохранение, перезагрузку и повторное открытие редактора.
```

---

## 16. Не делать

```text
НЕ загружать весь документ в один Tiptap editor.

НЕ считать Tiptap JSON единственной истиной без отдельного решения о миграции формата.

НЕ отправлять каждое изменение в Blazor Server через SignalR.

НЕ хранить картинки base64 внутри html.

НЕ хранить таблицы внутри paragraph.html.

НЕ терять block.id при roundtrip.

НЕ полагаться на то, что HTML после Tiptap вернётся byte-to-byte таким же.
```

---

## 17. Минимальные файлы реализации

```text
/wwwroot/js/tiptap-editor.js
/wwwroot/js/richTextChunkMapper.js

/Components/RichText/TiptapChunkEditor.razor
/Components/RichText/TiptapChunkEditor.razor.cs

/Application/RichText/SaveChunkCommand.cs
/Application/RichText/SaveChunkHandler.cs

/Domain/RichText/RichTextDocumentChunk.cs
/Domain/RichText/RichTextBlock.cs
/Domain/RichText/RichTextBlockKind.cs
```

---

## 18. Минимальные тесты

Нужно покрыть roundtrip:

```text
RichTextDocumentChunk -> Tiptap JSON -> RichTextDocumentChunk
```

Тест-кейсы:

```text
1. plain paragraph
2. paragraph with <em>
3. paragraph with <strong>
4. paragraph with link
5. heading level 1/2/3
6. image block
7. table block
8. block ids are preserved
9. new block gets id
10. \n normalization
11. dangerous HTML is removed
12. plain_text is extracted correctly
```

---

## 19. Definition of Done

Интеграция считается готовой, если:

```text
- можно открыть один RichTextDocumentChunk в Tiptap;
- можно редактировать paragraph/html;
- italic/bold/underline сохраняются;
- block.id сохраняются после roundtrip;
- можно вставить и отредактировать таблицу;
- image block отображается, но binary не попадает в chunk;
- dirty chunk сохраняется отдельно;
- весь документ целиком не загружается в editor;
- plain_text обновляется после сохранения;
- опасный HTML удаляется;
- компонент корректно уничтожает editor через destroy().
```

---

## 20. Итоговая архитектурная установка

Tiptap / ProseMirror — это не хранилище документа.

В нашей системе:

```text
BusinessEntityDataChunk.chunk_json = канонический chunk системы
Tiptap JSON = временная редакторная модель
HTML = представление inline-содержимого внутри блоков
plain_text = поисковое представление
```

Основная задача интеграции — написать стабильный адаптер:

```text
RichTextDocumentChunk <-> Tiptap JSON
```

И не нарушить главный принцип:

```text
Один editor редактирует один chunk, а не весь бесконечный документ.
```

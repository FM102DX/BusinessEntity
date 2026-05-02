# Политика редактирования Rich Document

## 1. Назначение

Этот документ фиксирует политику редактирования rich-text документов в системе `BusinessEntity`.

Документ описывает не реализацию конкретного редактора, а поведенческие и архитектурные правила edit-mode:

- сколько чанков загружается в редактор
- как работает переход по содержанию
- как работает скролл в режиме редактирования
- как хранится несохраненное состояние
- когда данные читаются из БД
- когда данные пишутся в БД
- как интегрируется Tiptap / ProseMirror

Документ находится на этапе согласования политики. Он не требует немедленного изменения кода.

---

## 2. Базовый принцип

Редактор rich-документа не открывает весь документ целиком.

В режиме редактирования применяется тот же общий принцип, что и в режиме чтения:

```text
Документ читается и отображается оконно, чанками.
DOM содержит только нужный локальный диапазон чанков.
```

Редактор работает с небольшим количеством чанков, которые пользователь хочет видеть сейчас.

Целевое состояние для MVP:

```text
В edit viewport одновременно находится примерно 2-3 чанка.
```

Принципиальная модель:

- все чанки, которые находятся внутри editor viewport, являются редактируемыми
- readonly preview чанков в edit viewport не используется
- если chunk выгружен из editor viewport, он не отображается и не редактируется
- при выгрузке chunk editor instance может быть уничтожен, но dirty draft должен быть сохранен

---

## 3. Режимы документа

Rich document имеет два основных UI-режима:

1. `Read`
2. `Edit`

### Read mode

В режиме чтения:

- используется текущий read viewport
- работает содержание
- работает оконная подгрузка чанков
- есть кнопка `Edit`

### Edit mode

В режиме редактирования:

- используется editor viewport
- есть кнопка `Save`
- желательно иметь индикатор несохраненных изменений
- кнопки `Cancel` / `Discard` в первом MVP нет

При переходе из `Read` в `Edit` редактор открывает первый чанк документа, если не задана другая стартовая позиция.

---

## 4. Editor viewport

Editor viewport должен использовать ту же концептуальную модель, что и read viewport:

- `LoadedChunks`
- `TotalChunkCount`
- top spacer
- bottom spacer
- estimated chunk height
- загрузка окон чанков
- переходы по `SortOrder`

Но editor viewport добавляет edit-state:

- `FocusedChunkSortOrder`
- dirty draft cache
- editor instances для текущих отображаемых чанков
- статус несохраненных изменений

Editor viewport не должен грузить весь документ.

---

## 5. Редактируемые чанки и фокус

Все чанки, находящиеся в editor viewport, редактируемы.

Каждый отображаемый chunk:

- отображается через Tiptap / ProseMirror
- имеет текущую editor-модель
- может стать dirty
- имеет собственный editor instance на время нахождения в viewport

Понятие focus все равно нужно:

- для текущей позиции пользователя
- для переходов по содержанию
- для scroll/focus restore после подгрузки окна
- для понимания, какой chunk пользователь редактирует прямо сейчас

Потеря focus сама по себе не делает chunk нередактируемым, если он остается внутри editor viewport.
Пока chunk показан в editor viewport, его editor instance продолжает жить и он остается редактируемым.

Перед тем как chunk выгружается из editor viewport, его текущее editor-состояние должно быть синхронизировано с dirty cache, если оно изменено.

Это снижает стоимость DOM и JS-state.

---

## 6. Загрузка при открытии edit mode

При открытии документа для редактирования:

1. читается shell документа
2. открывается первое окно чанков
3. все чанки первого окна загружаются как редактируемые Tiptap editors
4. focus получает первый чанк
5. содержание читается независимо, как и в режиме чтения

Редактор не должен ждать загрузки всего содержания, чтобы показать первый чанк.

---

## 7. Переход по содержанию

Клик по пункту содержания в edit mode работает аналогично read viewport.

Алгоритм:

1. outline node дает `ChunkSortOrder` и anchor/block reference
2. editor viewport проверяет, есть ли нужный чанк в текущем окне или dirty cache
3. если чанка нет в loaded window, загружается окно вокруг target chunk
4. все chunks нового окна отображаются как редактируемые
5. target chunk получает focus
6. если для любого chunk окна есть dirty draft, editor поднимает его из cache
7. если dirty draft нет, editor использует версию из БД / loaded chunk

Важно:

```text
Переход по содержанию не должен терять несохраненные изменения другого чанка.
```

Перед заменой окна нужно сохранить состояние всех выгружаемых editor chunks во внутренний cache, если они dirty.

---

## 8. Скролл в edit mode

Скролл вверх и вниз в edit mode должен работать по тем же принципам, что и read viewport.

При приближении к границе текущего окна:

- вниз подгружается следующее окно чанков
- вверх подгружается предыдущее окно чанков

Все chunks, которые после подгрузки находятся в editor viewport, становятся редактируемыми.

Chunks, которые выходят из editor viewport:

- синхронизируют dirty state во внутренний cache
- уничтожают editor instance
- исчезают из отображения

Но edit mode дополнительно обязан учитывать dirty cache.

Если подгружаемый чанк есть в dirty cache, UI должен показать cached draft, а не версию из БД.

Если dirty draft для чанка нет, чанк можно читать из БД.

---

## 9. Политика кеша редактирования

В edit mode существует внутренний cache несохраненных изменений.

Cache хранит только измененные чанки.

Это принципиальное правило:

```text
Неизмененные чанки не должны оставаться в edit cache.
```

Причина:

- документ может быть большим
- хранить все просмотренные чанки дорого и не нужно
- неизмененный чанк можно прочитать из БД повторно
- измененный чанк нельзя потерять до Save/Cancel

---

## 10. Что хранится в dirty cache

Dirty cache должен хранить не живой JS editor instance, а данные draft-состояния.

Рекомендуемая структура:

```text
RichTextChunkEditDraft
    ChunkId
    SortOrder
    OriginalEditorJson
    CurrentEditorJson
    IsDirty
    LastTouchedUtc
```

```text
CurrentEditorJson = Tiptap / ProseMirror JSON
```

В первом MVP conflict tracking не выполняется, поэтому `OriginalVersion` и `OriginalChecksum` не входят в обязательную структуру draft.
Их можно добавить позже для optimistic locking.

---

## 11. Почему не кешировать живой editor instance

Живой Tiptap / ProseMirror editor instance может быть тяжелым.

Его нужно держать только для тех chunks, которые прямо сейчас находятся в editor viewport.

Правильная модель:

- editor instance живет, пока chunk находится в editor viewport
- при выгрузке chunk из viewport забрать текущее состояние editor
- если состояние изменилось, сохранить draft в cache
- destroy/dispose JS editor instance при выгрузке chunk из viewport допустим и ожидаем
- при возврате к чанку создать editor заново из dirty draft или из storage state

Так мы сохраняем пользовательские изменения, но не держим тяжелые editor instances.

---

## 12. Правило выбора источника чанка

Когда editor viewport должен показать чанк, он выбирает источник в таком порядке:

```text
1. Dirty cache
2. LoadedChunks
3. Database
```

Расшифровка:

- если чанк был изменен, показываем dirty draft
- если не изменен, но уже есть в текущем loaded window, показываем loaded chunk
- если чанка нет локально, читаем из БД

Если dirty draft существует, версия из БД не должна затирать его до явного Save или явного discard-сценария будущих версий.

---

## 13. Выход чанка из фокуса и выгрузка из viewport

Когда редактируемый chunk выходит из фокуса, но остается внутри editor viewport:

1. chunk остается редактируемым
2. его Tiptap editor instance не уничтожается
3. можно синхронизировать draft-state в память, но это не является сохранением в БД

Когда chunk выгружается из editor viewport:

1. editor viewport забирает текущее состояние соответствующего Tiptap editor
2. сравнивает его с исходным состоянием
3. если изменений нет, draft не сохраняется
4. если изменения есть, draft сохраняется в dirty cache
5. UI помечает документ как having unsaved changes
6. если chunk выгружается из viewport, JS editor instance уничтожается

Выход из фокуса не пишет данные в БД.

Это важное правило:

```text
БД обновляется только по кнопке Save.
```

---

## 14. Возврат к ранее измененному чанку

Когда пользователь возвращается к измененному чанку через скролл или содержание:

1. editor viewport находит dirty draft по `SortOrder` или `ChunkId`
2. создает Tiptap editor для этого chunk из draft-состояния
3. пользователь видит ровно то, что редактировал ранее

Версия из БД при этом не перечитывается поверх draft.

Версия из БД не должна автоматически заменять dirty draft.

---

## 15. Save

Кнопка `Save` сохраняет все dirty chunks текущего документа.

Save flow:

1. забрать текущее состояние всех editor instances в текущем viewport
2. обновить dirty cache для измененных chunks
3. собрать все dirty drafts
4. отправить drafts в application service / helper
5. сервер конвертирует editor model в `RichTextBlock[]`
6. сервер обновляет только измененные chunk rows, без проверки конфликтов в MVP
7. сервер обновляет:
   - `Data`
   - `PlainText`
   - `HtmlCache`
   - `BlockCount`
   - `CharCount`
   - `DataSizeBytes`
   - `Version`
   - `Checksum`
8. сервер пересоздает ToC-property для измененных chunks
9. UI обновляет loaded chunks и очищает dirty cache для успешно сохраненных chunks
10. содержание перечитывается асинхронно

Save не должен полностью пересоздавать rich-text storage документа.

---

## 16. Conflict policy

В первом MVP conflict detection не реализуется.

Save просто сохраняет dirty chunks текущего пользователя.

Это означает:

- `OriginalVersion` и `OriginalChecksum` не нужны для MVP-save
- сервер не отклоняет save из-за изменения версии чанка
- последний save выигрывает
- conflict UI не нужен

Future extension:

- добавить `OriginalVersion` и/или `OriginalChecksum` в draft
- проверять актуальность чанка на сервере
- отклонять сохранение конкретного чанка при конфликте
- показывать пользователю конфликтную ситуацию

---

## 17. Cancel / Exit edit

В первом MVP `Cancel` / `Discard` не реализуется.

Минимальное поведение:

- пользователь может нажать `Save`
- выход из edit mode без сохранения можно временно не предоставлять
- если позже появится выход из edit mode, он должен учитывать dirty cache

Future behavior:

- `Save`
- `Discard`
- `Stay in edit mode`

---

## 18. Tiptap / ProseMirror integration

Tiptap / ProseMirror используется как editor engine для чанков текущего editor viewport.

Tiptap не является storage format системы.

Системные роли:

```text
BusinessEntityDataChunkDto.Data = canonical chunk storage
RichTextBlock[] = canonical chunk domain model
Tiptap JSON = temporary editor model
HtmlCache = readonly render cache
PlainText = search/read helper cache
```

Нужен стабильный adapter:

```text
RichTextDocumentChunk <-> Tiptap JSON
```

Этот adapter должен сохранять:

- block identity
- block kind
- heading level
- inline formatting
- links
- image references
- table structure, когда таблицы будут поддержаны

---

## 19. Block identity

Для нормального редактирования каждому блоку нужен стабильный `blockId`.

Текущая модель с `blockIndex` недостаточна для долгосрочного редактирования, потому что при вставке/удалении блоков индексы меняются.

Policy:

- добавить стабильный `Id` в `RichTextBlock`
- существующие блоки без `Id` получают его при нормализации
- новые блоки получают новые ids
- heading anchors должны опираться на `blockId`, а не только на `blockIndex`
- `blockIndex` можно оставить как fallback для старых данных

---

## 20. DOM size policy

В editor viewport одновременно должно быть мало реальных чанков.

Целевой MVP:

```text
2-3 чанка в отображении.
```

Например:

- previous editable chunk
- focused editable chunk
- next editable chunk

Это не жесткое техническое ограничение, а целевая UX/performance policy.

Настройки edit viewport должны быть полноценными sys parameters, а не временным hardcode:

- `RichTextEditChunksBeforeFocused`, default `1`
- `RichTextEditChunksAfterFocused`, default `1`
- `RichTextEditChunksOnOpen`, default `2`

На первом чанке фактическое количество чанков до focus равно `0`, даже если настройка `RichTextEditChunksBeforeFocused = 1`.
Окно открытия строится от первого chunk вперед.

---

## 21. Что не делать

Не делать:

- не загружать весь документ в editor
- не создавать Tiptap editor для каждого чанка большого документа
- не отправлять каждое нажатие клавиши в Blazor Server
- не писать в БД при blur/focus lost
- не терять dirty draft при скролле
- не затирать dirty draft версией из БД
- не использовать `ReplaceRichTextChunksAsync` для сохранения edit changes
- не перестраивать все содержание документа при сохранении одного чанка
- не считать `HtmlCache` canonical editable format
- не показывать readonly preview chunks внутри edit viewport
- не использовать временный hardcode для размеров edit window

---

## 22. MVP scope

Первый согласованный MVP edit mode:

```text
1. Read mode имеет кнопку Edit.
2. Edit mode имеет кнопку Save.
3. При входе в edit mode открывается первый chunk.
4. Клик по содержанию открывает target chunk в editor viewport.
5. Скролл вверх/вниз подгружает соседние chunks.
6. В отображении находятся примерно 2-3 chunks, и все они редактируемые.
7. Каждый отображаемый chunk редактируется через Tiptap.
8. Dirty chunk сохраняется во внутренний cache при выходе из фокуса или выгрузке из viewport.
9. Возврат к dirty chunk показывает cached draft.
10. В БД изменения пишутся только по Save.
11. Save обновляет только dirty chunks.
12. ToC для измененных chunks пересоздается после Save.
13. Cancel/Discard и conflict detection не входят в первый MVP.
14. Edit-window размеры задаются через sys parameters.
```

---

## 23. Зафиксированные решения

Перед реализацией зафиксированы такие решения:

1. Cancel/Discard в первом MVP не включается.
2. Все, что находится в editor viewport, является редактируемым.
3. Preview chunks в edit mode не используются.
4. Если chunk выгружен из editor viewport, его в редакторе нет.
5. Первый MVP поддерживает paragraphs, headings и inline formatting.
6. Таблицы, картинки и сложные embedded-элементы добавляются отдельным этапом.
7. Conflict tracking в первом MVP не выполняется.
8. Save сохраняет dirty chunks поверх текущего состояния БД.
9. Временные hardcoded-настройки размера edit window не используются.
10. Размер editor viewport задается через sys parameters с первой реализации.

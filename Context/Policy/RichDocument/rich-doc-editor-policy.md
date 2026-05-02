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

Это не означает, что все эти чанки обязаны иметь живой Tiptap editor instance.

Предпочтительная модель:

- активный чанк редактируется через Tiptap
- соседние чанки отображаются как readonly preview
- при переходе активность переносится на другой чанк

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
- желательно иметь кнопку `Cancel` или `Exit edit`, но это не обязательный MVP-пункт

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

- `ActiveChunkSortOrder`
- dirty draft cache
- текущий editor instance
- статус несохраненных изменений

Editor viewport не должен грузить весь документ.

---

## 5. Активный чанк

В каждый момент времени есть один активный редактируемый чанк.

Активный чанк:

- отображается через Tiptap / ProseMirror
- имеет текущую editor-модель
- может стать dirty
- перед сменой активного чанка должен выгрузить текущее состояние редактора во внутренний draft cache

Соседние чанки:

- могут быть показаны как preview
- используются для контекста
- не обязаны иметь активный editor instance

Это снижает стоимость DOM и JS-state.

---

## 6. Загрузка при открытии edit mode

При открытии документа для редактирования:

1. читается shell документа
2. открывается первое окно чанков
3. активным становится первый чанк
4. первый чанк загружается в Tiptap editor
5. содержание читается независимо, как и в режиме чтения

Редактор не должен ждать загрузки всего содержания, чтобы показать первый чанк.

---

## 7. Переход по содержанию

Клик по пункту содержания в edit mode работает аналогично read viewport.

Алгоритм:

1. outline node дает `ChunkSortOrder` и anchor/block reference
2. editor viewport проверяет, есть ли нужный чанк в текущем окне или dirty cache
3. если чанка нет в loaded window, загружается окно вокруг target chunk
4. target chunk становится активным
5. если для target chunk есть dirty draft, редактор поднимает его из cache
6. если dirty draft нет, редактор использует версию из БД

Важно:

```text
Переход по содержанию не должен терять несохраненные изменения другого чанка.
```

Перед сменой активного чанка нужно сохранить состояние текущего редактора во внутренний cache, если оно dirty.

---

## 8. Скролл в edit mode

Скролл вверх и вниз в edit mode должен работать по тем же принципам, что и read viewport.

При приближении к границе текущего окна:

- вниз подгружается следующее окно чанков
- вверх подгружается предыдущее окно чанков

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
    OriginalVersion
    OriginalChecksum
    OriginalEditorJson
    CurrentEditorJson
    IsDirty
    LastTouchedUtc
```

Если текущий MVP редактирует HTML, а не Tiptap JSON, допустима временная структура:

```text
RichTextChunkEditDraft
    ChunkId
    SortOrder
    OriginalVersion
    OriginalChecksum
    OriginalHtml
    CurrentHtml
    IsDirty
    LastTouchedUtc
```

Но целевая модель для Tiptap:

```text
CurrentEditorJson = Tiptap / ProseMirror JSON
```

---

## 11. Почему не кешировать живой editor instance

Живой Tiptap / ProseMirror editor instance может быть тяжелым.

Его не нужно хранить для каждого просмотренного чанка.

Правильная модель:

- при уходе чанка из фокуса забрать текущее состояние editor
- если состояние изменилось, сохранить draft в cache
- destroy/dispose JS editor instance допустим
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

Если dirty draft существует, версия из БД не должна затирать его до явного Save/Cancel/Resolve conflict.

---

## 13. Выход чанка из фокуса

Когда активный чанк выходит из фокуса:

1. editor viewport забирает текущее состояние из Tiptap
2. сравнивает его с исходным состоянием
3. если изменений нет, draft не сохраняется
4. если изменения есть, draft сохраняется в dirty cache
5. UI помечает документ как having unsaved changes

Выход из фокуса не пишет данные в БД.

Это важное правило:

```text
БД обновляется только по кнопке Save.
```

---

## 14. Возврат к ранее измененному чанку

Когда пользователь возвращается к измененному чанку через скролл или содержание:

1. editor viewport находит dirty draft по `SortOrder` или `ChunkId`
2. создает Tiptap editor из draft-состояния
3. пользователь видит ровно то, что редактировал ранее

Версия из БД при этом не перечитывается поверх draft.

Допустимо фоново проверить checksum/version для conflict detection, но нельзя автоматически заменить dirty draft.

---

## 15. Save

Кнопка `Save` сохраняет все dirty chunks текущего документа.

Save flow:

1. забрать текущее состояние активного editor
2. обновить dirty cache активного чанка
3. собрать все dirty drafts
4. отправить drafts в application service / helper
5. сервер конвертирует editor model в `RichTextBlock[]`
6. сервер обновляет только измененные chunk rows
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

Save должен использовать optimistic locking.

Минимальные данные для проверки:

- `OriginalVersion`
- `OriginalChecksum`

Если чанк изменился в БД после открытия edit draft, save должен вернуть conflict.

Policy для MVP:

- conflict не решается автоматически
- пользователь получает сообщение
- dirty draft остается в cache
- БД-версия не затирает dirty draft

---

## 17. Cancel / Exit edit

Если пользователь выходит из edit mode с dirty cache, UI должен явно решить, что делать.

Предпочтительное поведение:

- если dirty cache пустой, выход разрешен без вопросов
- если dirty cache не пустой, показать подтверждение

Варианты:

- `Save`
- `Discard`
- `Stay in edit mode`

Для MVP допустимо сначала сделать только предупреждение и запретить выход без Save/Discard.

---

## 18. Tiptap / ProseMirror integration

Tiptap / ProseMirror используется как editor engine для активного чанка.

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

- previous context chunk
- active editable chunk
- next context chunk

Это не жесткое техническое ограничение, а целевая UX/performance policy.

При необходимости настройки могут быть вынесены в sys parameters:

- edit chunks before active
- edit chunks after active
- max dirty drafts warning threshold

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

---

## 22. MVP scope

Первый согласованный MVP edit mode:

```text
1. Read mode имеет кнопку Edit.
2. Edit mode имеет кнопку Save.
3. При входе в edit mode открывается первый chunk.
4. Клик по содержанию открывает target chunk в editor viewport.
5. Скролл вверх/вниз подгружает соседние chunks.
6. В отображении находятся примерно 2-3 chunks.
7. Активный chunk редактируется через Tiptap.
8. Dirty chunk сохраняется во внутренний cache при выходе из фокуса.
9. Возврат к dirty chunk показывает cached draft.
10. В БД изменения пишутся только по Save.
11. Save обновляет только dirty chunks.
12. ToC для измененных chunks пересоздается после Save.
```

---

## 23. Open questions

Открытые вопросы перед реализацией:

1. Нужна ли кнопка `Cancel` в первом MVP или достаточно `Save` + предупреждение при выходе?
2. Должен ли active chunk переключаться кликом по preview chunk?
3. Нужно ли показывать dirty marker рядом с пунктами содержания?
4. Нужно ли ограничение на количество dirty drafts перед предупреждением?
5. Нужно ли в первом MVP поддерживать таблицы и картинки, или начать с paragraph/heading/inline formatting?

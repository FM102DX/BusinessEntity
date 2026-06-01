# Политика комментариев и ActivityMiniApp

Статус: рабочая MVP-политика, требует уточнений после первого внедрения.

## 1. Решение

Комментарии и будущие лайки относятся не к графу `BusinessEntity`, а к отдельному activity-слою.

Для этого вводится отдельный mini-app:

```text
ActivityMiniApp
```

На первом этапе `ActivityMiniApp` реализует только комментарии. Лайки, реакции, вложения, модерация, редактирование и удаление комментариев в MVP не входят.

## 2. Граница ответственности

`BusinessEntity` отвечает на вопрос:

```text
Что существует в системе?
```

`ActivityMiniApp` отвечает на вопрос:

```text
Что пользователи делают вокруг BusinessEntity?
```

Комментарии не являются `BusinessEntity`, не попадают в дерево и не связываются через `BusinessEntityRelation`.

## 3. Storage

Физическое хранение комментариев принадлежит `DataProviderMiniApp`.

Для комментариев создается таблица:

```text
BusinessEntityComments
```

Минимальная схема:

```text
Id                 uuid
CreatedDate        timestamptz
LastModifiedDate   timestamptz
BusinessEntityId   uuid
ParentId           uuid null
Data               text
```

Смысл полей:

- `Id` - идентификатор комментария;
- `BusinessEntityId` - `BusinessEntity.Id`, к которому относится комментарий;
- `ParentId` - комментарий, на который дан ответ;
- `Data` - JSON payload комментария;
- `CreatedDate` и `LastModifiedDate` - стандартные технические даты storage-слоя.

`CreatedDate` и `LastModifiedDate` входят в схему потому, что storage DTO в `DataProviderMiniApp` наследуются от базовой DTO-модели.

## 4. Data JSON

MVP payload комментария:

```json
{
  "schemaVersion": 1,
  "kind": "BusinessEntityComment",
  "text": "Текст комментария",
  "format": "plainText",
  "authorUserId": "00000000-0000-0000-0000-000000000000",
  "authorDisplayName": "user"
}
```

Правила:

- текст хранится как plain text;
- переносы строк сохраняются;
- HTML от пользователя не исполняется;
- `authorUserId` и `authorDisplayName` на MVP хранятся в `Data`, чтобы не расширять таблицу сверх минимальной схемы;
- позже автора можно вынести в отдельные колонки, если понадобится индексировать или фильтровать комментарии по пользователю.

## 5. Порядок чтения

Основной запрос чтения:

```sql
select *
from "BusinessEntityComments"
where "BusinessEntityId" = @businessEntityId
order by "CreatedDate";
```

Для MVP комментарии читаются всей пачкой без пагинации и чанковости.

## 6. Индексы

Минимальные индексы:

```sql
create index ix_business_entity_comments_entity_created
on "BusinessEntityComments" ("BusinessEntityId", "CreatedDate");

create index ix_business_entity_comments_parent
on "BusinessEntityComments" ("ParentId");
```

## 7. Вложенность

Максимальная отображаемая глубина ответа - 3 уровня под корневым комментарием.

Пример:

```text
A
  B
    C
      D
```

Если пользователь отвечает на `D`, новый комментарий сохраняется как ответ на `C`, то есть остается на третьем уровне:

```text
A
  B
    C
      D
      E
```

Правило записи:

```text
if parent depth >= 3
    normalizedParent = ближайший предок с depth = 2
else
    normalizedParent = requestedParent
```

Так дерево не уходит глубже третьего уровня, а ответы на слишком глубокую ветку идут "стеной" по времени.

## 8. UI-компоненты

`ActivityMiniApp` содержит три Razor-компонента:

```text
BusinessEntityCommentsSection
BusinessEntityCommentItem
BusinessEntityCommentEditor
```

`BusinessEntityCommentsSection`:

- загружает и отображает все комментарии для одного `BusinessEntityId`;
- всегда содержит root editor для нового корневого комментария;
- если комментариев нет, показывает только root editor;
- управляет reload после создания комментария.

`BusinessEntityCommentItem`:

- отображает существующий комментарий;
- показывает автора, дату, текст и ссылку `Ответить`;
- по нажатию `Ответить` раскрывает `BusinessEntityCommentEditor` под комментарием.

`BusinessEntityCommentEditor`:

- содержит поле ввода и кнопку `Отправить`;
- создает корневой комментарий или ответ;
- не поддерживает attachments;
- не поддерживает таблицы;
- не поддерживает лайки.

## 9. Размещение на страницах

Комментарии должны быть встроены в:

- страницу обычного `Document`;
- страницу `RichTextDocument`.

Для `Document` секция комментариев располагается под содержимым документа.

Для `RichTextDocument` секция комментариев располагается в нижней части страницы как отдельная область со своим scroll-контейнером. Прокрутка rich-document viewport и прокрутка комментариев не должны смешиваться.

В MVP комментарии rich-document все равно читаются всей пачкой, без пагинации.

## 10. Права

Activity наследует доступ от целевой `BusinessEntity`.

MVP-правило:

- видеть комментарии может пользователь, который уже получил доступ к странице документа;
- писать комментарии может только аутентифицированный пользователь;
- отдельного права `activity-write` пока нет;
- анонимный пользователь может читать доступный документ, но не должен создавать комментарии.

Позже можно добавить отдельное право:

```text
WriteActivity
```

## 11. Backup

Комментарии являются пользовательскими данными и должны попадать в backup пространства.

Backup пространства должен включать строки `BusinessEntityComments`, относящиеся к `BusinessEntity` этого пространства.

В текущем MVP backup может быть доработан отдельным этапом, если backup-сервис еще не знает про таблицу комментариев.

## 12. Запрещено

Запрещено:

- делать каждый комментарий отдельным `BusinessEntity`;
- связывать комментарии через `BusinessEntityRelation`;
- добавлять лайки в MVP комментариев;
- добавлять attachments в MVP комментариев;
- загружать комментарии rich-document чанками;
- хранить пользовательский HTML как исполняемый HTML комментария;
- смешивать комментарии с version history документа.

## 13. Открытые вопросы

Перед финализацией политики нужно решить:

- нужны ли редактирование и soft-delete комментариев;
- нужно ли отдельное право `WriteActivity`;
- нужно ли выносить автора из `Data` в отдельные колонки;
- должны ли комментарии попадать в существующий backup уже в первом релизе;
- нужен ли rich-text editor для комментариев или plain text остается постоянным решением.

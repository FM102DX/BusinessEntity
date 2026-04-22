# Политика хранения данных

## 1. Назначение документа

Этот документ фиксирует базовые постулаты хранения данных в системе `BusinessEntity`.

Он описывает:

- какие runtime-сущности считаются базовыми
- как они соотносятся с DTO-слоем хранения
- когда используется `BusinessEntity`
- когда используется `BusinessEntityData`
- как работает `BusinessEntity<T>`
- где должна жить фабрика создания сущностей
- где должен жить конвертер между runtime-моделью и DTO

Документ считается нормативным для дальнейшей разработки. Новые изменения модели хранения должны ему соответствовать.

---

## 2. Базовые runtime-сущности

Базовый контур бизнес-логики представлен тремя сущностями:

1. `BusinessEntity`
2. `BusinessEntityData`
3. `BusinessEntityRelation`

### `BusinessEntity`

`BusinessEntity` представляет сам объект в системе.

Это:

- узел дерева и графа
- минимальная идентичность объекта
- объект, участвующий в связях
- сущность, которая отображается в дереве
- сущность, которая хранится как `BusinessEntityDto`

`BusinessEntity`:

- не является тяжеловесным доменным объектом
- не должен нести в себе сложную payload-модель
- может существовать без `BusinessEntityData`
- используется для папок, групп, пространств и других простых объектов

### `BusinessEntityData`

`BusinessEntityData` представляет payload-часть объекта.

Это:

- подчиненный объект по отношению к `BusinessEntity`
- расширенное содержимое объекта
- тяжеловесный бизнес-объект
- объект, который хранится как `BusinessEntityDataDto`

Все специализированные тяжелые бизнес-объекты должны наследоваться от `BusinessEntityData`.

Пример:

- `Document : BusinessEntityData`

### `BusinessEntityRelation`

`BusinessEntityRelation` представляет связь между двумя `BusinessEntity`.

Это:

- отдельная runtime-сущность
- отдельная запись хранения
- модель ребра графа

Она хранится как отдельный DTO-тип:

- `BusinessEntityRelationDto`

---

## 3. Правила хранения

В storage-слое используются ровно три DTO:

1. `BusinessEntityDto`
2. `BusinessEntityDataDto`
3. `BusinessEntityRelationDto`

Соответствие такое:

- `BusinessEntity` <-> `BusinessEntityDto`
- `BusinessEntityData` <-> `BusinessEntityDataDto`
- `BusinessEntityRelation` <-> `BusinessEntityRelationDto`

Никакой новый базовый тип хранения не должен вводиться без отдельной архитектурной причины.

---

## 4. Постулаты хранения

### 4.1. Идентичность объекта хранится отдельно от payload

Идентичность объекта живет в `BusinessEntity`.

Payload живет в `BusinessEntityData`.

Связи живут в `BusinessEntityRelation`.

Эти три ответственности не должны смешиваться в одной runtime-модели и не должны храниться в одной DTO-записи.

### 4.2. `BusinessEntityData` подчинен `BusinessEntity`

`BusinessEntityData` не является самостоятельным корневым объектом.

Он всегда относится к конкретному `BusinessEntity`.

Обязательное правило:

- `BusinessEntityData.Id == BusinessEntity.Id`

Это означает:

- data-объект разделяет identity родительской сущности
- data-объект не создает свою собственную отдельную identity
- data-объект представляет расширенное состояние уже существующего `BusinessEntity`

### 4.3. Не каждый `BusinessEntity` обязан иметь data-объект

Простые объекты могут существовать как чистый `BusinessEntity`.

Примеры:

- папка
- группа
- пространство
- служебный контейнер

Для таких объектов отсутствие `BusinessEntityData` считается нормальным.

### 4.4. Data-backed объекты создаются в два шага

Если создается тяжелый объект, например документ, создание происходит логически так:

1. создается `BusinessEntity` нужного типа
2. создается соответствующий наследник `BusinessEntityData`
3. data-объект получает тот же `Id`, что и `BusinessEntity`
4. при необходимости создаются связи через `BusinessEntityRelation`

---

## 5. Обобщенная runtime-модель `BusinessEntity<T>`

Для удобства работы вводится обобщенная runtime-модель:

- `BusinessEntity<T> where T : IBusinessEntityData`

Назначение:

- хранить обычную `BusinessEntity`
- одновременно держать рядом typed payload
- явно показывать, что данная сущность data-backed

Пример:

- `BusinessEntity<Document>`

означает:

- есть базовая сущность `BusinessEntity`
- есть payload-объект `Document`
- объект `Document` лежит в поле `Data`

### Правила для `BusinessEntity<T>`

`BusinessEntity<T>`:

- наследуется от `BusinessEntity`
- содержит свойство `T Data`
- используется только для сущностей, у которых есть typed payload
- не заменяет обычный `BusinessEntity`, а дополняет его

При присвоении `Data` должны синхронизироваться как минимум:

- `Data.Id`
- `Data.EntityId`

Допустимо также синхронизировать:

- `CreatedDate`
- `LastModifiedDate`
- `Name`
- `BusinessEntityType`
- `EntityType`

Но identity-sync по `Id` обязателен всегда.

---

## 6. Фабрика создания сущностей

Для создания runtime-объектов используется `BusinessEntityFactory`.

Фабрика является стандартной точкой создания:

- простых `BusinessEntity`
- typed-сущностей `BusinessEntity<T>`

### Требования к фабрике

Фабрика должна:

- принимать тип создаваемого объекта
- уметь создать обычный `BusinessEntity`
- уметь создать `BusinessEntity<T>`
- при создании data-backed сущности обеспечивать совпадение `Id` у сущности и data-объекта
- не допускать расхождения identity между `BusinessEntity` и `BusinessEntityData`

### Обязательное правило

Если фабрика создает `BusinessEntity<T>`, то после создания должно быть истинно:

- `entity.Data.Id == entity.Id`
- `entity.Data.EntityId == entity.Id`

---

## 7. Где должна жить логика конвертации

Конвертация между runtime-сущностями и DTO не должна жить в UI, в helper-классах или в доменных сущностях.

Ее место:

- внутри `DataProviderMiniApp`

Это правильный слой, потому что именно он отвечает за:

- storage DTO
- загрузку из репозиториев
- запись в репозитории
- hydration runtime-модели
- serialization/deserialization payload

### Предпочтительный вариант

Предпочтительно выделить отдельный блок конвертации внутри `DataProviderMiniApp`, например:

- `MiniApps/DataProviderMiniApp/Internal/Mappers/`

и разнести ответственность на три класса:

1. `BusinessEntityStorageMapper`
2. `BusinessEntityDataStorageMapper`
3. `BusinessEntityRelationStorageMapper`

Плюс при необходимости:

4. `BusinessEntityHydrator`

### Что должен делать `BusinessEntityHydrator`

`BusinessEntityHydrator` нужен, если система начинает собирать не только разрозненные runtime-объекты, но и агрегаты вида:

- `BusinessEntity<T>`

Его ответственность:

- взять `BusinessEntityDto`
- взять `BusinessEntityDataDto`
- десериализовать payload
- создать runtime `BusinessEntity<T>`
- синхронизировать identity
- вернуть готовый typed агрегат

### Допустимый упрощенный вариант

Если пока не хочется заводить отдельный hydrator, допустимо временно держать это в существующем `DataProviderMapper`.

Но в этом случае mapper должен быть разделен хотя бы логически на зоны:

- mapping entity
- mapping relation
- mapping data
- hydration typed aggregate

### Нежелательный вариант

Нежелательно:

- держать конвертацию в `BusinessEntityHelper`
- держать конвертацию в Blazor-компонентах
- держать конвертацию в доменных типах `Document`, `Folder`, `Space`
- смешивать storage mapping и UI projection в одном классе

---

## 8. Что считать правильным созданием документа

Документ в системе считается data-backed объектом.

Правильная модель:

1. создается `BusinessEntity` типа `Document`
2. создается `Document : BusinessEntityData`
3. оба объекта получают один и тот же `Id`
4. при runtime-агрегации они могут быть представлены как `BusinessEntity<Document>`
5. storage-слой сохраняет их раздельно:
   `BusinessEntityDto` и `BusinessEntityDataDto`

---

## 9. Ограничения и запреты

Запрещено:

- использовать `BusinessEntityData` как самостоятельную корневую identity
- создавать новый `BusinessEntityData` с новым `Id`, не совпадающим с `BusinessEntity`
- хранить связь между объектами внутри payload вместо `BusinessEntityRelation`
- превращать `BusinessEntity` в тяжеловесный доменный объект
- плодить отдельные DTO под каждый доменный тип без крайней причины

Допустимо:

- иметь чистый `BusinessEntity` без `BusinessEntityData`
- иметь `BusinessEntity<T>` для data-backed сущностей
- развивать typed payload-модель поверх `BusinessEntityData`

---

## 10. Целевое направление развития

Система должна двигаться в сторону следующей модели:

- `BusinessEntity` отвечает за узел дерева и identity
- `BusinessEntityData` отвечает за typed payload
- `BusinessEntityRelation` отвечает за граф
- `BusinessEntity<T>` отвечает за удобную runtime-агрегацию сущности и typed payload
- `BusinessEntityFactory` отвечает за корректное создание объектов
- `DataProviderMiniApp` отвечает за сохранение и восстановление runtime-модели из DTO

Это и есть базовый канонический контур хранения данных для проекта.

# NewTPT (JWTAuthTemplate)

Серверная часть (ASP.NET Core Web API, .NET 7) для работы с файлами спектров и таблиц:

- регистрация и вход пользователей (JWT + cookie), роли;
- хранение файлов в MinIO с версионированием документов в PostgreSQL;
- загрузка больших файлов по частям (`uploads/{uploadId}/chunks` / `complete`);
- чтение содержимого Excel (`.xls`, `.xlsx`) и SPC в JSON, фильтрация по диапазонам;
- анализ данных (PCA), транспонирование матриц, предобработка;
- сохранение статусов пользовательских сессий.

Все маршруты имеют префикс `/api/v1`, описание каждого метода доступно в Swagger. Полный список — в разделе «API».

Исходный шаблон: <https://github.com/msuddaby/ASPNetCoreJWTAuthTemplate>.

## Архитектура

Проект разделён на слои по принципу чистой архитектуры; зависимости направлены только внутрь.

| Слой | Папка | Содержимое |
|------|-------|------------|
| Контракты | `Shared/Dtos` | DTO запросов и ответов API |
| Прикладной слой | `Application` | модели, интерфейсы (порты), сервисы бизнес-логики, исключения, валидация. Не зависит от EF Core, MinIO и ASP.NET Core MVC |
| Инфраструктура | `Infrastructure` | EF Core/PostgreSQL (`Database`), MinIO (`Storage`), Identity и JWT (`Identity`), чтение Excel/SPC (`FileParsing`), PCA (`Analysis`) |
| Представление | `WebAPI` | контроллеры, фильтр ответов, `Startup` |

Регистрация зависимостей: `Application/DependencyInjection.cs` (`AddApplication`) и `Infrastructure/DependencyInjection.cs` (`AddInfrastructure`). Соблюдение границ слоёв проверяет тест `ApplicationLayerDoesNotDependOnOuterLayers`.

Маршруты прежних версий (`/User/...`, `/minio/...`, `/roles/...` и т.д.) удалены; таблица соответствия старых и новых адресов — в разделе «API».

## Требования

- .NET 7 SDK;
- Visual Studio 2022 (17.6 или новее) с рабочей нагрузкой ASP.NET и поддержкой Docker;
- Docker Desktop 4.36 или новее;
- PostgreSQL 16 и MinIO (поднимаются через `docker-compose.yml`).

Официальные образы MinIO в Docker Hub и бинарники на dl.min.io больше недоступны, поэтому `docker/minio.Dockerfile` собирает MinIO `RELEASE.2024-01-18T22-51-28Z` из исходников (первая сборка занимает несколько минут). При желании образ можно заменить на `quay.io/minio/minio`.

## Развёртывание

1. Запустите Docker Desktop.
2. В корне решения выполните:

   ```bash
   docker compose up -d --build
   ```

   Только базы данных (для запуска API из Visual Studio): `docker compose up -d db minio`.
3. Миграции применяются автоматически при старте API (параметр `Database:ApplyMigrations`, по умолчанию `true`). Вручную из корня решения: `dotnet ef database update --project JWTAuthTemplate/JWTAuthTemplate.csproj --startup-project JWTAuthTemplate/JWTAuthTemplate.csproj` (без `--startup-project` инструмент выберет `docker-compose.dcproj` и завершится ошибкой) или в Visual Studio: «Консоль диспетчера пакетов» → проект по умолчанию `JWTAuthTemplate` → `Update-Database`.
4. Адреса:
   - Swagger: <http://localhost:5505/swagger> (в режиме Development);
   - консоль MinIO: <http://localhost:9001> (`minioadmin` / `minioadmin`).

Основные переменные окружения: `CONNECTION_STRING`, `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`, `MinioSettings__Endpoint`, `MinioSettings__PublicEndpoint`, `MinioSettings__AccessKey`, `MinioSettings__SecretKey`. Для рабочего окружения обязательно смените `JWT_SECRET` и пароли.

### Тесты

```bash
dotnet test JWTAuthTemplate.Tests/JWTAuthTemplate.Tests.csproj
```

Модульные тесты не требуют внешних сервисов. Интеграционные тесты (реальные PostgreSQL, MinIO и запущенный API) выполняются автоматически, если доступны `http://localhost:5505`, PostgreSQL `localhost:5432` (`postgres` / `root`, право CREATEDB) и MinIO `localhost:9000` (`minioadmin` / `minioadmin`) — то есть после `docker compose up -d --build`. Иначе они помечаются как пропущенные. Адреса можно переопределить переменными `NEWTPT_TEST_URL`, `NEWTPT_TEST_DB`, `NEWTPT_TEST_MINIO`, `NEWTPT_TEST_MINIO_USER`, `NEWTPT_TEST_MINIO_PASSWORD`; `NEWTPT_SKIP_INTEGRATION=1` отключает их принудительно.

## Обновление ранее развёрнутой версии

Перед любым обновлением сделайте резервную копию PostgreSQL (pgAdmin: Backup, формат Custom, схема и данные) и тома MinIO. Не выполняйте `docker compose down -v`, `Update-Database 0` и не удаляйте таблицы или историю миграций.

Порядок обновления: остановить API, заменить файлы (см. ниже), выполнить `docker compose up -d --build` или запустить API из Visual Studio — недостающие миграции применяются автоматически при старте. Если база создана master-версией, перед запуском выполните шаги раздела «База данных master-версии».

### Удаление устаревших файлов

Если новая версия копируется поверх старой рабочей копии, удалите файлы, которых больше нет в проекте (при коммите из Visual Studio они отобразятся как удалённые):

```text
CHANGED_FILES.md, DELETED_FILES.txt, README_CHANGES.md, TEST_REPORT.md, MIGRATION_REPAIR.md
.gigacode/, .vscode/settings.json
docker/runtime.Dockerfile, docker/minio-runtime.Dockerfile
scripts/restart_smoke.py, scripts/pca_reference_smoke.py
JWTAuthTemplate/Application/Interfaces/{IFileContentService,ILegacyUserRepository,ILegacyUserService,IMinioService,IReferencesRepository}.cs
JWTAuthTemplate/Application/Models/FileReferenceModel.cs
JWTAuthTemplate/Application/Services/{BaseService,FileContentService,LegacyUserService,ReferenceManager,RoleService,TokenService,UserService}.cs
JWTAuthTemplate/Infrastructure/Data/, JWTAuthTemplate/Infrastructure/Security/
JWTAuthTemplate/Infrastructure/Database/{LegacyUserRepository,ReferencesRepository}.cs
JWTAuthTemplate/Infrastructure/Storage/{MinioService,MinioClientWrapper}.cs
JWTAuthTemplate/Shared/Dtos/{CreateNewVersionRequest,MinioSettingsDTO,TestMatrixDTO}.cs
JWTAuthTemplate/WebAPI/Controllers/{AuthenticationController,MatrixController,MinioController,MinioController.Master,TestMatrixController,TestPreprocessController,UserController}.cs
JWTAuthTemplate.Tests/LegacyServiceTests.cs
```

Затем удалите папки `bin` и `obj` (или выполните «Очистить решение») и пересоберите.

### База данных master-версии

Признак: в `__EFMigrationsHistory` есть `20250719070930_Init`, `20250725153627_Act1`, `20260925183255_AddDocumentVersioning`, но нет `20261023213643_Init`. В этом случае API при старте остановится с сообщением о legacy-базе — это защита данных.

1. Остановите все экземпляры API и сделайте резервную копию (лучше проверить переход на восстановленной копии).
2. В pgAdmin (Query Tool нужной БД) выполните `scripts/preflight_legacy_database.sql` — только чтение; сохраните вывод.
3. Проверьте часовой пояс старых дат в строке `set_config('newtpt.legacy_timestamp_zone', 'Europe/Samara', true)` в `scripts/repair_legacy_database.sql` (сейчас указан подтверждённый `Europe/Samara`; если даты хранились в UTC — укажите `UTC`).
4. Выполните `scripts/repair_legacy_database.sql` целиком. Скрипт работает в одной транзакции: переводит даты в `timestamptz`, заполняет `FileName`/`FileExtension` статусов, восстанавливает `UserReferencesInMinio`, проверяет схему по эталону и только затем отмечает `20261023213643_Init`. При ошибке выполните `ROLLBACK;` и разберите причину.
5. Запустите API — будет применена `20261030151648_ChunkUploads`. Ожидаемая история:

   ```text
   20250719070930_Init
   20250725153627_Act1
   20260925183255_AddDocumentVersioning
   20261023213643_Init
   20261030151648_ChunkUploads
   ```

6. Сравните количество пользователей, документов, версий и статусов с выводом шага 2, проверьте скачивание старого документа.

Откат ниже `20261023213643_Init` через `dotnet ef` недопустим: используйте резервную копию.

### Изменения API

Маршруты приведены к единому стилю (`/api/v1`, существительные во множественном числе, kebab-case, HTTP-методы вместо глаголов в адресах), дублирующие маршруты объединены, тестовые удалены. Таблица соответствия — в разделе «API». Схема базы данных не изменилась, перенос данных описан выше.

## API

Успешные ответы: `{ "success": true, "data": ... }` (204 — без тела, файлы — как поток с заголовком `X-Success: true`). Ошибки — в формате ProblemDetails (RFC 7807): `{ "status": 404, "title": "Not Found", "detail": "..." }`; 400 — неверные параметры, 404 — объект не найден, 409 — конфликт, 500 — внутренняя ошибка (детали только в логе).

| Метод и маршрут | Описание |
|---|---|
| `POST /api/v1/auth/register` | Регистрация пользователя |
| `POST /api/v1/auth/login` | Вход, выдача JWT и refresh-токена |
| `POST /api/v1/auth/logout` | Выход (завершение cookie-сессии) |
| `POST /api/v1/auth/refresh` | Обновление пары токенов |
| `GET /api/v1/roles` | Список ролей |
| `POST /api/v1/roles` | Создание роли (`{ "name": "..." }`), 201 |
| `DELETE /api/v1/roles/{roleName}` | Удаление роли, 204 |
| `GET /api/v1/users/by-username/{username}` | Пользователь по имени |
| `GET /api/v1/users/{userId}/roles` | Роли пользователя |
| `PUT /api/v1/users/{userId}/roles/{roleName}` | Назначение роли, 204 (409, если уже назначена) |
| `DELETE /api/v1/users/{userId}/roles/{roleName}` | Снятие роли, 204 (404, если не назначена) |
| `POST /api/v1/sessions/statuses` | Сохранение статуса сессии, 201 `{ "id" }` (требует JWT) |
| `GET /api/v1/sessions/statuses?fileName=&fileExtension=` | Все статусы по файлу, от новых к старым |
| `GET /api/v1/sessions/statuses/latest?fileName=&fileExtension=&asOf=` | Последний статус; параметры файла и момента времени необязательны (статусы не позже `asOf`) |
| `GET /api/v1/sessions/statuses/first?fileName=&fileExtension=&since=` | Первый статус файла не раньше момента `since` |
| `GET /api/v1/users/{userId}/documents` | Документы пользователя |
| `POST /api/v1/users/{userId}/documents` | Загрузка файлов (multipart, поле `files`); при совпадении имён — 409, если не указан `?allowDuplicateNames=true` |
| `GET /api/v1/documents/{documentId}` | Документ |
| `DELETE /api/v1/documents/{documentId}` | Удаление документа со всеми версиями, 204 |
| `GET /api/v1/documents/{documentId}/versions` | Версии документа |
| `POST /api/v1/documents/{documentId}/versions` | Новая версия (multipart, поле `file`) |
| `GET /api/v1/document-versions/{versionId}` | Версия документа |
| `DELETE /api/v1/document-versions/{versionId}` | Удаление версии, 204 (409 для последней версии) |
| `GET /api/v1/document-versions/{versionId}/content` | Скачивание содержимого версии |
| `PUT /api/v1/users/{userId}/uploads/{uploadId}/chunks/{chunkIndex}` | Загрузка части файла (поля формы `fileName`, `totalChunks`, `chunk`) |
| `POST /api/v1/users/{userId}/uploads/{uploadId}/complete` | Завершение загрузки по частям (`{ "fileName", "totalChunks" }`), повторный вызов идемпотентен |
| `PUT /api/v1/storage/buckets/{bucket}` | Создание бакета, 204 |
| `PUT /api/v1/storage/{bucket}/objects/{objectName}` | Загрузка объекта без создания документа (multipart, поле `file`), 204; 404, если бакета нет |
| `GET /api/v1/storage/{bucket}/objects/{objectName}` | Скачивание объекта |
| `GET /api/v1/storage/{bucket}/presigned-urls/{objectName}` | Временная ссылка (1 час) `{ "url" }` |
| `GET /api/v1/storage/{bucket}/tables?objectNames=a.xlsx&objectNames=b.spc&x1=&x2=&y1=&y2=` | Таблицы из файлов Excel (xls, xlsx) и SPC; формат определяется по расширению, диапазон (все четыре параметра) применяется к Excel |
| `GET /api/v1/analysis/methods` | Доступные методы анализа |
| `POST /api/v1/analysis` | Анализ матрицы из тела запроса (`method`, по умолчанию `PCA`) |
| `POST /api/v1/analysis/excel?method=&varianceThreshold=` | Анализ таблицы из Excel: либо файл `file`, либо поля формы `bucket` + `objectName` |
| `POST /api/v1/matrices/transpose` | Транспонирование матрицы |
| `POST /api/v1/preprocessing` | Предобработка рядов X и Y |

### Использование на клиенте

Базовый адрес: `http://<хост>:5505/api/v1`. Успешный ответ всегда `{ "success": true, "data": ... }`; ниже под «ответом» понимается содержимое `data`. Для маршрутов `/sessions/...` нужен заголовок `Authorization: Bearer <token>`, остальные маршруты вызываются без токена.

#### Регистрация и вход

```http
POST /auth/register
{ "username": "ivan", "password": "P@ssw0rd!", "confirmPassword": "P@ssw0rd!", "email": "ivan@example.com" }
```

Ответ: `{ "success": true }`; при ошибке Identity — 400 с текстом в `detail`.

```http
POST /auth/login
{ "username": "ivan", "password": "P@ssw0rd!" }
```

Ответ: `{ "token": "<JWT>", "refreshToken": "...", "user": { "id": "<userId>", "username", "email", "createDate", "roles": [ { "role": { "name" }, ... } ] } }`. Сохраните `user.id` — он используется как `{userId}` и как имя бакета в хранилище. Access-токен действует 2 дня, refresh-токен — 7 (параметры `JWT:TokenValidityInDays`, `JWT:RefreshTokenValidityInDays`); при 401 обновите пару:

```http
POST /auth/refresh
{ "accessToken": "<просроченный JWT>", "refreshToken": "<refreshToken>" }
```

Ответ: `{ "jwt": { "token", "refreshToken", "expiration" }, "user": { "id", "username", "email", "roles": [...] } }`. `POST /auth/logout` без тела завершает cookie-сессию браузера (для клиентов с Bearer-токеном достаточно удалить токен на своей стороне).

#### Роли

```http
GET  /roles                                 → [ "Admin", "User" ]
POST /roles            { "name": "Analyst" } → 201 { "name": "Analyst" }; 409, если роль есть
DELETE /roles/Analyst                        → 204; 404, если роли нет
GET  /users/by-username/ivan                 → { "id", "username", "email", "createDate", "roles" }
GET  /users/{userId}/roles                   → [ "Analyst" ]
PUT  /users/{userId}/roles/Analyst           → 204; 409, если роль уже назначена
DELETE /users/{userId}/roles/Analyst         → 204; 404, если роль не назначена
```

Если известно только имя пользователя, сначала получите `id` через `/users/by-username/{username}`.

#### Файлы пользователя (документы и версии)

Загрузка одного или нескольких файлов — multipart/form-data, каждый файл в поле `files`:

```bash
curl -X POST "$API/users/$USER_ID/documents" -F files=@spectrum.xlsx -F files=@sample.spc
```

Ответ: `{ "count": 2, "bucketName": "<userId>", "references": [ { "documentId", "fileName", "fileExtension", "minioObjectName", "fileReferenceMinio", "currentVersionNumber", "createdAt", "updatedAt" } ] }`. Если файл с таким именем у пользователя уже есть — 409; чтобы всё равно создать новый документ, добавьте `?allowDuplicateNames=true`.

```http
GET  /users/{userId}/documents              → список документов (поля как в references)
GET  /documents/{documentId}                → один документ
GET  /documents/{documentId}/versions       → [ { "versionId", "documentId", "versionNumber", "minioObjectName", "fileSize", "originalFileName", "createdAt" } ]
POST /documents/{documentId}/versions       multipart, поле file → новая версия (тот же формат)
GET  /document-versions/{versionId}         → одна версия
GET  /document-versions/{versionId}/content → байты файла (application/octet-stream, имя в Content-Disposition)
DELETE /document-versions/{versionId}       → 204; 409 для последней версии документа
DELETE /documents/{documentId}              → 204, удаляет все версии и файлы в хранилище
```

#### Загрузка большого файла по частям

Файл режется на части до 8 МиБ (не более 10 000 частей, файл до 10 ГиБ). Клиент сам выбирает `uploadId` (например, GUID) и отправляет части в любом порядке, можно параллельно:

```http
PUT /users/{userId}/uploads/{uploadId}/chunks/{chunkIndex}
multipart/form-data: fileName=big.bin, totalChunks=N, chunk=<байты части>
```

Ответ `{ "chunkIndex": N }`. Индексы с 0; повтор той же части безопасен, часть с тем же индексом, но другим содержимым — 409. При обрыве связи повторите только неудавшийся индекс. Когда все части отправлены:

```http
POST /users/{userId}/uploads/{uploadId}/complete
{ "fileName": "big.bin", "totalChunks": N }
```

Ответ: `{ "documentId", "minioObjectName", "fileName", "fileReferenceMinio" }` — создан обычный документ с первой версией. Если части загружены не все — 409 `Missing chunks`; повторный вызов для уже собранного файла возвращает ту же квитанцию.

Пример на C#:

```csharp
const int ChunkSize = 8 * 1024 * 1024;
string uploadId = Guid.NewGuid().ToString("N");
await using var file = File.OpenRead(path);
int total = (int)Math.Ceiling(file.Length / (double)ChunkSize);
var buffer = new byte[ChunkSize];
for (int index = 0; index < total; index++)
{
    int read = await file.ReadAsync(buffer.AsMemory(0, ChunkSize));
    using var form = new MultipartFormDataContent
    {
        { new StringContent(fileName), "fileName" },
        { new StringContent(total.ToString()), "totalChunks" },
        { new ByteArrayContent(buffer, 0, read), "chunk", fileName }
    };
    (await http.PutAsync($"users/{userId}/uploads/{uploadId}/chunks/{index}", form)).EnsureSuccessStatusCode();
}
var done = await http.PostAsJsonAsync($"users/{userId}/uploads/{uploadId}/complete", new { fileName, totalChunks = total });
```

#### Прямой доступ к хранилищу

`{objectName}` — полное имя объекта, например `minioObjectName` из документа (`<userId>/<documentId>/v1_spectrum.xlsx`); слэши в адресе допустимы.

```http
PUT /storage/buckets/{bucket}                        → 204 (бакет пользователя создаётся при регистрации; метод для восстановления)
PUT /storage/{bucket}/objects/{objectName}           multipart, поле file → 204, объект без документа; 404, если бакета нет
GET /storage/{bucket}/objects/{objectName}           → байты объекта
GET /storage/{bucket}/presigned-urls/{objectName}    → { "url": "http://.../..." } — ссылка на 1 час для скачивания напрямую из MinIO
```

#### Чтение таблиц из Excel и SPC

```http
GET /storage/{bucket}/tables?objectNames=<userId>/12/v1_a.xlsx&objectNames=<userId>/13/v1_b.spc
GET /storage/{bucket}/tables?objectNames=...&x1=400&x2=800&y1=0&y2=1.5
```

Ответ — массив в порядке запроса: `[ { "objectName", "format": "xlsx" | "xls" | "spc", "table": "<JSON-текст>" } ]`. Поле `table` — строка вида `{"labels": [...], "values": [[{"метка": "значение", ...}, ...]]}`, которую клиент разбирает отдельно (формат совпадает с прежними маршрутами `get-excel-content`, `get-spc-content`). Параметры `x1, x2, y1, y2` задаются только все вместе и применяются к Excel: остаются столбцы с числовым заголовком из диапазона [x1; x2], значения ограничиваются отрезком [y1; y2]; к SPC диапазон не применяется. За один запрос — от 1 до 1000 объектов.

#### Анализ данных (PCA)

```http
GET  /analysis/methods → [ "PCA" ]
POST /analysis
{
  "method": "PCA",
  "data": [[1.0, 2.0, 3.0], [2.0, 4.1, 6.2], [3.1, 5.9, 9.0]],
  "columnNames": ["a", "b", "c"],
  "varianceThreshold": 0.95,
  "newData": [[1.5, 3.0, 4.5]]
}
```

`data` — строки наблюдений; `columnNames`, `newData` (проекция новых строк на компоненты) и `varianceThreshold` необязательны. Ответ: `{ "method", "columnNames", "rowCount", "componentsCount", "componentsForVarianceThreshold", "loadings", "scores", "explainedVarianceRatio", "cumulativeVariance", "transformedData" }`.

Анализ таблицы из Excel (.xlsx, первая строка — заголовки столбцов, далее числа) — либо файл, либо объект хранилища, но не оба сразу:

```bash
curl -X POST "$API/analysis/excel?method=PCA&varianceThreshold=0.9" -F file=@data.xlsx
curl -X POST "$API/analysis/excel" -F bucket=$USER_ID -F objectName=$USER_ID/12/v1_data.xlsx
```

Ответ такой же, как у `POST /analysis`.

#### Матрицы и предобработка

```http
POST /matrices/transpose   { "data": [[1, 2, 3], [4, 5, 6]] } → [[1, 4], [2, 5], [3, 6]]

POST /preprocessing
{
  "data": { "datax": [1, 2, 3], "datay": [10, 20, 30] },
  "params": { "prepX": ["scaling"], "prepY": ["normalization"] }
}
→ { "datax": [...], "datay": [...] }
```

Шаги `prepX`/`prepY` применяются по порядку; поддерживаются `scaling` (деление на максимум по модулю, затем стандартизация: вычитание среднего и деление на стандартное отклонение) и `normalization` (деление на максимум по модулю и приведение к единичной евклидовой норме). Поля `labels`, `class`, `params.algoritm`, `params.crossVal` принимаются для совместимости и на результат не влияют.

#### Статусы сессии (нужен Bearer-токен)

Статус — произвольный JSON-объект; ключи `fileName` и `fileExtension` (если есть) сохраняются отдельно и используются для фильтрации:

```http
POST /sessions/statuses
{ "fileName": "spectrum", "fileExtension": "xlsx", "step": 3, "selectedRange": [400, 800] }
→ 201 { "id": 17 }

GET /sessions/statuses?fileName=spectrum&fileExtension=xlsx            → все статусы файла, от новых к старым ([] если нет)
GET /sessions/statuses/latest                                          → последний статус по всем файлам
GET /sessions/statuses/latest?fileName=spectrum&fileExtension=xlsx     → последний статус файла
GET /sessions/statuses/latest?fileName=...&fileExtension=...&asOf=2026-04-03T17:05:00Z   → последний статус не позже момента
GET /sessions/statuses/first?fileName=...&fileExtension=...&since=2026-04-03T17:05:00Z   → первый статус не раньше момента
```

Элемент статуса: `{ "id", "userId", "actualAt", "fileName", "fileExtension", "statusParamsDict": { ...исходный объект... } }`. Даты — ISO 8601, значение без зоны трактуется как UTC. Если подходящего статуса нет, `/latest` и `/first` возвращают 404.

#### Обработка ошибок

Все ошибки приходят с `Content-Type: application/problem+json`:

```json
{ "status": 409, "title": "Conflict", "detail": "User already in role 'Analyst'.", "traceId": "..." }
```

Клиенту достаточно проверять HTTP-код и показывать `detail`. 401 на `/sessions/...` означает отсутствующий или просроченный токен — выполните `/auth/refresh` и повторите запрос.

### Соответствие старых и новых маршрутов

| Старый маршрут | Новый маршрут |
|---|---|
| `POST /auth/Register`, `Login`, `Logout`, `RefreshToken` | `POST /api/v1/auth/register`, `login`, `logout`, `refresh` |
| `POST /auth/AddRole`, `POST /roles/create-{roleName}` | `POST /api/v1/roles` |
| `GET /auth/GetRoles`, `GET /roles/get-all` | `GET /api/v1/roles` |
| `DELETE /roles/delete-{roleName}` | `DELETE /api/v1/roles/{roleName}` |
| `PUT /roles/assign-user-role/{userId}/{roleName}`, `POST /User/AddRole` | `PUT /api/v1/users/{userId}/roles/{roleName}` |
| `DELETE /roles/remove-user-role/{userId}/{roleName}`, `DELETE /User/RemoveRole` | `DELETE /api/v1/users/{userId}/roles/{roleName}` |
| `GET /roles/user-roles/{userId}` | `GET /api/v1/users/{userId}/roles` |
| `GET /users/get-by-username`, `GET /User/Get` | `GET /api/v1/users/by-username/{username}` |
| `POST /sessions/save-status`, `POST /User/SaveStatus` | `POST /api/v1/sessions/statuses` |
| `GET /sessions/all-by-file-name`, `POST /User/GetAllStatusesByFileName` | `GET /api/v1/sessions/statuses?fileName=&fileExtension=` |
| `GET /sessions/latest-status`, `GET /User/GetLatestStatus` | `GET /api/v1/sessions/statuses/latest` |
| `GET /sessions/latest-by-file-name`, `POST /User/GetLatestStatusByFileName` | `GET /api/v1/sessions/statuses/latest?fileName=&fileExtension=` |
| `GET /sessions/latest-by-file-name-and-time` (последний статус не позже момента) | `GET /api/v1/sessions/statuses/latest?fileName=&fileExtension=&asOf=` |
| `POST /User/GetLatestStatusByFileNameAndTime` (первый статус не раньше момента, формат `dd.MM.yyyy HH:mm:ss`) | `GET /api/v1/sessions/statuses/first?fileName=&fileExtension=&since=` (ISO 8601) |
| `POST /minio/create-bucket`, `POST /minio/CreateBucket` | `PUT /api/v1/storage/buckets/{bucket}` |
| `POST /minio/upload-files`, `UploadFilesUpdateReferences`, `UploadFilesUpdateReferences_2` | `POST /api/v1/users/{userId}/documents` (`?allowDuplicateNames=true` — прежнее поведение `upload-files` и `UploadFilesUpdateReferences`; без флага — поведение `_2` с проверкой имён) |
| `POST /minio/upload-file` (объект без документа) | `PUT /api/v1/storage/{bucket}/objects/{objectName}` |
| `POST /minio/UploadChunk` | `PUT /api/v1/users/{userId}/uploads/{uploadId}/chunks/{chunkIndex}` |
| `POST /minio/CompleteUpload` | `POST /api/v1/users/{userId}/uploads/{uploadId}/complete` |
| `GET /minio/documents/{userId}` | `GET /api/v1/users/{userId}/documents` |
| `GET /minio/documents/detail/{documentId}` | `GET /api/v1/documents/{documentId}` |
| `DELETE /minio/documents/{documentId}` | `DELETE /api/v1/documents/{documentId}` |
| `GET`/`POST /minio/documents/{documentId}/versions` | `GET`/`POST /api/v1/documents/{documentId}/versions` |
| `GET`/`DELETE /minio/documents/versions/{versionId}` | `GET`/`DELETE /api/v1/document-versions/{versionId}` |
| `GET /minio/documents/versions/{versionId}/download` | `GET /api/v1/document-versions/{versionId}/content` |
| `POST /minio/get-file`, `GET /minio/GetFileContent` | `GET /api/v1/storage/{bucket}/objects/{objectName}` |
| `POST /minio/get-file-url`, `GET /minio/GetUrlFileFromMinio` | `GET /api/v1/storage/{bucket}/presigned-urls/{objectName}` |
| `POST /minio/get-excel-content`, `get-excel-content-limits`, `get-spc-content`; `GET /minio/GetAllTableFromExcel`, `GetTableFromExcelWithLimits`, `GetTablesFromSeveralSPC`, `GetAllContentFromFiles` | `GET /api/v1/storage/{bucket}/tables` |
| `POST /minio/AddReferenceInPostgre`, `GET /minio/GetFileReferencesByUserId` | удалены: ссылки на файлы хранятся в документах (`/users/{userId}/documents`) |
| `POST /minio/TestUploadFile` | удалён (тестовая запись в фиксированный бакет; общий аналог — `PUT /api/v1/storage/{bucket}/objects/{objectName}`) |
| `GET /analysis/methods` | `GET /api/v1/analysis/methods` |
| `POST /analysis/Analyze`, `POST /analysis/PCA` | `POST /api/v1/analysis` |
| `POST /analysis/AnalyzeExcel`, `POST /analysis/AnalyzeStoredExcel` | `POST /api/v1/analysis/excel` |
| `POST /TestMatrix/TransposeMatrix`, `POST /matrices/TransposeMatrix` | `POST /api/v1/matrices/transpose` |
| `POST /TestPreprocess/process` | `POST /api/v1/preprocessing` |

Отличия поведения, которые нужно учесть в клиентах:

- все ошибки — ProblemDetails (раньше текст), 500 не раскрывает внутренние детали;
- `DELETE` возвращает 204 без тела; создание роли и статуса сессии — 201;
- список статусов сессии при отсутствии данных — `200 []`, а не 404; поле `statusParams` (сырая строка) удалено, остался объект `status`;
- таблицы (`/storage/{bucket}/tables`) возвращаются массивом `{ "objectName", "format", "table" }` вместо одной строки на файл;
- удаление несуществующего документа или версии — 404 (раньше 400), удаление последней версии — 409 (раньше 500);
- `/sessions/...` заполняет `fileName` и `fileExtension`, `auth/refresh` возвращает данные пользователя;
- таблица `UserReferencesInMinio` сохранена в схеме, но новые записи в неё не добавляются: она дублировала `Documents`/`DocumentVersions` (имя файла, расширение, ETag), список файлов пользователя даёт `GET /api/v1/users/{userId}/documents`;
- операции со статусами сессий выполняются только от имени пользователя из JWT (маршруты `/User/...` принимали `userId` параметром без проверки токена);
- роли назначаются по `userId`; идентификатор по имени пользователя даёт `GET /api/v1/users/by-username/{username}`.

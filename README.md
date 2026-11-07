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

### Предыдущая версия develop

Схема БД и формат данных не менялись (новых миграций нет), поэтому данные, созданные старой версией, читаются новой, и наоборот — при необходимости можно вернуться на предыдущую версию кода без изменений в БД. Достаточно остановить API, обновить файлы и запустить: при необходимости будет применена миграция `20261030151648_ChunkUploads`. При запуске в Docker: `docker compose up -d --build`.

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
| `GET /api/v1/sessions/statuses/latest?fileName=&fileExtension=&asOf=` | Последний статус; параметры файла и момента времени необязательны |
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
| `GET /api/v1/storage/{bucket}/objects/{objectName}` | Скачивание объекта |
| `GET /api/v1/storage/{bucket}/presigned-urls/{objectName}` | Временная ссылка (1 час) `{ "url" }` |
| `GET /api/v1/storage/{bucket}/tables?objectNames=a.xlsx&objectNames=b.spc&x1=&x2=&y1=&y2=` | Таблицы из файлов Excel (xls, xlsx) и SPC; формат определяется по расширению, диапазон (все четыре параметра) применяется к Excel |
| `GET /api/v1/analysis/methods` | Доступные методы анализа |
| `POST /api/v1/analysis` | Анализ матрицы из тела запроса (`method`, по умолчанию `PCA`) |
| `POST /api/v1/analysis/excel?method=&varianceThreshold=` | Анализ таблицы из Excel: либо файл `file`, либо поля формы `bucket` + `objectName` |
| `POST /api/v1/matrices/transpose` | Транспонирование матрицы |
| `POST /api/v1/preprocessing` | Предобработка рядов X и Y |

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
| `GET /sessions/latest-by-file-name-and-time`, `POST /User/GetLatestStatusByFileNameAndTime` | `GET /api/v1/sessions/statuses/latest?fileName=&fileExtension=&asOf=` |
| `POST /minio/create-bucket`, `POST /minio/CreateBucket` | `PUT /api/v1/storage/buckets/{bucket}` |
| `POST /minio/upload-files`, `upload-file`, `UploadFilesUpdateReferences`, `UploadFilesUpdateReferences_2` | `POST /api/v1/users/{userId}/documents` (`?allowDuplicateNames=true` — прежнее поведение `upload-files`) |
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
| `POST /minio/TestUploadFile` | удалён |
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
- таблица `UserReferencesInMinio` сохранена в схеме, но новые записи в неё не добавляются.

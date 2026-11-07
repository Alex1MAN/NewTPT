# NewTPT (JWTAuthTemplate)

Серверная часть (ASP.NET Core Web API, .NET 7) для работы с файлами спектров и таблиц:

- регистрация и вход пользователей (JWT + cookie), роли;
- хранение файлов в MinIO с версионированием документов в PostgreSQL;
- загрузка больших файлов по частям (`UploadChunk` / `CompleteUpload`);
- чтение содержимого Excel (`.xls`, `.xlsx`) и SPC в JSON, фильтрация по диапазонам;
- анализ данных (PCA), транспонирование матриц, предобработка;
- сохранение статусов пользовательских сессий.

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

Маршруты master-версии (`/User/...`, `/minio/UploadFilesUpdateReferences`, `/minio/GetAllTableFromExcel` и т.д.) сохранены для старого клиента и используют те же сервисы, что и новые маршруты.

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

Модульные тесты не требуют внешних сервисов. Интеграционные тесты запускаются, если задана `NEWTPT_TEST_URL` (адрес запущенного API, например `http://localhost:5505`); также используются `NEWTPT_TEST_DB` (строка подключения PostgreSQL с правом CREATEDB) и `NEWTPT_TEST_MINIO`.

## Обновление ранее развёрнутой версии

Перед любым обновлением сделайте резервную копию PostgreSQL (pgAdmin: Backup, формат Custom, схема и данные) и тома MinIO. Не выполняйте `docker compose down -v`, `Update-Database 0` и не удаляйте таблицы или историю миграций.

### Удаление устаревших файлов

Если новая версия копируется поверх старой рабочей копии, удалите файлы, которых больше нет в проекте (при коммите из Visual Studio они отобразятся как удалённые):

```text
CHANGED_FILES.md, DELETED_FILES.txt, README_CHANGES.md, TEST_REPORT.md, MIGRATION_REPAIR.md
.gigacode/, .vscode/settings.json
docker/runtime.Dockerfile, docker/minio-runtime.Dockerfile
scripts/restart_smoke.py, scripts/pca_reference_smoke.py
JWTAuthTemplate/Application/Interfaces/{ILegacyUserRepository,ILegacyUserService,IMinioService,IReferencesRepository}.cs
JWTAuthTemplate/Application/Models/FileReferenceModel.cs
JWTAuthTemplate/Application/Services/{BaseService,LegacyUserService,ReferenceManager,TestMatrixService,RoleService,TokenService,UserService}.cs
JWTAuthTemplate/Infrastructure/Data/, JWTAuthTemplate/Infrastructure/Security/
JWTAuthTemplate/Infrastructure/Database/{LegacyUserRepository,ReferencesRepository}.cs
JWTAuthTemplate/Infrastructure/Storage/{MinioService,MinioClientWrapper}.cs
JWTAuthTemplate/Shared/Dtos/{CreateNewVersionRequest,MinioSettingsDTO}.cs
JWTAuthTemplate/WebAPI/Controllers/{MinioController.Master,TestMatrixController,TestPreprocessController,UsersController}.cs
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

### Изменения поведения API для клиентов

Все 64 маршрута сохранены с прежними параметрами; ответы совпадают с предыдущей версией develop побайтно (включая коды и тексты ошибок маршрутов `/User/...` master-версии), кроме следующих отличий:

- ошибки 500 не раскрывают внутренние детали (текст «Внутренняя ошибка сервера.», подробности — в логе);
- `DELETE /minio/documents/{id}` и `DELETE /minio/documents/versions/{id}` для несуществующего объекта возвращают 404 вместо 400;
- удаление последней версии документа возвращает 409 вместо 500;
- `/sessions/...` заполняет поля `fileName` и `fileExtension` (раньше всегда `null`);
- `auth/RefreshToken` возвращает данные пользователя (раньше поля были пустыми).

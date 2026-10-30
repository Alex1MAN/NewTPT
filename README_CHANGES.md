# Перенос master → develop и интеграция TestPCA

Исходная база: приложенные архивы `NewTPT-master.zip`, `NewTPT-develop.zip`, `TestPCA.zip`. Решение остаётся ASP.NET Core / .NET 7 с PostgreSQL и MinIO; пользовательские данные и миграция Init не заменяются.

Если ваша существующая БД имеет историю `20250719070930_Init`, `20250725153627_Act1`, `20260925183255_AddDocumentVersioning`, сначала выполните переход по `MIGRATION_REPAIR.md`. Обычный запуск Init develop на такой базе недопустим; новый Startup выявляет этот случай до применения миграций.

## Перенос в Visual Studio

1. Создайте отдельную ветку от develop и сохраните незакоммиченные изменения.
2. Скопируйте содержимое `NewTPT-develop` из архива в корень своего решения с заменой совпадающих файлов. Каталоги `.git`, `.vs`, `bin`, `obj` в архив не включены.
3. Удалите старый файл `JWTAuthTemplate/Application/Services/MinioService.cs`: реализация перенесена в `JWTAuthTemplate/Infrastructure/Storage/MinioService.cs`. Это единственное удаление исходного файла; список есть в `DELETED_FILES.txt`.
4. Откройте `JWTAuthTemplate.sln`, выполните Restore NuGet, Build и тесты из `JWTAuthTemplate.Tests`.
5. Проверьте Git Changes: `CHANGED_FILES.md` содержит автоматически рассчитанные отличия от приложенного develop. Не заменяйте свои рабочие настройки подключения тестовыми значениями.
6. Сделайте коммит обычными средствами Visual Studio. Автоматический коммит или push не выполнялся.

## Архитектура

Новая функциональность разделена на прикладные сервисы, порты инфраструктуры и HTTP-адаптеры. Сохранена структура одного `.csproj`: это логическое разделение слоёв, не полная переделка решения на отдельные сборки.

- **Application**: `ChunkUploadService`, `AnalysisService`, `FileContentService`, `LegacyUserService`, `TestMatrixService`, `PreprocessService`, интерфейсы репозиториев и алгоритмов.
- **Infrastructure**: PostgreSQL-транзакции/advisory-lock в `UploadRepository`; MinIO в `MinioObjectStorage` и перенесённом `MinioService`; Excel/MathNet и адаптер `PcaAnalysisAlgorithm`.
- **WebAPI**: привязка параметров, вызов сервисов, HTTP-коды, фильтр успешного ответа.
- **Shared/Dtos**: запросы и результаты анализа, DTO загрузки.
- **Существующий код**: старые Identity-сервисы не переписаны целиком; их прямые зависимости от EF/Identity оставлены вне объёма переноса. Новые прикладные сервисы не ссылаются на конкретные инфраструктурные реализации.

Имена и регистр переносимых методов сохранены. Приватная файловая механика master (`SaveChunkAsync`, `CombineChunks`, `SaveCombinedFile`, `RemoveChunkFolder`, `SanitizePath`) заменена потоковой сборкой из MinIO и состоянием в PostgreSQL, без временных каталогов приложения.

## Перенесённые методы

| Область | Методы |
|---|---|
| Чанки | `UploadChunk`, `CompleteUpload` |
| Загрузка | `UploadFilesUpdateReferences`, `UploadFilesUpdateReferences_2`, `TestUploadFile`, alias `CreateBucket` |
| Ссылки и скачивание | `AddReferenceInPostgre`, `GetFileReferencesByUserId`, `GetUrlFileFromMinio`, `GetFileContent` |
| Содержимое | `GetAllTableFromExcel`, `GetTableFromExcelWithLimits`, `GetTablesFromSeveralSPC`, `GetAllContentFromFiles` |
| Auth | `AddRole`, `GetRoles`; существующие `Register`, `Login`, `Logout`, `RefreshToken` сохранены |
| User | `Get`, `AddRole`, `RemoveRole`, `SaveStatus`, `GetLatestStatus`, `GetAllStatusesByFileName`, `GetLatestStatusByFileName`, `GetLatestStatusByFileNameAndTime` |
| Матрицы | `TransposeMatrix`, `Transpose` |
| Предобработка | `ProcessData`, `Scaling`, `Normalization` |
| Вспомогательные | `UploadFileAsync` с путём файла, `UploadFileAsync2`, `GetUserWithRolesAsync`, `IncludeUserRoles` |
| TestPCA | `LoadDataFromExcel`, `Standardize`, `Fit`, `Transform`, `GetComponentsForVarianceThreshold` |

Develop-маршруты `/minio/documents/...`, `/users/...`, `/roles/...`, `/sessions/...` сохранены. Совместимые маршруты master доступны через `/Auth`, `/User`, `/Minio`, `/TestMatrix`, `/TestPreprocess`; имя и регистр метода сохранены в исходниках.

## Успех: True на клиенте

В JSON булевы значения записываются как `true`, не строка `"True"`. Клиент C# получает значение `bool true`.

- **Команда без результата**: HTTP 200 и тело `true`.
- **Ответ с данными**: HTTP 200 и `{ "success": true, "data": ... }`.
- **Объект с `Success` / `success`**, например `CompleteUploadResponse`: сохраняется без дополнительной обёртки.
- **Скачивание файла**: сохраняется бинарное тело; признак успеха передаётся заголовком `X-Success: true`.
- **HTTP 201**: остаётся 201, требование про OK к нему не применяется.
- **Ошибка**: HTTP 400 / 404 / 409 / 413 / 500, без ложного `success: true`.

Это изменение JSON-контракта прежних ответов с данными: например, результат Login теперь читается из `response.data`. Клиент, разбирающий прежний формат, нужно адаптировать.

## Чанковая загрузка

`POST /Minio/UploadChunk`, `multipart/form-data`:

```text
bucketName  = ID существующего пользователя, бакет создан регистрацией
uploadId    = уникальный идентификатор, 1..128 символов A-Z a-z 0-9 _ -
fileName    = имя файла без пути
chunkIndex  = индекс от 0
totalChunks = общее число частей
chunk       = бинарная часть
```

`POST /Minio/CompleteUpload`, `application/json`:

```json
{
  "bucketName": "ID-ПОЛЬЗОВАТЕЛЯ",
  "uploadId": "upload-001",
  "fileName": "data.xlsx",
  "totalChunks": 3
}
```

Результат содержит `success`, `documentId`, `minioObjectName`, `fileName`, `fileReferenceMinio`. Для чтения используется `minioObjectName`, а ETag не является адресом объекта.

- **Порядок и повторы**: части можно отправлять в любом порядке; идентичный чанк принимается повторно, другой SHA-256 или длина дают 409.
- **Согласованность**: тот же uploadId с другим именем/числом частей даёт 409; отсутствующая сессия даёт 404; пропущенные части не фиксируют документ.
- **Конкуренция**: `pg_advisory_xact_lock` сериализует операции одной загрузки между экземплярами приложения.
- **Завершение**: `Document`, первая `DocumentVersion`, `CurrentVersionId`, legacy-ссылка и квитанция завершаются одной транзакцией БД. Повторный/параллельный CompleteUpload возвращает ту же квитанцию.
- **Перезапуск**: состояние хранится в БД, части в MinIO.
- **Память**: сборка читает части последовательно, не объединяя весь файл в один `byte[]`.
- **Ограничения**: 8 MiB на чанк, 10000 частей, до 10 GiB на файл; нагрузочный прогон на максимальном размере не проводился.
- **Уборка**: после commit части удаляются; при сбое повтор CompleteUpload повторяет уборку. Автоматический TTL брошенных загрузок пока не реализован.
- **Атомарность**: распределённой транзакции PostgreSQL/MinIO нет; сбой до commit может оставить объект-сироту. Детерминированное имя позволяет повторить завершение.

Сейчас чанковая загрузка создаёт новый документ. Загрузка новой версии существующего документа потребует отдельного documentId в контракте.

## PCA и расширение

Основной маршрут: `POST /analysis/Analyze`. Маршрут `POST /analysis/PCA` всегда выбирает PCA.

```json
{
  "method": "PCA",
  "data": [[1, 2], [2, 4], [3, 6]],
  "columnNames": ["X1", "X2"],
  "varianceThreshold": 0.95,
  "newData": [[4, 8]]
}
```

Ответ в `data` содержит scores, loadings, explainedVarianceRatio, cumulativeVariance, число компонентов и transformedData при наличии newData. Матрицы сериализуются по строкам; новые данные используют средние и масштабы обучения.

- **Excel**: `POST /analysis/AnalyzeExcel`, multipart-поле `file`, query `method=PCA`, `varianceThreshold=0.95`. `.xlsx`, первый лист, первая используемая строка как заголовки; неполные/нечисловые строки пропускаются как в TestPCA.
- **MinIO**: `POST /analysis/AnalyzeStoredExcel?bucketName=...&objectName=...&method=PCA`. Принимает объект чанковой загрузки по возвращённому minioObjectName.
- **Методы**: `GET /analysis/methods`. Пока только PCA; PLS возвращает 400.
- **Расширение**: добавить `IAnalysisAlgorithm` с `Method = "PLS"` и регистрацию DI; `AnalysisRequest.Options` предназначен для дополнительных параметров. PCA отклоняет неизвестные параметры.
- **Расчёт**: population-стандартизация, covariance с `n-1`, симметричное собственное разложение MathNet. Точно постоянные столбцы превращаются в нули; полностью постоянная матрица отклоняется.
- **Лимиты**: 1024 столбца, 100000 строк, 2000000 ячеек одновременно; Excel до 16 MiB, JSON до 32 MiB.
- **Графики**: возвращены численные данные для клиентской визуализации, не PNG/GUI. Сохранение модели и отдельный prediction-endpoint не добавлены.

## PostgreSQL, Docker, тесты

Миграция `20261030151648_ChunkUploads` добавляет таблицы сессий/частей. Init и её данные сохранены; миграции применяются при запуске, если `Database:ApplyMigrations` не установлен в `false`.

```powershell
docker compose up -d --build
dotnet test JWTAuthTemplate.Tests/JWTAuthTemplate.Tests.csproj -c Release --filter "Category!=Integration"
```

Swagger: `http://localhost:5505/swagger`; MinIO console: `http://localhost:9001`. PostgreSQL `postgres/root`, MinIO `minioadmin/minioadmin` в compose являются исключительно тестовыми значениями.

`MinioSettings__PublicEndpoint` / `MINIO_PUBLIC_ENDPOINT` задаёт адрес MinIO, доступный клиенту для presigned URL; внутренний `minio:9000` отличается от внешнего `localhost:9000`.

Исходный образ MinIO не удалось скачать в проверочной среде. Compose собирает ту же исходную версию через `docker/minio.Dockerfile`, проверяет SHA-256 и сохраняет исходники сервера в образе.

Для отдельного тестового стека:

```powershell
$env:NEWTPT_TEST_URL = "http://localhost:5505"
$env:NEWTPT_TEST_DB = "Host=localhost;Database=JWTAuthTemplate;Username=postgres;Password=root"
$env:NEWTPT_TEST_MINIO = "localhost:9000"
dotnet test JWTAuthTemplate.Tests/JWTAuthTemplate.Tests.csproj -c Release --filter "Category=Integration"
```

Тесты создают пользователей, роли, бакеты и документы. Не запускайте их на рабочей БД; без NEWTPT_TEST_URL интеграционные тесты явно помечаются skipped.

Для ограниченной CI-среды есть `docker/runtime.Dockerfile` и `docker/minio-runtime.Dockerfile`, упаковывающие предварительно собранные артефакты. Фактический объём проверок и ограничения описаны в `TEST_REPORT.md`.

## Границы задачи

Это тестовое приложение, не production-конфигурация. Открытые маршруты master сохранены: перед внешней публикацией нужны ограничения административных операций по ролям, проверка владения бакетами/документами и отдельное хранение секретов.

`/sessions/...` требует JWT, так как работает с текущим пользователем; исправлено получение IHttpContextAccessor. Исправлено сохранение срока refresh token, без которого RefreshToken отвергал только что выданный токен.

SPC-парсер в приложенной DLL не изменялся. Агрегирование/маршрутизация SPC проверены отдельно; для проверки реального бинарного SPC нужен эталонный файл.

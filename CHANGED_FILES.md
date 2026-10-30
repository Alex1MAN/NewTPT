# Отличия от исходного NewTPT-develop.zip

Изменено: 23 файлов. Добавлено: 72 файлов. Удалено: 1 файлов.

## Изменённые файлы

- `.gitignore`
- `JWTAuthTemplate.sln`
- `JWTAuthTemplate/Application/Interfaces/IMatrixService.cs`
- `JWTAuthTemplate/Application/Interfaces/IMinioService.cs`
- `JWTAuthTemplate/Application/Services/SessionService.cs`
- `JWTAuthTemplate/Application/Services/UserService.cs`
- `JWTAuthTemplate/Infrastructure/Database/Context.cs`
- `JWTAuthTemplate/Infrastructure/Database/ReferencesRepository.cs`
- `JWTAuthTemplate/JWTAuthTemplate.csproj`
- `JWTAuthTemplate/Migrations/ContextModelSnapshot.cs`
- `JWTAuthTemplate/Shared/Dtos/CompleteUploadResponse.cs`
- `JWTAuthTemplate/Shared/Dtos/FileReferenceDTO.cs`
- `JWTAuthTemplate/Shared/Dtos/MinioSettingsDTO.cs`
- `JWTAuthTemplate/Shared/Dtos/RoleDTO.cs`
- `JWTAuthTemplate/Shared/Dtos/UploadResultDTO.cs`
- `JWTAuthTemplate/WebAPI/Controllers/AuthenticationController.cs`
- `JWTAuthTemplate/WebAPI/Controllers/BaseController.cs`
- `JWTAuthTemplate/WebAPI/Controllers/MinioController.cs`
- `JWTAuthTemplate/WebAPI/Controllers/RolesController.cs`
- `JWTAuthTemplate/WebAPI/Controllers/SessionsController.cs`
- `JWTAuthTemplate/WebAPI/Startup.cs`
- `docker-compose.override.yml`
- `docker-compose.yml`

## Новые файлы

- `CHANGED_FILES.md`
- `DELETED_FILES.txt`
- `JWTAuthTemplate.Tests/AnalysisTests.cs`
- `JWTAuthTemplate.Tests/ChunkUploadTests.cs`
- `JWTAuthTemplate.Tests/DockerIntegrationTests.cs`
- `JWTAuthTemplate.Tests/Fixtures/NiCu100.numpy.json`
- `JWTAuthTemplate.Tests/Fixtures/NiCu100.xlsx`
- `JWTAuthTemplate.Tests/Fixtures/generate_pca_reference.py`
- `JWTAuthTemplate.Tests/HttpContractTests.cs`
- `JWTAuthTemplate.Tests/JWTAuthTemplate.Tests.csproj`
- `JWTAuthTemplate.Tests/LegacyFixtures/AddDocumentVersioning.cs`
- `JWTAuthTemplate.Tests/LegacyFixtures/LegacyFixtureContext.cs`
- `JWTAuthTemplate.Tests/LegacyFixtures/LegacyInit.cs`
- `JWTAuthTemplate.Tests/LegacyMigrationTests.cs`
- `JWTAuthTemplate.Tests/LegacyServiceTests.cs`
- `JWTAuthTemplate.Tests/PcaDatasetTests.cs`
- `JWTAuthTemplate/Application/Interfaces/IAnalysisService.cs`
- `JWTAuthTemplate/Application/Interfaces/IChunkUploadService.cs`
- `JWTAuthTemplate/Application/Interfaces/IFileContentService.cs`
- `JWTAuthTemplate/Application/Interfaces/ILegacyUserRepository.cs`
- `JWTAuthTemplate/Application/Interfaces/ILegacyUserService.cs`
- `JWTAuthTemplate/Application/Interfaces/IObjectStorage.cs`
- `JWTAuthTemplate/Application/Interfaces/IPreprocessService.cs`
- `JWTAuthTemplate/Application/Interfaces/IReferencesRepository.cs`
- `JWTAuthTemplate/Application/Interfaces/IUploadRepository.cs`
- `JWTAuthTemplate/Application/Models/ChunkUploadSession.cs`
- `JWTAuthTemplate/Application/Services/AnalysisService.cs`
- `JWTAuthTemplate/Application/Services/ChunkUploadService.cs`
- `JWTAuthTemplate/Application/Services/FileContentService.cs`
- `JWTAuthTemplate/Application/Services/LegacyUserService.cs`
- `JWTAuthTemplate/Application/Services/PreprocessService.cs`
- `JWTAuthTemplate/Application/Services/TestMatrixService.cs`
- `JWTAuthTemplate/Infrastructure/Analysis/DataProcessor.cs`
- `JWTAuthTemplate/Infrastructure/Analysis/PcaAnalysisAlgorithm.cs`
- `JWTAuthTemplate/Infrastructure/Analysis/PcaModel.cs`
- `JWTAuthTemplate/Infrastructure/Database/ContextFactory.cs`
- `JWTAuthTemplate/Infrastructure/Database/DatabaseMigrationRunner.cs`
- `JWTAuthTemplate/Infrastructure/Database/LegacyUserRepository.cs`
- `JWTAuthTemplate/Infrastructure/Database/UploadRepository.cs`
- `JWTAuthTemplate/Infrastructure/Security/UserManagerExtensions.cs`
- `JWTAuthTemplate/Infrastructure/Storage/MinioObjectStorage.cs`
- `JWTAuthTemplate/Infrastructure/Storage/MinioService.cs`
- `JWTAuthTemplate/Migrations/20261030151648_ChunkUploads.Designer.cs`
- `JWTAuthTemplate/Migrations/20261030151648_ChunkUploads.cs`
- `JWTAuthTemplate/Shared/Dtos/AnalysisDTO.cs`
- `JWTAuthTemplate/Shared/Dtos/PreprocessDTO.cs`
- `JWTAuthTemplate/WebAPI/Controllers/AnalysisController.cs`
- `JWTAuthTemplate/WebAPI/Controllers/MinioController.Master.cs`
- `JWTAuthTemplate/WebAPI/Controllers/TestMatrixController.cs`
- `JWTAuthTemplate/WebAPI/Controllers/TestPreprocessController.cs`
- `JWTAuthTemplate/WebAPI/Controllers/UserController.cs`
- `JWTAuthTemplate/WebAPI/Filters/SuccessResponseFilter.cs`
- `MIGRATION_REPAIR.md`
- `README_CHANGES.md`
- `TEST_REPORT.md`
- `TestResults/all-tests.log`
- `TestResults/all-tests.trx`
- `TestResults/build.log`
- `TestResults/docker-runtime-build.log`
- `TestResults/legacy-migrations.log`
- `TestResults/legacy-migrations.trx`
- `TestResults/migration-upgrade.log`
- `TestResults/minio-runtime-build.log`
- `TestResults/pca-realdata.json`
- `TestResults/restart-smoke.json`
- `docker/minio-runtime.Dockerfile`
- `docker/minio.Dockerfile`
- `docker/runtime.Dockerfile`
- `scripts/pca_reference_smoke.py`
- `scripts/preflight_legacy_database.sql`
- `scripts/repair_legacy_database.sql`
- `scripts/restart_smoke.py`

## Удаление при наложении архива

- `JWTAuthTemplate/Application/Services/MinioService.cs`

MinioService перемещён в Infrastructure/Storage. Для существующей legacy-БД сначала прочтите MIGRATION_REPAIR.md; история и данные не удаляются.

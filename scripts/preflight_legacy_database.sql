-- Только чтение. Запускается до изменений на нужной базе.
SELECT current_database(), current_user, current_schema(), current_setting('TimeZone') AS session_time_zone;
SELECT "MigrationId","ProductVersion" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";
SELECT table_name,column_name,data_type,is_nullable,is_identity
FROM information_schema.columns
WHERE table_schema='public' AND table_name IN (
    'Documents','DocumentVersions','UserSessionStatuses','UserReferencesInMinio',
    'UserProfilesMinio','ChunkUploadSessions','UploadParts')
ORDER BY table_name,ordinal_position;
SELECT 'users' AS entity,count(*) AS rows FROM public."AspNetUsers"
UNION ALL SELECT 'roles',count(*) FROM public."AspNetRoles"
UNION ALL SELECT 'documents',count(*) FROM public."Documents"
UNION ALL SELECT 'versions',count(*) FROM public."DocumentVersions"
UNION ALL SELECT 'statuses',count(*) FROM public."UserSessionStatuses"
UNION ALL SELECT 'profiles',count(*) FROM public."UserProfilesMinio";
SELECT d."Id",d."CreatedAt",d."UpdatedAt",d."CurrentVersionId" FROM public."Documents" d ORDER BY d."Id" LIMIT 10;
SELECT c.conname,pg_get_constraintdef(c.oid)
FROM pg_constraint c WHERE c.conrelid IN ('public."Documents"'::regclass,'public."DocumentVersions"'::regclass);

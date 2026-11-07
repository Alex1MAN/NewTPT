namespace JWTAuthTemplate.Application.Models;

/// <summary>Источник Excel-файла для анализа: загруженный файл запроса или объект хранилища.</summary>
public abstract record ExcelSource(string Name);

/// <summary>Файл, переданный в теле запроса.</summary>
public sealed record UploadedExcel(FileUpload File) : ExcelSource(File.FileName);

/// <summary>Объект, уже сохранённый в бакете пользователя.</summary>
public sealed record StoredExcel(string Bucket, string ObjectName) : ExcelSource(ObjectName);

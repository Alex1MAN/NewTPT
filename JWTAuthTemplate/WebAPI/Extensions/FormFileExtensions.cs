using JWTAuthTemplate.Application.Models;

namespace JWTAuthTemplate.WebAPI.Extensions;

/// <summary>Адаптер IFormFile -> FileUpload, чтобы прикладной слой не зависел от ASP.NET Core.</summary>
public static class FormFileExtensions
{
    public static FileUpload ToFileUpload(this IFormFile file) => new(file.FileName, file.Length, file.OpenReadStream);

    public static IReadOnlyList<FileUpload> ToFileUploads(this IEnumerable<IFormFile>? files)
        => files?.Select(ToFileUpload).ToList() ?? new List<FileUpload>();
}

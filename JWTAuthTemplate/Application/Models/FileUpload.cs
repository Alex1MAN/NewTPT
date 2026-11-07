namespace JWTAuthTemplate.Application.Models;

/// <summary>
/// Входящий файл без привязки к ASP.NET Core (IFormFile адаптируется в слое WebAPI).
/// </summary>
public sealed class FileUpload
{
    private readonly Func<Stream> _open;

    public FileUpload(string fileName, long length, Func<Stream> open)
        => (FileName, Length, _open) = (fileName, length, open);

    public string FileName { get; }
    public long Length { get; }
    public Stream OpenReadStream() => _open();
}

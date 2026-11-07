using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Чтение табличных файлов (Excel, SPC) из хранилища в JSON-текст для клиента.</summary>
public interface ITableContentService
{
    /// <summary>
    /// Читает по одной таблице из каждого объекта; формат определяется по расширению имени объекта.
    /// Диапазон <paramref name="range"/> применяется только к Excel-таблицам.
    /// </summary>
    Task<IReadOnlyList<TableContentDTO>> ReadTablesAsync(string bucket, IReadOnlyList<string> objectNames, TableRange? range = null);
}

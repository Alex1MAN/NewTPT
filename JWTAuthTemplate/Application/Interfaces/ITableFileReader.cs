using JWTAuthTemplate.Application.Models;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Порт чтения табличных файлов (Excel, SPC).</summary>
public interface ITableFileReader
{
    /// <summary>Первый лист Excel; первая строка — заголовки.</summary>
    TableData ReadExcel(byte[] content);

    /// <summary>Первый спектр SPC: столбцы X и Y.</summary>
    TableData ReadSpc(byte[] content);
}

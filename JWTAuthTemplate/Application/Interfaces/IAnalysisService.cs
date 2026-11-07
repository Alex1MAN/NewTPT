using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Сценарии анализа данных (PCA и будущие методы).</summary>
public interface IAnalysisService
{
    IReadOnlyList<string> GetMethods();

    /// <summary>Анализ матрицы, переданной в запросе; метод выбирается полем Method.</summary>
    object Analyze(AnalysisRequest request);

    /// <summary>Анализ таблицы из Excel-файла независимо от его источника (загрузка или хранилище).</summary>
    Task<object> AnalyzeExcelAsync(ExcelSource source, string method, double varianceThreshold);
}

/// <summary>Алгоритм анализа; новые методы добавляются регистрацией реализации в DI.</summary>
public interface IAnalysisAlgorithm
{
    string Method { get; }
    object Analyze(AnalysisRequest request);
}

/// <summary>Порт чтения числовой матрицы из Excel для анализа.</summary>
public interface IAnalysisDataReader
{
    AnalysisTable LoadDataFromExcel(Stream stream);
}

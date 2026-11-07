using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Infrastructure.Analysis;

/// <summary>Чтение числовой матрицы из Excel (ClosedXML) для анализа.</summary>
public sealed class ExcelAnalysisDataReader : IAnalysisDataReader
{
    public AnalysisTable LoadDataFromExcel(Stream stream)
    {
        try
        {
            var raw = new DataProcessor().LoadDataFromExcel(stream);
            return new AnalysisTable { Data = raw.Data.ToRowArrays(), ColumnNames = raw.ColumnNames };
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not OutOfMemoryException)
        {
            throw new ArgumentException("Cannot read numeric Excel data: " + ex.Message, ex);
        }
    }
}

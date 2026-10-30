using JWTAuthTemplate.Shared.Dtos;
namespace JWTAuthTemplate.Application.Interfaces;
public interface IAnalysisService
{
    object Analyze(AnalysisRequest request);
    object AnalyzeExcel(Stream stream, string method, double varianceThreshold);
    IReadOnlyList<string> GetMethods();
}
public interface IAnalysisAlgorithm
{
    string Method { get; }
    object Analyze(AnalysisRequest request);
}
public interface IAnalysisDataReader
{
    AnalysisTable LoadDataFromExcel(Stream stream);
}

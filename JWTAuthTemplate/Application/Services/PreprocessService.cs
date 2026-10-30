using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
namespace JWTAuthTemplate.Application.Services;
public sealed class PreprocessService : IPreprocessService
{
    public OutputModel ProcessData(InputModel input)
    {
        if (input?.Data == null) throw new ArgumentException("Invalid input data.");
        return new OutputModel
        {
            Datax = Process(input.Data.Datax, input.Params?.PrepX),
            Datay = Process(input.Data.Datay, input.Params?.PrepY)
        };
    }
    private List<double> Process(List<double> data, List<string>? steps)
    {
        if (data == null || data.Count == 0 || data.Any(v => !double.IsFinite(v)))
            throw new ArgumentException("Data arrays must be non-empty and finite.");
        var result = new List<double>(data);
        foreach (var step in steps ?? new())
            result = step switch
            {
                "scaling" => Scaling(result),
                "normalization" => Normalization(result),
                _ => throw new ArgumentException("Unknown preprocessing method: " + step)
            };
        return result;
    }
    private List<double> Scaling(List<double> data)
    {
        double scale = data.Max(v => Math.Abs(v));
        if (scale == 0) return data.Select(_ => 0d).ToList();
        var safe = data.Select(v => v / scale).ToArray();
        var mean = safe.Average();
        var stdDev = Math.Sqrt(safe.Select(d => Math.Pow(d - mean, 2)).Average());
        return stdDev == 0 ? data.Select(_ => 0d).ToList() : safe.Select(d => (d - mean) / stdDev).ToList();
    }
    private List<double> Normalization(List<double> data)
    {
        var scale = data.Max(v => Math.Abs(v));
        if (scale == 0) return data;
        var scaled = data.Select(d => d / scale).ToArray();
        var norm = Math.Sqrt(scaled.Sum(d => d * d));
        return scaled.Select(d => d / norm).ToList();
    }
}

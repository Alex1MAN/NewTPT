namespace JWTAuthTemplate.Shared.Dtos;
/// <summary>Входные данные предобработки.</summary>
public class InputModel
{
    public DataModel Data { get; set; } = new();
    public ParamsModel? Params { get; set; }
    public List<string>? Labels { get; set; }
    public List<string>? Class { get; set; }
}
/// <summary>Ряды X и Y.</summary>
public class DataModel
{
    public List<double> Datax { get; set; } = new();
    public List<double> Datay { get; set; } = new();
}
/// <summary>Параметры предобработки и алгоритма.</summary>
public class ParamsModel
{
    public List<string>? PrepX { get; set; }
    public List<string>? PrepY { get; set; }
    public List<string>? Algoritm { get; set; }
    public List<string>? CrossVal { get; set; }
}
/// <summary>Результат предобработки.</summary>
public class OutputModel
{
    public List<double> Datax { get; set; } = new();
    public List<double> Datay { get; set; } = new();
}

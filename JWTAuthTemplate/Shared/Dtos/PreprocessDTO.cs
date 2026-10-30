namespace JWTAuthTemplate.Shared.Dtos;
public class InputModel
{
    public DataModel Data { get; set; } = new();
    public ParamsModel? Params { get; set; }
    public List<string>? Labels { get; set; }
    public List<string>? Class { get; set; }
}
public class DataModel
{
    public List<double> Datax { get; set; } = new();
    public List<double> Datay { get; set; } = new();
}
public class ParamsModel
{
    public List<string>? PrepX { get; set; }
    public List<string>? PrepY { get; set; }
    public List<string>? Algoritm { get; set; }
    public List<string>? CrossVal { get; set; }
}
public class OutputModel
{
    public List<double> Datax { get; set; } = new();
    public List<double> Datay { get; set; } = new();
}

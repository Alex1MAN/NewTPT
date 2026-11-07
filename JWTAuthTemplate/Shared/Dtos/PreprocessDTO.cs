namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Запрос предобработки спектральных данных.</summary>
public class PreprocessRequest
{
    /// <summary>Ряды X и Y.</summary>
    public PreprocessSeries Data { get; set; } = new();

    /// <summary>Шаги предобработки для каждого ряда.</summary>
    public PreprocessParams? Params { get; set; }

    public List<string>? Labels { get; set; }
    public List<string>? Class { get; set; }
}

/// <summary>Ряды X и Y.</summary>
public class PreprocessSeries
{
    public List<double> Datax { get; set; } = new();
    public List<double> Datay { get; set; } = new();
}

/// <summary>Параметры предобработки: последовательности шагов ("scaling", "normalization") для X и Y.</summary>
public class PreprocessParams
{
    public List<string>? PrepX { get; set; }
    public List<string>? PrepY { get; set; }
    public List<string>? Algoritm { get; set; }
    public List<string>? CrossVal { get; set; }
}

/// <summary>Результат предобработки: преобразованные ряды X и Y.</summary>
public class PreprocessResult
{
    public List<double> Datax { get; set; } = new();
    public List<double> Datay { get; set; } = new();
}

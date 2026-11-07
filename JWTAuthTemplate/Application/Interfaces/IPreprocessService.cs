using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Предобработка данных (масштабирование, нормализация).</summary>
public interface IPreprocessService
{
    OutputModel ProcessData(InputModel input);
}

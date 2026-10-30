using JWTAuthTemplate.Shared.Dtos;
namespace JWTAuthTemplate.Application.Interfaces;
public interface IPreprocessService
{
    OutputModel ProcessData(InputModel input);
}

# Альтернатива для ограниченной CI-среды: сначала dotnet publish -o artifacts/publish.
FROM mcr.microsoft.com/dotnet/aspnet:7.0
COPY artifacts/publish/ /app/
WORKDIR /app
EXPOSE 80
ENTRYPOINT ["dotnet", "JWTAuthTemplate.dll"]

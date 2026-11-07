# Подробнее о настройке контейнера и о том, как Visual Studio использует этот Dockerfile для быстрой отладки: https://aka.ms/customizecontainer

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["JWTAuthTemplate/JWTAuthTemplate.csproj", "JWTAuthTemplate/"]
RUN dotnet restore "JWTAuthTemplate/JWTAuthTemplate.csproj"
COPY . .
WORKDIR "/src/JWTAuthTemplate"
RUN dotnet build "JWTAuthTemplate.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "JWTAuthTemplate.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "JWTAuthTemplate.dll"]
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0.101 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Meadow.Api/Meadow.Api.csproj", "Meadow.Api/"]
COPY ["Meadow.Data/Meadow.Data.csproj", "Meadow.Data/"]
# Ohne diese Zeile scheitert das restore unten - und zwar ERST nach dem
# Layer-Cache, was die Ursache gut versteckt.
COPY ["Meadow.Shared/Meadow.Shared.csproj", "Meadow.Shared/"]
RUN dotnet restore "Meadow.Api/Meadow.Api.csproj"
COPY . .
WORKDIR "/src/Meadow.Api"
RUN dotnet build "Meadow.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Meadow.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
WORKDIR /app
# Logs-Verzeichnis erstellen und Berechtigungen setzen
RUN mkdir -p /app/Logs && chown -R app:app /app/Logs
COPY --from=publish /app/publish .
USER app
ENTRYPOINT ["dotnet", "Meadow.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0.101 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["4Cows-FE/4Cows-FE.csproj", "4Cows-FE/"]
COPY ["BBCowDataLibrary/BBCowDataLibrary.csproj", "BBCowDataLibrary/"]
RUN dotnet restore "4Cows-FE/4Cows-FE.csproj"
COPY . .
WORKDIR "/src/4Cows-FE"
RUN dotnet build "4Cows-FE.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "4Cows-FE.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
WORKDIR /app
# Logs-Verzeichnis erstellen und Berechtigungen setzen
RUN mkdir -p /app/Logs && chown -R app:app /app/Logs
COPY --from=publish /app/publish .
USER app
ENTRYPOINT ["dotnet", "4Cows-FE.dll"]

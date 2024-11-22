FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["4Cows/4Cows.csproj", "4Cows/"]
COPY ["BBCowDataLibrary/BBCowDataLibrary.csproj", "BBCowDataLibrary/"]
RUN dotnet restore "4Cows/4Cows.csproj"
COPY . .
WORKDIR "/src/4Cows"
RUN dotnet build "4Cows.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "4Cows.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "4Cows.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BB_Cow/BB_Cow.csproj", "BB_Cow/"]
COPY ["BBCowDataLibrary/BBCowDataLibrary.csproj", "BBCowDataLibrary/"]
RUN dotnet restore "BB_Cow/BB_Cow.csproj"
COPY . .
WORKDIR "/src/BB_Cow"
RUN dotnet build "BB_Cow.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BB_Cow.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BB_Cow.dll"]

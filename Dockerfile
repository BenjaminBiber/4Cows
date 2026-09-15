FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# 8.0.414 statt 8.0.101. Dazwischen liegen knapp zwei Jahre Korrekturen am
# Trimmer und am WebAssembly-Publish - genau die zwei Dinge, von denen dieses
# Image jetzt abhaengt. Fest verdrahtet und nicht "8.0", damit ein Build von
# heute und einer von naechstem Monat dasselbe Ergebnis liefern.
FROM mcr.microsoft.com/dotnet/sdk:8.0.414 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# python3 MUSS vor der Workload stehen.
#
# wasm-tools zu installieren aendert den Publish: ist die Workload da, bindet er
# die WebAssembly-Laufzeit neu, statt die vorgebaute zu nehmen - und das
# Build-Skript dafuer ist Python, das im SDK-Image fehlt. Ohne diese Zeile
# bricht der Build mit "unable to find python in $PATH" ab, und zwar erst im
# LETZTEN Schritt nach mehreren Minuten.
#
# Das Relinking ist genau das, was fehlte: dotnet.native.wasm faellt von 2,79 auf
# 2,60 MB, und damit liegt der Offline-Cache bei 9,75 statt 10,19 MB - also im
# einstelligen Bereich, wie gefordert. Die Zahl aus einem lokalen Publish OHNE
# Workload ist nicht dieselbe; verlassen sollte man sich auf die aus dem Image.
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && rm -rf /var/lib/apt/lists/*
RUN dotnet workload install wasm-tools --skip-manifest-update

# Die vier csproj vor dem Quellcode, damit dieser Layer zwischen
# Codeaenderungen stehen bleibt - das restore laedt im dreistelligen
# MB-Bereich. Fehlt eine davon, scheitert das restore ERST nach dem
# Layer-Cache, was die Ursache gut versteckt.
COPY ["Meadow.Api/Meadow.Api.csproj", "Meadow.Api/"]
COPY ["Meadow.Client/Meadow.Client.csproj", "Meadow.Client/"]
COPY ["Meadow.Data/Meadow.Data.csproj", "Meadow.Data/"]
COPY ["Meadow.Shared/Meadow.Shared.csproj", "Meadow.Shared/"]
RUN dotnet restore "Meadow.Api/Meadow.Api.csproj"

COPY . .

# EIN publish, kein getrenntes build davor: Meadow.Api zieht den Client mit,
# und ein vorheriges "dotnet build" haette denselben Trimmer- und
# WASM-Durchlauf ein zweites Mal gefahren.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/Meadow.Api"
RUN dotnet publish "Meadow.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
WORKDIR /app

# /tmp/Logs und NICHT /app/Logs. LoggerService.InitializeLogger schreibt nach
# /tmp/Logs; das alte Dockerfile bereitete /app/Logs vor, in dem nie etwas
# ankam, waehrend das tatsaechlich benutzte Verzeichnis keine gesetzten Rechte
# hatte.
RUN mkdir -p /tmp/Logs && chown -R app:app /tmp/Logs

COPY --from=publish /app/publish .
USER app
ENTRYPOINT ["dotnet", "Meadow.Api.dll"]

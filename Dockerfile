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

# Vor dem restore, damit dieser Layer zwischen Codeaenderungen stehen bleibt -
# der Download liegt im dreistelligen MB-Bereich.
#

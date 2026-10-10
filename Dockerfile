# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY WebScraper.sln ./
COPY src/WebScraper.Core/WebScraper.Core.csproj src/WebScraper.Core/
COPY src/WebScraper.Api/WebScraper.Api.csproj src/WebScraper.Api/

RUN dotnet restore src/WebScraper.Api/WebScraper.Api.csproj

COPY src/ ./src/
RUN dotnet publish src/WebScraper.Api/WebScraper.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Used by docker compose health checks (aspnet image has no curl by default).
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 1000 appuser \
    && chown -R appuser:appuser /app
USER appuser

COPY --from=build --chown=appuser:appuser /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WebScraper.Api.dll"]

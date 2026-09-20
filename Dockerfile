# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
USER app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG MOVIEAPP_SOURCE_VERSION=unknown
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/MovieApp.Domain/MovieApp.Domain.csproj src/MovieApp.Domain/
COPY src/MovieApp.Contracts/MovieApp.Contracts.csproj src/MovieApp.Contracts/
COPY src/MovieApp.Application/MovieApp.Application.csproj src/MovieApp.Application/
COPY src/MovieApp.Infrastructure/MovieApp.Infrastructure.csproj src/MovieApp.Infrastructure/
COPY src/MovieApp.Api/MovieApp.Api.csproj src/MovieApp.Api/

RUN dotnet restore src/MovieApp.Api/MovieApp.Api.csproj

COPY src/ src/

WORKDIR /src/src/MovieApp.Api
RUN dotnet publish MovieApp.Api.csproj \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:InformationalVersion=${MOVIEAPP_SOURCE_VERSION}

FROM base AS final
ARG MOVIEAPP_SOURCE_VERSION=unknown
ENV MOVIEAPP_SOURCE_VERSION=${MOVIEAPP_SOURCE_VERSION}
WORKDIR /app
COPY --from=build /app/publish .

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER app

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "MovieApp.Api.dll"]

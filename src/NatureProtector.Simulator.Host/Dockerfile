# syntax=docker/dockerfile:1.7
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:9.0.313
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime:9.0.17-noble-chiseled

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/NatureProtector.Simulator.Host/NatureProtector.Simulator.Host.csproj --configfile NuGet.Config
RUN dotnet publish src/NatureProtector.Simulator.Host/NatureProtector.Simulator.Host.csproj \
    --configuration Release \
    --no-restore \
    --output /out \
    /p:UseAppHost=false

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
WORKDIR /app
ENV DOTNET_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    RabbitMq__ObservabilityRawEnabled=false
COPY --from=build --chown=app:app /out/ ./
USER app
STOPSIGNAL SIGTERM
ENTRYPOINT ["dotnet", "NatureProtector.Simulator.Host.dll"]

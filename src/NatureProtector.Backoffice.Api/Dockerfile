# syntax=docker/dockerfile:1.7
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:9.0.313
ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:9.0.17-noble-chiseled

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/NatureProtector.Backoffice.Api/NatureProtector.Backoffice.Api.csproj --configfile NuGet.Config
RUN dotnet publish src/NatureProtector.Backoffice.Api/NatureProtector.Backoffice.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /out \
    /p:UseAppHost=false

FROM ${DOTNET_ASPNET_IMAGE} AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    RuntimeOrchestration__Mode=Disabled \
    RuntimeOrchestration__EvidenceMode=Disabled
COPY --from=build --chown=app:app /out/ ./
USER app
EXPOSE 8080
STOPSIGNAL SIGTERM
ENTRYPOINT ["dotnet", "NatureProtector.Backoffice.Api.dll"]

<#
.SYNOPSIS
Cria a estrutura base de dados local para a área piloto de Proença-a-Nova.

.DESCRIPTION
O script cria a árvore de diretórios usada pelos datasets externos, baseline,
manifests e artefactos de runtime. Opcionalmente descarrega também algumas
referências públicas que servem de apoio metodológico.

.PARAMETER DownloadPublicReferences
Quando presente, descarrega os documentos públicos configurados no próprio
script.

.NOTES
- Não produz a baseline tratada por si só; prepara apenas o espaço e as
  referências iniciais.
- É seguro correr várias vezes, porque a criação de diretórios é idempotente.
#>

param(
    [switch]$DownloadPublicReferences
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$directories = @(
    "data",
    "data/external",
    "data/external/dgt",
    "data/external/ipma",
    "data/external/era5-land",
    "data/external/era5",
    "data/external/cems-effis",
    "data/external/icnf",
    "data/external/icnf/stats",
    "data/external/icnf/geocatalog",
    "data/external/icnf/municipal_docs",
    "data/external/firms",
    "data/external/corine",
    "data/external/tree-cover-density",
    "data/external/pt-firesprd",
    "data/external/cop-dem",
    "data/baseline",
    "data/baseline/areas",
    "data/baseline/areas/proenca-a-nova",
    "data/manifests",
    "data/manifests/datasets",
    "data/manifests/scenarios",
    "data/runtime",
    "data/runtime/simulations",
    "data/runtime/exports"
)

foreach ($directory in $directories) {
    $path = Join-Path $root $directory
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}

if (-not $DownloadPublicReferences) {
    Write-Output "Estrutura base criada. Usa -DownloadPublicReferences para descarregar os documentos públicos diretos."
    exit 0
}

$downloads = @(
    @{
        Url = "https://fogos.icnf.pt/pmdfci/05_CASTELO_BRANCO/0508/2G/Caderno_I/Texto/PMDFCI_ProencaANova_Caderno_I.pdf"
        Target = "data/external/icnf/municipal_docs/PMDFCI_ProencaANova_Caderno_I.pdf"
    },
    @{
        Url = "https://www.ipma.pt/bin/file.data/climate-normal/cn_91-20_ALVEGA.pdf"
        Target = "data/external/ipma/cn_91-20_ALVEGA.pdf"
    }
)

foreach ($download in $downloads) {
    $targetPath = Join-Path $root $download.Target
    $targetDir = Split-Path -Parent $targetPath
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

    # Estas descargas são apenas referências públicas complementares; o script
    # não depende delas para criar a estrutura base.
    Invoke-WebRequest -Uri $download.Url -OutFile $targetPath -UseBasicParsing
    Write-Output ("Downloaded " + $download.Target)
}

Write-Output "Downloads públicos concluídos."

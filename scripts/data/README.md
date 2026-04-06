# Aquisição e Curadoria de Dados

## Missão Deste README

A missão deste README é explicar, de forma simples mas completa, como foi feita a parte de aquisição, organização e tratamento dos dados para a área piloto de `Proença-a-Nova`.

Este documento conta a história do pipeline de dados como ele existe hoje no repositório:

1. primeiro criámos o espaço de trabalho dos dados;
2. depois trouxemos as fontes brutas (`raw`);
3. a seguir transformámos essas fontes em artefactos curados (`baseline`);
4. por fim ligámos esses artefactos ao que o simulador vai precisar: área, grelha, atributos por célula, histórico de fogo, referência meteorológica e candidatos a cenários.

O objetivo não é apenas dizer "que comando correr". O objetivo é explicar:

- porque cada comando existe;
- o que ele consome;
- o que ele produz;
- e como contribui para o resultado final.

Para o mapa das lacunas ainda em aberto e da ordem dos próximos passos, ver também:

- [docs/planning/pipeline-gap-and-dependency-map.md](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/docs/planning/pipeline-gap-and-dependency-map.md)

## Índice

1. [A história do pipeline de dados](#a-história-do-pipeline-de-dados)
2. [Estrutura atual do repositório de dados](#estrutura-atual-do-repositório-de-dados)
3. [Setup necessário](#setup-necessário)
4. [Bootstrap inicial](#bootstrap-inicial)
5. [Fluxo completo de aquisição e curadoria](#fluxo-completo-de-aquisição-e-curadoria)
6. [Comandos por etapa](#comandos-por-etapa)
7. [Formalização dos cenários A/B/C](#formalização-dos-cenários-abc)
8. [Estado atual do que já foi produzido](#estado-atual-do-que-já-foi-produzido)
9. [Catálogo completo de scripts](#catálogo-completo-de-scripts)
10. [O que ainda falta](#o-que-ainda-falta)

## A História do Pipeline de Dados

Este bloco do projeto começa com uma ideia simples: o simulador e a pipeline não podem trabalhar sobre ficheiros soltos, descarregados manualmente e sem contexto. Precisamos de uma base de dados de entrada que seja:

- repetível;
- rastreável;
- legível por outra pessoa;
- fácil de alterar sem rebentar o resto do projeto.

Por isso, a construção foi feita por camadas.

### 1. Primeiro criámos o palco

Antes de falar de meteorologia, risco ou sensores, era preciso definir:

- qual é a área piloto;
- qual é a unidade analítica;
- onde ficam os dados brutos;
- onde ficam os dados curados;
- onde ficam os manifests e a proveniência.

O palco escolhido foi `Proença-a-Nova`.
O limite oficial veio da `CAOP 2025`.
A unidade analítica passou a ser uma grelha de `1 km x 1 km`.

### 2. Depois demos identidade a cada célula

Com a área e a grelha prontas, criámos o `cells_attributes`.
Pensa nele como a tabela mestra da área piloto.

Cada linha representa uma célula da grelha.
Cada coluna representa contexto que o simulador e o motor de risco vão precisar:

- ocupação do solo;
- perigosidade estrutural;
- declive;
- exposição de vertentes;
- mais tarde altitude, tree cover density e outros atributos.

### 3. Depois trouxemos a memória do território

A seguir precisávamos de contexto histórico:

- que grandes incêndios aconteceram perto da área;
- que área ardida oficial intersectou o concelho;
- que dias fazem sentido como candidatos a cenários.

Para isso juntámos duas famílias de dados:

- `PT-FireSprd`, para grandes eventos e progressão;
- `ICNF`, para área ardida observada e camadas de risco rural.

### 4. Depois demos clima ao sistema

Com território e histórico montados, faltava a parte meteorológica.
Foi aqui que construímos:

- `ipma_nearby_stations`, para saber quais as estações mais relevantes;
- `ipma_recent_observations`, para validar a parte local;
- `weather_reference`, para dar uma série horária coerente ao simulador;
- `weather_daily_reference`, para transformar essa série em contexto comparável dia a dia.

### 5. Finalmente ligámos os candidatos ao contexto real

Os `scenario_candidates` começaram como um seed histórico.
Agora já não são apenas uma lista de incêndios relevantes.
Cada candidato já pode ser lido também como um "dia meteorologicamente comparável":

- quente ou não;
- seco ou não;
- ventoso ou não;
- mais ou menos extremo no contexto local.

Isto ainda não é `FWI` nem `KBDI`, mas já é uma ponte real entre:

- dados históricos;
- baseline meteorológica;
- e futura geração de cenários do simulador.

### 6. Depois transformámos a shortlist em cenários executáveis

Até aqui já existiam candidatos, mas ainda faltava uma decisão concreta:

- qual é o dia "normal plausível" para o cenário `A`;
- qual é o dia "forte e crítico" para o cenário `B`;
- e como é que o cenário `C` reutiliza a mesma base física do `B` sem inventar um clima novo.

Foi por isso que passámos a gerar ficheiros de cenário reais.

Esses ficheiros já guardam:

- a data escolhida;
- a razão da escolha;
- o contexto meteorológico e de índices;
- e um bloco `simulator_options` alinhado com o que o `Simulator.Host` já sabe consumir hoje.

## Estrutura Atual do Repositório de Dados

Hoje a parte de dados está organizada assim:

```text
data/
  external/
    dgt/
    icnf/
    ipma/
    lneg/
    open-meteo/
    pt-firesprd/
    ...
  baseline/
    areas/
      proenca-a-nova/
  manifests/
    datasets/
    scenarios/
  runtime/
    simulations/
    exports/

scripts/
  data/
    *.py
    *.ps1
    requirements-data.txt
```

### O significado de cada pasta

`data/external/`

- aqui ficam os dados brutos;
- são os ficheiros descarregados das fontes oficiais;
- não são a "fonte de verdade" do projeto;
- servem como entrada para a curadoria.

`data/baseline/`

- aqui ficam os ficheiros curados;
- esta é a camada que o simulador e o resto do sistema devem consumir;
- sempre que possível, é daqui que o projeto deve ler.

`data/manifests/`

- aqui ficam os manifests de datasets e cenários;
- explicam o que existe, de onde veio e qual o estado.

`data/runtime/`

- aqui vão ficar saídas temporárias de simulações e exports;
- não deve ser confundido com baseline.

`scripts/data/`

- aqui está a "linha de montagem";
- cada script representa uma transformação concreta do pipeline de dados.

## Setup Necessário

Este setup é importante. Sem ele, os scripts podem falhar mesmo que o código esteja correto.

### Sistema operativo esperado

Os comandos aqui documentados foram preparados para:

- Windows
- PowerShell

### Python recomendado

Usa `Python 3.12` ou `Python 3.13` instalado a partir do `python.org`.

Evita:

- Python da Microsoft Store
- Python do MSYS2

### 1. Confirmar que o Python certo existe

Corre:

```powershell
py -0p
python --version
```

Resultado esperado:

- `py -0p` deve mostrar um Python em `C:\...`
- o ideal é algo como `C:\Users\Miguel\AppData\Local\Programs\Python\Python312\python.exe`

### 2. Criar o ambiente virtual de dados

Exemplo:

```powershell
& 'C:\Users\Miguel\AppData\Local\Programs\Python\Python312\python.exe' -m venv .venv-data312
```

Resultado esperado:

- fica criada a pasta [`.venv-data312`](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/.venv-data312)

### 3. Atualizar `pip`, `setuptools` e `wheel`

```powershell
.\.venv-data312\Scripts\python.exe -m pip install --upgrade pip setuptools wheel
```

Resultado esperado:

- o ambiente fica pronto para instalar as dependências geoespaciais

### 4. Instalar os requisitos de dados

```powershell
.\.venv-data312\Scripts\python.exe -m pip install -r scripts\data\requirements-data.txt
```

Resultado esperado:

- ficam instalados `geopandas`, `rasterio`, `pyogrio`, `pandas`, `pyarrow`, `requests`, etc.

### 5. Validar o ambiente

```powershell
.\.venv-data312\Scripts\python.exe -c "import geopandas, rasterio, pyogrio, pandas, pyarrow; print('ok')"
```

Resultado esperado:

```text
ok
```

Se isto falhar, os scripts de curadoria não devem ser corridos ainda.

## Bootstrap Inicial

O bootstrap não gera logo todos os artefactos finais, mas cria a estrutura base e prepara o repositório para os passos seguintes.

### Comando

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\data\bootstrap-proenca-a-nova.ps1
```

Se quiseres repetir também os downloads públicos:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\data\bootstrap-proenca-a-nova.ps1 -DownloadPublicReferences
```

### O que este passo faz

- cria a estrutura `data/`
- cria manifests base
- prepara a área piloto `proenca-a-nova`
- descarrega referências públicas simples, quando pedido

### Resultado esperado

- existe a pasta [data](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data)
- existem manifests em [data/manifests](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/manifests)
- existem as pastas base de `external`, `baseline` e `runtime`

## Fluxo Completo de Aquisição e Curadoria

Esta é a ordem lógica do pipeline de dados tal como foi construída.

### Etapa 1. Curar a área e a grelha

Fonte principal:

- `CAOP 2025`

Objetivo:

- obter o limite oficial de `Proença-a-Nova`
- gerar a grelha de `1 km`

### Etapa 2. Criar a tabela base por célula

Objetivo:

- criar o `cells_attributes`
- deixar o schema pronto para ser enriquecido

### Etapa 3. Enriquecer o território

Fontes usadas:

- `COS 2018`
- `ICNF perigosidade estrutural`
- `LNEG EU-DEM slope`
- `LNEG EU-DEM aspect`

Objetivo:

- dar contexto físico e territorial a cada célula

### Etapa 4. Construir memória de fogo

Fontes usadas:

- `PT-FireSprd v2.0`
- `ICNF área ardida`

Objetivo:

- construir `fire_history`
- criar seeds de `scenario_candidates`

### Etapa 5. Construir contexto meteorológico

Fontes usadas:

- `IPMA API`
- `Open-Meteo Historical Weather API`

Objetivo:

- identificar estações próximas
- validar observação recente
- gerar `weather_reference`
- gerar `weather_daily_reference`

### Etapa 6. Enriquecer candidatos a cenários

Objetivo:

- passar de um seed histórico para um conjunto de candidatos com contexto meteorológico real

### Etapa 7. Formalizar os cenários executáveis

Objetivo:

- escolher um cenário `A` base plausível;
- escolher um cenário `B` de risco elevado;
- derivar um cenário `C` degradado a partir do `B`;
- e gravar tudo isso em ficheiros versionados, legíveis e repetíveis

## Comandos por Etapa

### 1. Curar o limite do concelho e a grelha

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\curate_proenca_from_caop.py
```

Contributo:

- define a área piloto oficial
- cria a unidade analítica do simulador

Resultado esperado:

- [area.gpkg](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/area.gpkg)
- [area.geojson](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/area.geojson)
- [grid_1km.gpkg](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/grid_1km.gpkg)
- [grid_1km.geojson](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/grid_1km.geojson)

Estado atual:

- grelha gerada com **467 células**

### 2. Criar o seed de `cells_attributes`

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\build_cells_attributes_seed.py
```

Contributo:

- cria a tabela base por célula
- prepara o schema dos atributos futuros

Resultado esperado:

- [cells_attributes.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/cells_attributes.parquet)
- [cells_attributes.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/cells_attributes.csv)

### 3. Aplicar ocupação do solo (`COS 2018`)

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\apply_cos2018_land_cover.py
```

Contributo:

- dá contexto territorial básico a cada célula

Resultado esperado:

- preenche `land_cover_code`
- preenche `land_cover_label`
- preenche `land_cover_macroclass`
- preenche `land_cover_pct_dominant`

### 4. Aplicar perigosidade estrutural (`ICNF`)

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\apply_structural_hazard_2020_2030.py
```

Contributo:

- aproxima a tabela de células ao problema de risco rural

Resultado esperado:

- preenche `structural_hazard_code`
- preenche `structural_hazard`

### 5. Aplicar declive e exposição de vertentes (`LNEG`)

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\apply_lneg_slope_aspect_from_identify.py
```

Contributo:

- adiciona topografia derivada de serviços oficiais
- evita depender logo de um DEM bruto local

Resultado esperado:

- preenche `slope_deg`
- preenche `aspect_deg`

Nota importante:

- aqui o `GetMap` do WMS não serviu porque devolvia um TIFF renderizado e sem georreferência útil
- o caminho correto foi usar `identify` sobre o `MapServer`

### 6. Extrair metadados do `PT-FireSprd`

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\extract_pt_firesprd_metadata.py
```

Contributo:

- cria um índice tabular limpo da base de grandes incêndios

Resultado esperado:

- [pt_firesprd_metadata.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/external/pt-firesprd/pt_firesprd_metadata.parquet)
- [pt_firesprd_metadata.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/external/pt-firesprd/pt_firesprd_metadata.csv)

### 7. Criar o seed de `scenario_candidates`

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\build_scenario_candidates_seed_from_pt_firesprd.py
```

Contributo:

- cria uma primeira shortlist de dias e eventos relevantes

Resultado esperado:

- [scenario_candidates.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/scenario_candidates.parquet)
- [scenario_candidates.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/scenario_candidates.csv)

### 8. Criar o seed de `fire_history`

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\build_fire_history_seed_from_pt_firesprd.py
```

Contributo:

- cria o histórico contextual inicial de fogo

Resultado esperado:

- [fire_history.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/fire_history.parquet)
- [fire_history.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/fire_history.csv)

### 9. Integrar área ardida oficial do `ICNF`

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\apply_icnf_burned_area_to_fire_history.py
```

Contributo:

- deixa o `fire_history` menos dependente de proximidade municipal
- traz interseções reais com o concelho

Resultado esperado:

- o `fire_history` é regravado com eventos `ICNF_ardida_*`

### 10. Descarregar amostras abertas do `IPMA`

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\download_ipma_open_data.py
```

Contributo:

- prepara contexto observacional local

Resultado esperado:

- `stations.json`
- `observations.json`
- `obs-surface.geojson`

### 11. Construir a shortlist de estações próximas

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\build_ipma_nearby_stations.py
```

Contributo:

- ajuda a escolher o melhor ponto de referência meteorológico

Resultado esperado:

- [ipma_nearby_stations.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/ipma_nearby_stations.csv)

Estado atual:

- a estação mais próxima é `Proença-a-Nova, P.Moitas`

### 12. Construir uma amostra recente de observações locais

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\build_ipma_recent_observations_sample.py
```

Contributo:

- permite validar se o nosso contexto meteorológico faz sentido localmente

Resultado esperado:

- [ipma_recent_observations.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/ipma_recent_observations.parquet)
- [ipma_recent_observations.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/ipma_recent_observations.csv)

### 13. Construir o `weather_reference` horário

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\build_weather_reference_from_open_meteo.py
```

Contributo:

- cria a base meteorológica horária do simulador

Resultado esperado:

- [weather_reference.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/weather_reference.parquet)
- [weather_reference.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/weather_reference.csv)
- um JSON bruto em `data/external/open-meteo/proenca-a-nova/`

Estado atual:

- série horária de **2017-01-01** a **2025-12-31**
- **78.888 linhas**

### 14. Construir o `weather_daily_reference`

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\build_weather_daily_reference.py
```

Contributo:

- transforma a série horária em contexto diário comparável

Resultado esperado:

- [weather_daily_reference.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/weather_daily_reference.parquet)
- [weather_daily_reference.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/weather_daily_reference.csv)

Estado atual:

- **3287 dias**

### 15. Enriquecer os `scenario_candidates` com contexto meteorológico

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\enrich_scenario_candidates_from_daily_weather.py
```

Contributo:

- transforma candidatos históricos em candidatos meteorologicamente classificados

Resultado esperado:

- regrava [scenario_candidates.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/scenario_candidates.parquet)
- regrava [scenario_candidates.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/scenario_candidates.csv)

Estado atual:

- o script foi tornado idempotente para evitar colunas duplicadas `_x` e `_y`

### 16. Calcular índices diários de referência

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\build_fire_weather_indexes_reference.py
```

Contributo:

- calcula uma referência diária aproximada de `FWI` e `KBDI`
- usa precipitação diária e condições próximas do meio-dia obtidas da série horária
- prepara a ponte entre meteorologia diária, simulador e motor de risco

Resultado esperado:

- regrava [weather_daily_reference.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/weather_daily_reference.parquet)
- regrava [weather_daily_reference.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/weather_daily_reference.csv)
- passam a existir colunas como `ffmc_reference`, `fwi_reference` e `kbdi_reference`

### 17. Enriquecer os `scenario_candidates` com os índices

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\enrich_scenario_candidates_from_fire_weather_indexes.py
```

Contributo:

- preenche os campos de índices nos candidatos a cenários
- deixa de existir apenas um contexto meteorológico genérico e passa a haver também uma leitura por índices

Resultado esperado:

- regrava [scenario_candidates.parquet](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/scenario_candidates.parquet)
- regrava [scenario_candidates.csv](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/baseline/areas/proenca-a-nova/scenario_candidates.csv)
- passam a existir campos como `fwi_reference`, `kbdi_reference` e `candidate_index_kind`

### 18. Gerar os cenários `A/B/C`

```powershell
.\.venv-data312\Scripts\python.exe scripts\data\build_proenca_scenarios.py
```

Contributo:

- materializa os cenários em ficheiros reais;
- escolhe o cenário `A` a partir da referência diária, procurando um dia seco de verão próximo do centro da distribuição local;
- escolhe o cenário `B` a partir dos candidatos históricos, com prioridade para ligação direta a `Proença-a-Nova` e contexto crítico de índices;
- deriva o cenário `C` a partir do `B`, mudando apenas o perfil de falhas.

Resultado esperado:

- [proenca-a-nova-scenarios.generated.json](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/manifests/scenarios/proenca-a-nova-scenarios.generated.json)
- [scenario_a.base.json](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/manifests/scenarios/proenca-a-nova/scenario_a.base.json)
- [scenario_b.high-risk.json](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json)
- [scenario_c.degraded-pipeline.json](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/data/manifests/scenarios/proenca-a-nova/scenario_c.degraded-pipeline.json)

Estado atual:

- `scenario_a`: `2017-06-01`, cenário base plausível, com `FWI 18.597` e `KBDI 185.259`
- `scenario_b`: `2020-09-13`, ligado a `Proença-a-Nova`, com `FWI 65.377` e `KBDI 650.106`
- `scenario_c`: mesma base física do `scenario_b`, mas com perfil `measurement_and_transport_faults`

### 19. Usar os cenários gerados no `Simulator.Host`

O `Simulator.Host` já consegue ler manifestos de cenário gerados.

Para isso foram introduzidos dois campos na secção `Simulator`:

- `ScenarioManifestPath`
- `ScenarioManifestScenarioKey`

#### Opção A. Apontar para um ficheiro individual

Exemplo:

```json
"Simulator": {
  "ScenarioManifestPath": "data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json",
  "ScenarioManifestScenarioKey": null
}
```

Neste modo, o host lê diretamente o ficheiro do cenário e aplica o bloco `simulator_options`.

#### Opção B. Apontar para o catálogo gerado

Exemplo:

```json
"Simulator": {
  "ScenarioManifestPath": "data/manifests/scenarios/proenca-a-nova-scenarios.generated.json",
  "ScenarioManifestScenarioKey": "scenario_c"
}
```

Neste modo, o host lê o catálogo inteiro e escolhe a entrada pedida em `ScenarioManifestScenarioKey`.

#### O que é sobrescrito

Quando um manifesto é usado, o host passa a carregar a partir do ficheiro:

- `AreaId`
- `ScenarioId`
- `ScenarioName`
- `ScenarioDescription`
- `ScenarioCategory`
- `StartTimestamp`
- `BaseTemperature`
- `BaseHumidity`
- `BaseWindSpeed`
- `FailureRate`
- `NoiseLevel`
- `TimeAcceleration`
- `NumberOfCycles`
- `IntervalSeconds`

Nota importante:

- a lista de sensores ainda continua a vir do `appsettings.json`
- nesta fase, os manifestos de cenário fecham o contexto do cenário, mas ainda não geram automaticamente a rede de sensores

## Formalização dos Cenários A/B/C

Esta parte merece ser lida como uma pequena história operacional.

O cenário `A` não foi escolhido a partir de um incêndio.
Foi escolhido a partir da própria referência diária, procurando um dia que parecesse "normal para verão", seco e plausível, sem cair nem nos extremos baixos nem nos extremos altos.

O cenário `B` foi escolhido ao contrário: procurámos um candidato histórico forte, com bom contexto de índices e, se possível, ligação direta à área piloto.
Foi por isso que a seleção automática acabou em `2020-09-13`, com o evento `ProencaaNova_13092020`.

O cenário `C` não inventa outro clima.
Isso é importante.
Ele reutiliza exatamente o contexto físico do `B` e muda apenas a parte da degradação:

- mais falhas;
- mais ruído;
- e, no desenho futuro, injeções como duplicação, atraso e out-of-order.

Na prática, isto já deixa três artefactos com papéis diferentes:

- `A`: comportamento base
- `B`: comportamento crítico
- `C`: mesmo comportamento crítico, mas com pipeline e sensores degradados

E agora esses artefactos já não são apenas documentação.
Também podem ser usados para parametrizar o `Simulator.Host`.

## Estado Atual do Que Já Foi Produzido

Hoje, a área piloto de `Proença-a-Nova` já tem:

- área oficial curada
- grelha 1 km
- `cells_attributes` com:
  - ocupação do solo
  - perigosidade estrutural
  - declive
  - exposição de vertentes
- `fire_history` com seed histórico e interseções `ICNF`
- `ipma_nearby_stations`
- `ipma_recent_observations`
- `weather_reference`
- `weather_daily_reference`
- `scenario_candidates` enriquecidos com contexto meteorológico
- cenários `A/B/C` formalizados em ficheiros de manifesto

## Catálogo Completo de Scripts

Nem todos os scripts têm o mesmo papel.
Alguns são comandos de execução direta.
Outros são utilitários de apoio usados por outros passos da pipeline.

### Scripts de execução direta

Estes são os scripts que fazem sentido correr explicitamente:

- [bootstrap-proenca-a-nova.ps1](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/bootstrap-proenca-a-nova.ps1)
- [curate_proenca_from_caop.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/curate_proenca_from_caop.py)
- [build_cells_attributes_seed.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/build_cells_attributes_seed.py)
- [apply_cos2018_land_cover.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/apply_cos2018_land_cover.py)
- [apply_structural_hazard_2020_2030.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/apply_structural_hazard_2020_2030.py)
- [apply_lneg_slope_aspect_from_identify.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/apply_lneg_slope_aspect_from_identify.py)
- [extract_pt_firesprd_metadata.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/extract_pt_firesprd_metadata.py)
- [build_scenario_candidates_seed_from_pt_firesprd.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/build_scenario_candidates_seed_from_pt_firesprd.py)
- [build_fire_history_seed_from_pt_firesprd.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/build_fire_history_seed_from_pt_firesprd.py)
- [apply_icnf_burned_area_to_fire_history.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/apply_icnf_burned_area_to_fire_history.py)
- [download_ipma_open_data.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/download_ipma_open_data.py)
- [build_ipma_nearby_stations.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/build_ipma_nearby_stations.py)
- [build_ipma_recent_observations_sample.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/build_ipma_recent_observations_sample.py)
- [build_weather_reference_from_open_meteo.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/build_weather_reference_from_open_meteo.py)
- [build_weather_daily_reference.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/build_weather_daily_reference.py)
- [enrich_scenario_candidates_from_daily_weather.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/enrich_scenario_candidates_from_daily_weather.py)
- [build_fire_weather_indexes_reference.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/build_fire_weather_indexes_reference.py)
- [enrich_scenario_candidates_from_fire_weather_indexes.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/enrich_scenario_candidates_from_fire_weather_indexes.py)
- [build_proenca_scenarios.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/build_proenca_scenarios.py)

### Scripts auxiliares

Estes scripts existem para suportar transformações mais finas ou reutilização de lógica.
Normalmente não são o primeiro ponto de entrada para uma pessoa nova na equipa, mas fazem parte do toolkit da pipeline.

- [enrich_cells_attributes_from_vector.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/enrich_cells_attributes_from_vector.py)
  - utilitário genérico para enriquecer `cells_attributes` a partir de camadas vetoriais
- [enrich_cells_attributes_from_raster.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/enrich_cells_attributes_from_raster.py)
  - utilitário genérico para enriquecer `cells_attributes` a partir de raster e estatística zonal
- [geospatial_utils.py](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/geospatial_utils.py)
  - funções partilhadas para leitura, CRS, clipping e operações geoespaciais recorrentes
- [requirements-data.txt](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/requirements-data.txt)
  - catálogo de dependências Python desta pipeline de dados

### Regra prática

Se o objetivo for repetir o pipeline de forma linear, segue os comandos listados na secção anterior.
Se o objetivo for acrescentar novas fontes ou enriquecer mais o `cells_attributes`, então os scripts auxiliares passam a ser relevantes.

## O Que Ainda Falta

Ainda faltam blocos importantes.

### 1. Altitude

Falta `altitude_m` no `cells_attributes`.

Bloqueio atual:

- o `MDT25m` da `DGT` aparece com restrições e sem serviço aberto no metadado

### 2. `Tree Cover Density`

Falta `tree_cover_density`.

Bloqueio atual:

- download e autenticação da fonte

### 3. `CORINE`

Falta a camada de compatibilidade europeia.

Estado atual:

- não é bloqueador, porque o `COS 2018` já foi aplicado como fonte principal

### 4. `FIRMS` e `CEMS/EFFIS`

Faltam para reforçar:

- `fire_history`
- `scenario_candidates`

Bloqueio atual:

- autenticação ou download manual

### 5. Refinar `FWI` e `KBDI`

Mesmo depois desta etapa, os índices continuam a ser uma referência operacional aproximada, porque:

- a fonte meteorológica ainda é um bootstrap público;
- falta substituir por `ERA5-Land` oficial quando houver acesso;
- falta também confrontar os resultados com as fontes de validação externas.

## Observação Final

Se tiveres de explicar este bloco a outra pessoa da equipa, a frase mais simples é esta:

> primeiro demos forma ao território, depois demos memória de fogo, depois demos clima, e por fim ligámos os dias candidatos a um contexto meteorológico comparável.

Esse é o papel deste pipeline de dados dentro do projeto.


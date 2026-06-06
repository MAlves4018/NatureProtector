# Diário Técnico de Miguel Alves

## Recapitulação Quinzenal

### Período

24 de março a 7 de abril de 2026

### Objetivo desta entrada

Registar, de forma primeiro estruturada e simples e só depois mais detalhada, o trabalho de análise, modelação, investigação, consolidação documental e alinhamento técnico realizado neste intervalo.

## Índice

- [Resumo Estruturado](#resumo-estruturado)
- [1. Modelação técnica e tentativa de formalização](#1-modelação-técnica-e-tentativa-de-formalização)
- [2. Reformulação do problema e consolidação conceptual](#2-reformulação-do-problema-e-consolidação-conceptual)
- [3. Fontes, variáveis, cenários e validação](#3-fontes-variáveis-cenários-e-validação)
- [4. Passagem da investigação para a implementação](#4-passagem-da-investigação-para-a-implementação)
- [5. Consolidação documental do repositório](#5-consolidação-documental-do-repositório)
- [6. Consolidação de testes e medição de coverage](#6-consolidação-de-testes-e-medição-de-coverage)
- [7. Reanálise do roadmap e lições desta fase](#7-reanálise-do-roadmap-e-lições-desta-fase)

## Resumo Estruturado

### O que foi feito

1. Tentámos construir uma forma séria de representar tecnicamente o projeto, primeiro através de modelação manual e depois explorando automatização a partir do código.
2. Reformulámos o problema da simulação, deixando de o tratar como simples geração de números e passando a enquadrá-lo como base técnica de um módulo de prevenção.
3. Fechámos critérios de fontes, áreas, variáveis, cenários, calibração e validação, para que o simulador fique ancorado em dados e decisões metodológicas defensáveis.
4. Traduzimos a investigação para um plano técnico de implementação, com baseline de dados, área piloto, manifests, cenários executáveis e ordem de trabalho.
5. Reorganizámos a documentação do repositório para refletir melhor o estado real do projeto e identificámos onde o roadmap já não estava totalmente sincronizado com o que efetivamente foi feito.
6. Consolidámos a frente de testes, alargando os projetos de validação automática e integrando uma medição agregada de coverage para acompanhar a qualidade técnica do código.

### Resultado principal da quinzena

O projeto ficou mais claro em três planos ao mesmo tempo:

- no plano conceptual, porque passou a existir uma linguagem mais estável para falar de risco, simulação, erro e pipeline;
- no plano de dados, porque a baseline e os cenários deixaram de ser apenas intenção e passaram a existir como artefactos concretos;
- no plano documental, porque o repositório passou a ter uma estrutura mais legível e uma leitura mais honesta do estado atual;
- no plano de validação, porque passou a existir uma base de testes mais larga e uma forma objetiva de medir coverage consolidado.

## 1. Modelação técnica e tentativa de formalização

No início desta fase, procurámos evitar uma documentação meramente ilustrativa. Em vez de ficar por diagramas soltos ou por uma imagem demasiado grande e difícil de manter, tentámos construir uma representação técnica do sistema que mostrasse módulos, classes, interfaces, relações, fluxos principais e estado de implementação.

Foi nesse contexto que explorámos o uso do Modelio como ferramenta de modelação. A intenção não era apenas desenhar, mas sim organizar o sistema em packages, vistas e diagramas especializados, distinguindo contexto, deployment, domínio, mensagens, pipeline runtime, persistência, regras e lacunas. Esta abordagem ajudou-nos a clarificar melhor o que queríamos representar e por que ordem.

Ao mesmo tempo, percebemos limitações práticas importantes. Nem todos os artefactos que tínhamos eram diretamente importáveis como projeto Modelio, e foi necessário distinguir entre projeto real, XMI importável e blueprint estrutural. Isso ajudou a perceber os mecanismos corretos, mas também mostrou que o caminho manual teria um custo de manutenção significativo.

Daí surgiu a tentativa de apoiar a modelação com extração automática a partir do código C#, através de Roslyn. A ideia era criar um modelo intermédio versionável, capaz de alimentar diagramas e documentação sem obrigar a reconstruir tudo manualmente a cada alteração. No entanto, esta abordagem não chegou a dar o retorno esperado nesta fase. Em vez de insistirmos numa solução interessante mas pouco rentável no contexto do projeto, aceitámos recuar e concentrar esforço onde o impacto era mais imediato: a documentação.

## 2. Reformulação do problema e consolidação conceptual

Em paralelo com a modelação, houve um trabalho mais profundo de clarificação do próprio problema. A formulação inicial, centrada em "simular sensores para incêndios", mostrou-se demasiado estreita. Passámos então a tratar o simulador como uma peça estrutural de uma plataforma de prevenção, deteção e apoio à decisão.

Esta reformulação obrigou a separar conceitos que antes apareciam misturados. Tornou-se importante distinguir meteorologia observada, perigo meteorológico e risco operacional. Essa distinção alterou a forma de olhar para o sistema: o simulador deixou de ser apenas um emissor de valores e passou a ser uma fonte de verdade física plausível, útil para demonstração, calibração, teste e validação.

Também foi nesta fase que fechámos a lógica de índices e de apoio à decisão. Em vez de escolher logo um score único, comparámos famílias de índices e definimos um núcleo mais coerente: `FWI` como referência principal, `PIR/RCM` como enquadramento operacional português e `KBDI` como complemento de seca acumulada. Outros índices relevantes, como `Haines` e `NFDRS`, ficaram como referências comparativas, mas não como obrigação imediata de implementação.

Esta fase foi importante porque fixou a gramática do resto do trabalho. Se os índices têm memória, a simulação também tem de ter memória. Se o risco depende do território, a simulação não pode viver apenas de meteorologia. A partir daqui, o trabalho ganhou uma estrutura bastante mais consistente.

## 3. Fontes, variáveis, cenários e validação

Com o problema melhor definido, a investigação passou a trabalhar por camadas mais concretas. Primeiro organizámos critérios para escolher áreas de estudo, evitando decisões apenas intuitivas. Entraram em jogo contraste climático, regime de fogo, cobertura de dados, tipos de combustível, topografia e relevância para uma primeira implementação. Foi assim que se consolidou o trio Proença-a-Nova, Covilhã/Serra da Estrela e Monchique, com a consciência de que parte desta escolha é factual e parte é metodológica.

Depois fechámos as variáveis de base do simulador: temperatura, humidade relativa, vento médio, direção, rajada e precipitação. A seguir veio a fase das fontes, mas já não como simples lista. Passámos a trabalhar cada fonte pelo seu papel no sistema. `IPMA` e `ERA5-Land` surgiram como referências para clima local e continuidade; `ICNF`, `FIRMS` e `PT-FireSprd` ajudaram a enquadrar território, histórico e episódios de fogo; `CORINE` e `Tree Cover Density` entraram como suporte à leitura territorial e ao combustível.

Uma das decisões mais fortes desta fase foi a separação do simulador em três camadas: verdade física, erro de medição e erro de pipeline. Esta distinção permitiu organizar de forma muito mais clara o vocabulário técnico do sistema. Na camada de medição ficaram elementos como ruído, bias, drift, quantização, lag, outliers, stuck values e intermitência. Na camada de pipeline ficaram perda, duplicação, out-of-order, latência, jitter, timestamps errados, bursts e dados parciais.

Também foi aqui que o regime temporal da simulação se consolidou. Deixou de fazer sentido falar seriamente de "um dia isolado" como modo principal. A lógica dos índices, da secura acumulada e dos antecedentes meteorológicos obrigou a assumir a simulação contínua como regime central. Ao mesmo tempo, ficou estabelecido que a política de amostragem pode ser adaptativa sem alterar a semântica canónica dos índices.

Com esta base, os cenários `A`, `B` e `C` deixaram de ser apenas exemplos narrativos. O cenário `A` passou a representar um dia plausível e não extremo; o cenário `B` passou a corresponder a um episódio de maior propensão ao fogo, ancorado em evidência real ou muito plausível; o cenário `C` passou a ser entendido como uma degradação da mesma base física, e não como um "terceiro clima". Em paralelo, começaram a ficar mais claras as metodologias de calibração e validação, incluindo validação física, estatística, operacional, espacial e temporal.

## 4. Passagem da investigação para a implementação

À medida que a investigação foi fechando termos, fontes e critérios, a conversa mudou de natureza. Deixou de estar centrada em "o que faz sentido estudar" e passou a focar-se em "o que temos de construir agora". Foi neste momento que o trabalho convergiu para um roadmap técnico mais concreto para o NatureProtector.

Começámos então a ligar o plano conceptual à implementação real. A área piloto foi fixada em Proença-a-Nova. O objetivo passou a ser construir uma baseline de dados utilizável pelo simulador e pela futura camada de controlo. Isso traduziu-se em artefactos concretos: limite da área, grelha de 1 km, atributos por célula, meteorologia de referência, histórico de fogo, shortlist de cenários e manifests de proveniência.

Ao mesmo tempo, a ligação entre baseline e simulador ficou mais concreta. Os cenários começaram a ser tratados como objetos executáveis, e não apenas como texto metodológico. A combinação entre artefactos curados, manifests, candidatos a cenário e parametrização do simulador passou a desenhar um caminho claro entre a investigação e a runtime.

No fim desta etapa, o problema principal já não era "como continuar a pesquisar", mas sim "como montar, curar e ligar corretamente o baseline ao motor de simulação e ao pipeline". Esta mudança de foco foi uma das evoluções mais importantes desta quinzena.

## 5. Consolidação documental do repositório

Depois de percebermos que a modelação automática ainda não estava pronta para ser o centro da estratégia, a prioridade passou a ser consolidar a documentação do projeto. O primeiro passo foi rever o repositório como ele realmente existe: código, `docs`, `data`, `infra`, `tests` e projetos em `src`.

Esta revisão mostrou uma assimetria importante. Já existia documentação útil na frente de dados e planeamento, mas faltava um ponto de entrada global e faltava documentação local junto dos módulos principais. A resposta a esse problema não foi escolher uma única filosofia de documentação, mas sim combinar duas: documentação transversal e relativamente estável em `docs/`, e documentação estável mas local em `README.md` junto de cada projeto ou área relevante.

Com essa lógica, o repositório ganhou uma nova camada documental. Passou a existir um `README.md` principal, um índice em `docs/`, um índice técnico em `src/`, documentação em `infra/` e `tests/`, e um `README.md` em cada projeto principal de `src`. O princípio foi sempre preservar o conhecimento já existente, sem apagar ou desvalorizar a documentação anterior.

Na escrita dessa documentação houve uma preocupação clara em refletir o estado real do projeto. Ficou registado, por exemplo, que ainda existem resíduos de uma arquitetura anterior no `Simulator.Host`, que `NatureProtector.Shared` continua a misturar contratos com detalhes de `RabbitMQ`, que a pipeline ainda não é durável e que a API continua numa fase embrionária. Esta honestidade foi importante para impedir que a documentação descrevesse uma arquitetura-alvo como se já estivesse concluída.

## 6. Consolidação de testes e medição de coverage

Outra evolução relevante desta quinzena foi a frente de testes. Para além do domínio central, começaram a ganhar forma projetos de teste mais próximos da runtime e da integração entre módulos, nomeadamente nas áreas de `Simulator.Host`, `Prevention.Host`, `Infrastructure.Influx`, `Backoffice.Api` e integração entre simulador e prevenção. Isto foi importante porque o projeto deixou de depender quase exclusivamente da confiança no código de domínio e passou a ter validação automática sobre partes mais próximas do comportamento real do sistema.

Em paralelo, foi integrada a medição de coverage com `coverlet.collector` e com o collector `XPlat Code Coverage`, permitindo gerar relatórios agregados em formato `cobertura`. Também ficou estabelecido um caminho repetível para produzir um relatório consolidado através do script `scripts/tests/generate-coverage-report.ps1`, o que ajudou a transformar a coverage numa ferramenta de acompanhamento técnico e não apenas num resultado ocasional.

O efeito prático desta integração foi duplo. Por um lado, passou a existir maior visibilidade sobre que assemblies e que zonas do código já estão bem protegidos por testes. Por outro, ficou mais fácil identificar hotspots ainda frágeis, sobretudo em componentes de runtime e de publicação. Em termos de narrativa do projeto, esta frente teve um papel importante: ajudou a mostrar que o trabalho não se limitou a documentação e planeamento, tendo havido também uma consolidação concreta da qualidade técnica e da capacidade de validação automática.

## 7. Reanálise do roadmap e lições desta fase

Depois da consolidação documental, foi feita uma reanálise cruzada entre o que os novos `README` descrevem, o estado real do código e os documentos de planeamento. A conclusão principal foi que os `README` ficaram, no geral, sincronizados com o repositório, mas o roadmap e o mapa de lacunas precisavam de pequenos ajustes para refletir melhor a história real do projeto.

O ponto mais importante desta reanálise foi perceber que o projeto não evoluiu de forma totalmente linear. As fases iniciais do roadmap, ligadas à modularização e ao plano de controlo em `PostgreSQL`, continuam por fechar. Ao mesmo tempo, a frente de datasets, manifests e cenários executáveis está mais avançada do que o roadmap inicialmente assumia. Em vez de tratar isto como erro, passámos a lê-lo como um desvio consciente que também trouxe valor: ajudou a clarificar melhor que tabelas fazem falta, que artefactos devem ser versionados e como os cenários devem ser representados.

Foi precisamente daqui que surgiu a utilidade de manter um diário técnico. O diário não serve apenas para listar tarefas feitas. Serve para explicar decisões, desvios à ordem inicial, razões desses desvios, impacto no trabalho seguinte e estado de maturidade de cada frente. Em termos de síntese, esta quinzena mostrou uma progressão clara: começámos por tentar modelar e compreender o sistema; passámos por fechar a investigação e a linguagem técnica; e terminámos com uma base documental e de planeamento bastante mais sólida para a fase seguinte de implementação.

---

## Recapitulação Quinzenal

### Período

7 de abril a 21 de abril de 2026

### Objetivo desta entrada

Registar, de forma primeiro estruturada e só depois mais detalhada, o trabalho de consolidação arquitetural, persistência, bootstrap, backoffice, observabilidade e documentação operacional realizado nesta quinzena.

## Índice

- Resumo Estruturado
- 1. Consolidação da arquitetura e da explicação do sistema
- 2. Estruturação de documentação operacional e guias de exploração
- 3. Persistência relacional e passagem efetiva para PostgreSQL
- 4. Bootstrap do plano de controlo e integração com a runtime
- 5. Evolução do Backoffice e correção da superfície HTTP
- 6. Observabilidade com InfluxDB, Grafana e baseline local
- 7. Limpeza técnica, alinhamento geral e resultado da quinzena

## Resumo Estruturado

### O que foi feito

1. Consolidámos uma nova camada documental para explicar a arquitetura do projeto de forma progressiva, coerente com o código e útil tanto para relatório como para apresentação.
2. Criámos documentação prática para exploração do repositório, execução do que já existe hoje e integração entre InfluxDB e Grafana, para tornar o sistema mais fácil de compreender e demonstrar.
3. Estruturámos e fixámos a camada de persistência em PostgreSQL, deixando de a tratar como intenção futura e passando a descrevê-la e usá-la como plano de controlo, inbox durável e base das projeções operacionais.
4. Materializámos o bootstrap do plano de controlo, com carga da área piloto, grelha, sensores, cenários e artefactos de dataset, criando um caminho repetível entre baseline de dados e runtime.
5. Evoluímos o Backoffice e a API para exporem de forma mais clara a configuração ativa, as áreas, os sensores, os cenários, as `simulation runs` e o estado operacional persistido.
6. Corrigimos problemas de rotas e alinhámos a superfície HTTP com o estado real do projeto, reduzindo a distância entre o que estava implementado e o que era de facto navegável e demonstrável.
7. Fizemos limpeza técnica em componentes antigos do simulador e da prevenção, reforçámos testes e melhorámos o alinhamento global entre código, documentação, infraestrutura e runtime.

### Resultado principal da quinzena

O resultado principal desta quinzena foi a passagem do projeto para um estado muito mais explicável e operacional em três frentes ao mesmo tempo:

- na frente documental, porque a arquitetura passou a estar descrita por documentos especializados, guias operacionais e diagramas mais orientados ao estado real do sistema;
- na frente de persistência, porque o PostgreSQL passou a ficar claramente definido e materializado como base do plano de controlo, da inbox durável e das projeções;
- na frente de execução e demonstração, porque passou a existir um caminho mais claro para bootstrapar o sistema, arrancar os hosts, consultar a API, observar o fluxo e perceber o que já vive hoje no repositório.

## 1. Consolidação da arquitetura e da explicação do sistema

Nesta quinzena investimos muito trabalho em tornar a arquitetura do projeto realmente legível. O objetivo já não era apenas ter documentação espalhada ou diagramas soltos, mas sim construir uma explicação progressiva e tecnicamente séria do sistema, começando pelo contexto, passando pelo fluxo global de dados, chegando ao simulador, à pipeline operacional, à persistência e à implementação real.

Foi nesta fase que a documentação arquitetural ganhou uma forma muito mais madura. A arquitetura deixou de ser apresentada apenas como uma composição genérica de componentes e passou a ser descrita como uma cadeia completa: fontes externas, baseline, manifests, cenários, simulador, sensores virtuais, eventos, pipeline, persistência, risco, alertas e visualização. Isto foi importante porque o projeto tem muito valor precisamente nessa coerência end-to-end, e essa coerência não estava suficientemente explícita.

Também foi aqui que trabalhámos na revisão de diagramas e da narrativa que os acompanha. Em vez de misturar tudo num único desenho, procurámos tornar cada peça mais focada e mais alinhada com o que realmente existe. Houve um esforço claro para separar arquitetura conceptual, runtime real, deployment, domínio e persistência, e para fazer com que a documentação não vendesse como implementado aquilo que ainda é apenas arquitetura-alvo ou evolução futura.

## 2. Estruturação de documentação operacional e guias de exploração

Uma parte muito importante desta quinzena foi a criação de documentação prática, pensada para quem precisa de perceber rapidamente o repositório ou de correr o sistema sem conhecimento prévio. Isto respondeu a uma lacuna real do projeto: já havia bastante trabalho feito, mas faltava uma forma clara de o ler e explorar.

Foram criados e consolidados vários documentos novos em `docs/architecture`. Entre eles, um guia de exploração do repositório, um documento focado apenas nas capacidades atuais e em como executar o que já existe, um guia específico para `Grafana` e `InfluxDB`, e um documento consolidado sobre a arquitetura de `PostgreSQL`. Cada um destes documentos foi pensado para um problema diferente: perceber o repositório, perceber o que já corre, perceber a observabilidade, ou perceber a persistência relacional.

Este trabalho não foi apenas de escrita. Houve também reanálise do código e dos README já existentes para garantir consistência. A documentação principal, os índices de `docs/` e `docs/architecture/`, os README dos módulos e a documentação de infraestrutura foram revistos para servirem como um sistema de navegação coerente. Em termos práticos, o repositório deixou de depender de conhecimento implícito e passou a oferecer percursos de leitura bastante mais claros.

## 3. Persistência relacional e passagem efetiva para PostgreSQL

Nesta quinzena a parte de persistência foi uma das frentes mais importantes. O projeto passou a ter uma camada relacional muito mais definida, com a criação e integração de `NatureProtector.Infrastructure.Postgres` como módulo responsável por adaptar o domínio e a runtime ao `PostgreSQL`.

Esta camada passou a estruturar-se explicitamente em três schemas principais: `control`, `pipeline` e `projection`. O schema `control` ficou responsável pela configuração ativa do sistema, pela área piloto, grelha, sensores, perfis, cenários, artefactos de dataset e `simulation runs`. O schema `pipeline` passou a guardar a inbox durável, as tentativas de processamento, rejeições técnicas e quarentena. O schema `projection` passou a fixar logs duráveis, snapshots de risco, estado operacional por área, estado por célula e alertas ativos simples.

Do ponto de vista técnico, isto traduziu-se na criação de records persistentes, `DbContext`, migrations incrementais e componentes de suporte à injeção de dependências. A persistência deixou assim de estar distribuída de forma difusa ou apenas em memória. Passou a existir uma base relacional que serve de fonte de verdade para o plano de controlo e de ponto de commit durável para o fluxo operacional.

Na prevenção, esta evolução foi particularmente relevante. O `Prevention.Host` passou a trabalhar com uma inbox durável em `PostgreSQL`, com tentativas registadas, retries internos, classificação de falhas e quarentena persistida. Para além disso, leituras aceites, avaliações de risco e snapshots agregados deixaram de existir apenas como efeitos transitórios e passaram a ser guardados em repositórios concretos sobre o schema `projection`. Isto tornou a pipeline muito mais auditável e muito mais próxima de um comportamento operacional real.

## 4. Bootstrap do plano de controlo e integração com a runtime

Outra evolução central desta quinzena foi a materialização do bootstrap do plano de controlo. Em vez de ficar dependente de preparação manual ou apenas de ficheiros no sistema de dados, o projeto passou a ter um utilitário específico, `NatureProtector.Postgres.Bootstrap`, e um script de apoio, `scripts/postgres/bootstrap-control-plane.ps1`, para carregar uma primeira configuração utilizável em `PostgreSQL`.

Este bootstrap passou a semear `configuration_versions`, `areas`, `grid_cells`, `sensor_profiles`, `sensor_networks`, `sensor_nodes`, `scenario_definitions`, `dataset_artifacts` e `scenario_dataset_bindings`. Em termos práticos, isto criou uma ponte real entre a baseline de dados da área piloto e a runtime do sistema. A informação deixou de estar apenas em ficheiros curados e passou a estar resolvida também num plano de controlo persistido, pronto a ser lido pelos hosts.

O simulador passou a aproveitar diretamente esta evolução. Com `ControlPlaneEnabled`, o `Simulator.Host` consegue agora resolver a área, o cenário e os sensores ativos a partir do `PostgreSQL`, em vez de depender apenas de configuração local. Além disso, passou a registar `simulation runs` persistidas, o que é importante para ligar uma execução concreta ao cenário escolhido, ao tempo lógico e ao estado real da execução.

Isto foi um avanço muito importante porque tornou o arranque do sistema muito mais reproduzível. Deixou de ser necessário interpretar o projeto apenas como um conjunto de ficheiros e hosts soltos. Passou a existir um caminho claro: levantar baseline, bootstrapar o plano de controlo, arrancar hosts, produzir eventos e observar o efeito persistido.

## 5. Evolução do Backoffice e correção da superfície HTTP

Na frente do Backoffice e da API houve também evolução real e não apenas documental. O `NatureProtector.Backoffice.Api` deixou de ser visto como um simples esqueleto ASP.NET Core e passou a assumir-se como a primeira superfície HTTP séria do plano de controlo e do estado operacional.

Foram introduzidas estruturas específicas para esta frente, incluindo configuração própria do Backoffice, contratos de `control plane`, serviços dedicados e novos controladores. A API passou a expor consultas sobre configurações ativas, áreas, grelha, sensores, cenários, `simulation runs`, estado operacional por área, estado por célula e alertas ativos simples. Isto deu ao projeto uma superfície externa muito mais útil para demonstração, exploração e validação manual.

Também houve trabalho de correção de rotas e de alinhamento entre o comportamento esperado e o que a API realmente devolvia. Em vez de termos exemplos pouco claros ou superfícies incoerentes, passámos a ter um conjunto de rotas mais coerente sob `api/control/...`, um ficheiro `.http` mais útil para exploração manual e uma separação mais clara entre endpoints disponíveis, endpoints condicionados por estado e casos em que ainda pode não existir projeção suficiente para devolver dados.

Do ponto de vista do repositório, esta frente foi importante porque tornou o backoffice menos abstrato. A partir daqui, já não estamos apenas a falar de persistência e processamento interno. Passámos também a ter uma primeira camada de leitura e inspeção do sistema que é demonstrável e que ajuda a defender tecnicamente o que já foi construído.

## 6. Observabilidade com InfluxDB, Grafana e baseline local

Para além da persistência relacional, trabalhámos também na frente de observabilidade e na explicação da baseline local. O projeto ganhou documentação específica para explicar como ligar `Grafana` ao `InfluxDB`, como validar a datasource, como explorar as medições existentes e como começar a desenhar dashboards com base no que a pipeline já escreve hoje.

Isto obrigou a rever não só o guia documental mas também os ficheiros de infraestrutura associados a `Grafana`, `InfluxDB` e à datasource provisionada. Ficou mais claro qual é o caminho suportado pelo repositório para leitura dos dados do `InfluxDB`, que tabelas existem hoje, que queries fazem sentido e como passar da telemetria bruta a painéis úteis de observabilidade.

Ao mesmo tempo, a baseline local ficou mais bem explicada. A documentação passou a indicar com mais precisão como levantar `RabbitMQ`, `PostgreSQL`, `InfluxDB` e `Grafana`, que credenciais usar, o que esperar de cada serviço e como validar que o sistema está vivo. Isto ajudou a tornar a execução do projeto muito mais previsível e menos dependente de memória informal da equipa.

## 7. Limpeza técnica, alinhamento geral e resultado da quinzena

Finalmente, esta quinzena também foi marcada por limpeza técnica e alinhamento global. Houve remoção de componentes legados do simulador e da prevenção que já não faziam sentido na arquitetura atual, sobretudo elementos antigos ligados a persistência local, validação residual e caminhos de ingestão que estavam a ficar deslocados na estrutura do repositório.

Também houve reforço de testes em várias frentes, nomeadamente na integração entre simulador e prevenção, nos componentes novos da persistência, no comportamento do `Backoffice.Api` e em partes da infraestrutura e configuração. Isto foi importante porque as mudanças desta quinzena mexeram em peças centrais da runtime e não podiam ficar sustentadas apenas por documentação.

Em síntese, esta quinzena não foi apenas de “escrever docs”. Foi uma fase de consolidação real do sistema. A documentação avançou muito, mas avançou ao mesmo tempo que a arquitetura relacional foi formalizada, que o bootstrap do plano de controlo foi materializado, que o backoffice passou a expor uma superfície útil e que a runtime ficou mais próxima de um fluxo end-to-end coerente, persistido, observável e demonstrável.

## 8. Diagnóstico operacional da pipeline de prevenção e estabilização da baseline local

### Resumo estruturado

#### O que foi feito

1. Foi feita uma análise detalhada ao comportamento real da pipeline de prevenção, cruzando código, logs de runtime, queries sobre o estado persistido e configuração ativa do simulador e do plano de controlo.
2. Foi identificado e corrigido um problema semântico importante na entrada do fluxo: eventos com `OperationalState=Invalid` estavam a entrar na pipeline de processamento como se fossem leituras aceites.
3. Foi melhorado o registo de rejeições, de modo a tornar mais auditável o caminho dos eventos rejeitados antes de entrarem no processamento normal.
4. Foi estabilizado o comportamento do consumidor RabbitMQ, incluindo controlo mais explícito do número de mensagens em voo, para reduzir o risco de crescimento descontrolado da fila e facilitar o diagnóstico.
5. Foi confirmado que a cadência real de produção do simulador não depende apenas de `appsettings`, mas também do cenário ativo carregado a partir do plano de controlo em PostgreSQL.
6. Foi clarificado o caminho permanente para reduzir a pressão de produção local, atuando sobre o catálogo gerado de cenários, sobre o bootstrap do plano de controlo e sobre a seleção de sensores ativos.
7. Foi tentada uma otimização da consulta `GetLatestByAreaAsync`, mas essa alteração introduziu uma falha de tradução no `Entity Framework Core`, tendo sido revertida para restaurar a estabilidade funcional.
8. Foram introduzidas medições temporais mais finas no fluxo de processamento, permitindo isolar com maior precisão o custo relativo de persistência, projeções, queries e escrita em InfluxDB.
9. Foi comprovado, por medição direta, que o principal gargalo atual da pipeline local está nas escritas para `InfluxDB`, e não no `PostgreSQL` nem na consulta `GetLatestByAreaAsync`.

#### Problemas identificados

1. Existia um erro semântico no tratamento de leituras inválidas, que contaminava a pipeline aceite e tinha de ser travado logo à entrada.
2. A taxa de produção do simulador podia ultrapassar a capacidade real de consumo local, sobretudo quando o número de sensores ativos bootstrapados no plano de controlo era demasiado elevado para o modo de desenvolvimento.
3. A configuração local podia induzir em erro porque, com `ControlPlaneEnabled`, a runtime efetiva é resolvida a partir do cenário persistido em PostgreSQL e não apenas do ficheiro local de configuração.
4. Uma tentativa de otimização do repositório de avaliações de risco provocou uma exceção de tradução LINQ no `Entity Framework Core`, mostrando que nem toda a otimização aparente é segura no estado atual do código.
5. O custo dominante do fluxo de prevenção não estava onde inicialmente parecia estar: os tempos medidos mostraram que as três escritas por evento para `InfluxDB` consomem quase todo o tempo total da pipeline.

#### O que ficou comprovado

1. As rejeições por `invalid_operational_state` passaram a acontecer de forma explícita e os eventos inválidos deixaram de entrar no `event_inbox`.
2. O estado persistido da inbox, das tentativas e dos resultados voltou a mostrar um comportamento funcional estável, sem sinais dominantes de retries ou quarentenas anormais nesta carga local controlada.
3. A consulta `GetLatestByAreaAsync`, na versão segura atualmente ativa, deixou de ser o suspeito principal do atraso.
4. O gargalo real da baseline local está concentrado na observabilidade temporal, mais especificamente no caminho de escrita para `InfluxDB`.

### Desenvolvimento detalhado

Nesta fase do trabalho foi feita uma análise mais operacional da pipeline de prevenção, já não apenas ao nível da arquitetura-alvo ou da organização do repositório, mas ao nível do comportamento concreto do sistema em runtime. O objetivo foi perceber por que razão a execução local ainda apresentava atrasos relevantes, em que ponto do fluxo esses atrasos apareciam e que alterações eram necessárias para estabilizar o sistema sem abrir uma frente excessiva de refatoração.

O primeiro problema relevante identificado foi semântico. Verificou-se que eventos produzidos com `OperationalState=Invalid` estavam a entrar na pipeline como se fossem leituras aceites. Isto significava que dados marcados como inválidos podiam ainda assim alimentar persistência aceite, avaliação de risco, snapshots agregados e projeções. A correção passou por reforçar a validação logo na entrada do `PreventionWorker`, antes do evento ser assumido como válido para o fluxo principal. Em paralelo, foi melhorado o mecanismo de registo das rejeições, para que o caminho desses eventos ficasse mais claro e auditável.

Ao mesmo tempo, foi analisada a relação entre a taxa de produção do simulador e a capacidade real de consumo local. Ficou claro que o modo `ControlPlaneEnabled` altera de forma decisiva a leitura do problema: a cadência e o volume da simulação dependem do cenário persistido em `PostgreSQL` e da rede de sensores ativa bootstrapada, e não apenas do que está escrito no `appsettings.json`. Isto levou a rever a forma correta de reduzir a pressão local, não apenas ajustando o cenário gerado, mas também garantindo que o bootstrap do plano de controlo consegue desativar sensores antigos quando a seleção de células piloto é reduzida. Sem essa desativação explícita, baixar o número-alvo de estações no bootstrap não teria efeito real sobre a quantidade de sensores ativos.

Durante esta fase também foi tentada uma otimização da consulta `GetLatestByAreaAsync`, com o objetivo de evitar carregar o histórico completo das avaliações de risco de uma área em memória. No entanto, essa alteração produziu uma falha de tradução no `Entity Framework Core`, com impacto direto no processamento do pipeline. A consequência prática foi clara: em vez de insistir numa otimização que quebrava a estabilidade funcional, a consulta foi reposta numa forma segura, aceitando temporariamente um custo maior para recuperar o comportamento correto do sistema.

Depois da estabilidade funcional ter sido recuperada, a análise passou a focar-se em medições temporais mais finas. Foram introduzidos logs de duração por etapa, permitindo separar de forma objetiva o custo da persistência relacional, das projeções, da consulta do estado mais recente por área e das escritas para `InfluxDB`. O resultado dessa medição foi decisivo: a quase totalidade do `pipeline_total_ms` estava a ser consumida nas três escritas sequenciais para `InfluxDB`, enquanto as operações sobre `PostgreSQL` e a consulta `GetLatestByAreaAsync` permaneciam tipicamente na ordem dos poucos milissegundos ou, em alguns casos, de poucas dezenas de milissegundos.

Esta conclusão é importante porque altera a leitura do estado atual do projeto. O principal problema já não é um erro funcional de pipeline, nem um bloqueio no inbox, nem um descontrolo do consumidor RabbitMQ em carga reduzida. O principal problema passa a ser o custo operacional da observabilidade temporal local, mais concretamente da forma como o `InfluxWriteService` está a ser usado pelo fluxo de prevenção. Em termos práticos, o sistema ficou semanticamente mais correto e estruturalmente mais controlado, mas continuou a sofrer atrasos significativos devido ao custo de observabilidade.

### Trabalho a fazer na continuação desta frente

1. Introduzir um modo local em que as escritas para `InfluxDB` possam ser desligadas por configuração, permitindo desenvolvimento e diagnóstico com a pipeline funcional mas sem o principal gargalo temporal.
2. Rever o `InfluxWriteService` para perceber se as três escritas por evento podem ser amortecidas, agrupadas ou executadas de forma menos penalizadora para a baseline local.
3. Manter, por agora, a versão segura de `GetLatestByAreaAsync`, só voltando a tentar uma otimização dessa consulta quando houver uma abordagem tecnicamente segura e validada.
4. Garantir que o catálogo gerado de cenários, o bootstrap do plano de controlo e a runtime do simulador se mantêm sincronizados, evitando novas derivações entre configuração pretendida e comportamento efetivo.
5. Consolidar a documentação operacional desta frente, deixando explícito que a principal limitação local atual não está no armazenamento relacional nem no pipeline de retries, mas sim no custo das escritas para `InfluxDB`.
6. Só depois de estabilizado o modo local sem pressão excessiva é que deve voltar a ser equacionada uma otimização mais profunda da pipeline, para evitar abrir uma refatoração larga antes de o comportamento atual estar suficientemente controlado.

# Recapitulação Quinzenal

## Período

22 de abril a 5 de maio de 2026

## Objetivo desta entrada

Registar, de forma primeiro estruturada e só depois mais detalhada, o trabalho de preparação da apresentação de progresso, consolidação da documentação de implementação, diagnóstico operacional da pipeline, correção de riscos de robustez, preparação da fronteira de cálculo de risco, manutenção do repositório e alinhamento entre código, documentação e comportamento real da baseline.

## Índice

* Resumo Estruturado
* 1. Preparação e revisão da apresentação de progresso
* 2. Consolidação do `implementation.md`, `architecture.md` e dos diagramas de implementação
* 3. Organização das ferramentas de documentação técnica
* 4. Diagnóstico operacional da pipeline em execução real
* 5. Instrumentação, cronómetros, logs e observabilidade transversal
* 6. InfluxDB como observabilidade configurável e não crítica
* 7. Idempotência concorrente e robustez dos adaptadores PostgreSQL
* 8. Validação semântica `area_id` ↔ `sensor_id`
* 9. Normalização, `RiskInput` e elegibilidade para risco
* 10. Simulator, bootstrap e plano de controlo
* 11. Backoffice API, contratos e testes
* 12. Manutenção do repositório, merge e validação de build
* 13. Resultado da quinzena e próximos passos

## Resumo Estruturado

### O que foi feito

1. Foi preparada e revista a apresentação de progresso do projeto, com reorganização da narrativa, seleção dos conteúdos principais, separação de slides extra e maior foco na evidência técnica da baseline demonstrável.
2. Foi consolidado o `implementation.md` como documento de onboarding técnico da implementação atual e foi atualizado o `architecture.md` para refletir melhor a arquitetura real, a arquitetura-alvo e os novos limites internos da pipeline.
3. Foi expandido e revisto o conjunto de diagramas de implementação, com vistas dedicadas ao simulador, prevenção, persistência, API, bootstrap, cenários, testes, rejeição, retry e quarentena.
4. Foram organizadas e validadas ferramentas de documentação técnica, incluindo Doxygen, DocFX, Structurizr, PlantUML, Graphviz, Docker, Draw.io e scripts auxiliares em `scripts/docs`.
5. Foi feito diagnóstico operacional da pipeline em execução real, com análise da ordem de processamento, volume de mensagens, pressão sobre o consumidor, tempos por etapa, comportamento RabbitMQ e impacto da configuração persistida em PostgreSQL.
6. Foram introduzidos e usados cronómetros, logs e elementos de observabilidade para medir operações críticas e identificar onde surgiam atrasos, acumulações ou custos operacionais relevantes.
7. Foi consolidado o modo local com InfluxDB desligado por configuração, usando `NoOpInfluxWriteService`, logging explícito e teste de pipeline para garantir que PostgreSQL, snapshots e projeções continuam a funcionar sem telemetria temporal.
8. Foi reforçada a distinção entre PostgreSQL como estado durável e InfluxDB como observabilidade temporal, evitando que falhas ou custos de observabilidade sejam confundidos com falhas do processamento operacional.
9. Foi corrigida a idempotência concorrente em pontos vulneráveis do padrão `read-then-insert`, tratando unique violations esperadas como duplicados legítimos sem mascarar erros reais.
10. Foi estabilizada a identidade dos `AreaRiskSnapshot`, que deixou de depender de um `Guid.NewGuid()` por tentativa e passou a usar o `EventId` da leitura como identidade estável do snapshot derivado desse evento.
11. Foi introduzida validação semântica entre `area_id` do envelope e `sensor_id` do payload, depois da inbox e antes da `ReadingRiskPipeline`, colocando em quarentena eventos com sensor inexistente, sensor inativo ou sensor pertencente a outra área.
12. Foi criada uma fronteira interna explícita entre evento bruto, leitura normalizada, input de risco e cálculo de risco: `EventEnvelope<SensorReadingProducedPayload> -> NormalizedReading -> RiskInput -> IRiskScoringService -> RiskAssessment`.
13. Foi introduzida uma etapa explícita de elegibilidade para risco: `NormalizedReading -> RiskEligibilityResult -> RiskInput`, preparando a distinção entre leitura aceite para auditoria e leitura efetivamente usada no cálculo de risco.
14. Foi implementada a semântica interna para leituras `NotEligible`: a leitura continua a ser persistida como accepted reading, mas a pipeline termina com sucesso sem score, sem `RiskAssessment`, sem `AreaRiskSnapshot`, sem projeções de risco e sem retry/quarentena.
15. Foi mantido o `SimpleRiskScoringService` como baseline demonstrável, mas a pipeline passou a depender de `IRiskScoringService`, reduzindo o acoplamento ao score simples atual e preparando a evolução futura para modelos mais ricos.
16. Foram revistos componentes da Backoffice API e testes associados, incluindo compatibilidade com contratos atualizados e alinhamento com a leitura de `control.*` e `projection.*`.
17. Foi feita manutenção do repositório, incluindo resolução de conflitos após `git pull`, atualização do `.gitignore`, validação de build, análise de artefactos gerados e separação entre código demonstrável e frentes experimentais.
18. Foram executadas validações sucessivas com `dotnet build --no-restore` e `dotnet test --no-restore`; no final das alterações da pipeline, a solução passou com 647 testes.
19. Foi feita uma atualização documental transversal para alinhar melhor o estado descrito do projeto com o estado real da branch, incluindo README, documentação de arquitetura, documentação de implementação, roadmap, documentação de testes e capacidades atuais da baseline.
20. Foram criados scripts de setup e validação local para verificar pré-requisitos do ambiente e o estado da baseline, incluindo .NET, Docker, Docker Compose, Git, Node/npm, Perl, MiKTeX, ficheiros `.env`, serviços Docker, RabbitMQ, PostgreSQL, InfluxDB, Grafana e Backoffice API.

### Resultado principal da quinzena

O resultado principal desta quinzena foi a passagem de uma baseline apenas demonstrável para uma baseline tecnicamente mais robusta, mais auditável e mais preparada para evoluir. A pipeline deixou de ser apenas um fluxo que consome leituras e calcula um score simples. Passou a ter limites internos mais explícitos: inbox durável, idempotência concorrente, validação semântica contra o plano de controlo, normalização, input de risco e elegibilidade.

Esta quinzena também clarificou uma decisão importante: a evolução para índices reais de risco, como FWI, KBDI ou Haines, não deve começar pela substituição direta da fórmula atual. Antes disso, era necessário preparar a pipeline para receber inputs mais ricos, distinguir leituras aceites de leituras elegíveis para risco, formalizar a normalização e evitar que a `ReadingRiskPipeline` acumulasse regras de modelo, janelas temporais, estado persistido e políticas de dados em falta.

Em paralelo, a documentação ficou mais alinhada com o comportamento real. O `implementation.md`, o `architecture.md`, os diagramas e a narrativa da apresentação passaram a refletir melhor a runtime: o que acontece antes da inbox, o que acontece depois da inbox, onde entram retry/quarentena, como se separa PostgreSQL de InfluxDB, e por que motivo o score atual deve ser tratado como baseline e não como modelo final.

---

## 1. Preparação e revisão da apresentação de progresso

Durante este período foi preparada e revista a apresentação de progresso do projeto NatureProtector. O trabalho passou pela reorganização da estrutura da apresentação, de forma a alinhar melhor com os critérios pedidos: introdução ao problema, explicação do módulo de prevenção, investigação realizada, requisitos abordados, pipeline implementada, trabalho futuro e demonstração.

Foi acrescentado um slide de organização da apresentação, para tornar mais claro o percurso da exposição. Esta alteração ajudou a enquadrar melhor a sequência dos temas e a reduzir o risco de a apresentação parecer apenas uma sucessão de conteúdos técnicos sem fio condutor.

Também houve trabalho de separação entre slides principais e slides extra. Os slides principais ficaram mais focados no essencial para o tempo disponível, enquanto os slides opcionais passaram a servir como apoio para perguntas, nomeadamente sobre tecnologias, diagramas mais detalhados, escolhas de arquitetura, persistência, pipeline e funcionamento interno.

A linha principal da apresentação passou a centrar-se na evidência de progresso: mostrar que já existe uma baseline demonstrável com simulação, transporte por RabbitMQ, persistência em PostgreSQL e InfluxDB, cálculo inicial de risco, API/backoffice e observabilidade em Grafana. Esta abordagem ajudou a transformar a apresentação numa defesa do estado real do projeto, e não apenas numa descrição da ideia geral.

---

## 2. Consolidação do `implementation.md`, `architecture.md` e dos diagramas de implementação

Nesta quinzena foi consolidada a documentação de implementação do projeto, com foco em explicar como a solução está realmente organizada no repositório e como os principais fluxos técnicos funcionam na prática.

O `implementation.md` passou a assumir o papel de ponto de entrada para a implementação atual. A intenção foi evitar que a compreensão do sistema dependesse apenas da leitura dispersa de código, diagramas soltos ou conhecimento informal da equipa. Este documento passou a funcionar como uma vista de síntese da baseline implementada, ligando runtime, persistência, pipeline, API, simulador, observabilidade e testes.

Depois das alterações feitas na pipeline, o documento teve de ser revisto para não ficar desatualizado. Em particular, foram identificadas zonas que já não podiam continuar a dizer que a prevenção processava diretamente o envelope bruto até ao score. A nova leitura correta passou a ser: o evento é recebido, materializado na inbox, validado semanticamente, convertido internamente em `NormalizedReading`, avaliado quanto à elegibilidade e só depois transformado em `RiskInput`.

O `architecture.md` também foi atualizado para refletir esta evolução. A arquitetura deixou de descrever apenas a intenção futura de `accepted / rejected / normalized` e passou a reconhecer que já existe uma primeira fronteira interna de normalização e input de risco. Ao mesmo tempo, manteve-se a distinção correta: isto ainda não significa que o projeto tenha uma implementação final de índices reais, nem que exista publicação externa de eventos `ReadingNormalized`. Trata-se de uma fronteira interna preparada para evolução.

Em paralelo, foi expandido o conjunto de diagramas de implementação em `docs/architecture/diagrams`. Em vez de tentar representar tudo num único diagrama demasiado denso, foram criados ou revistos diagramas especializados para diferentes aspetos da solução: fluxo nominal do simulador, fluxo nominal da prevenção, rejeição, retry e quarentena, bootstrap do plano de controlo, persistência, organização do repositório, cenários e manifestos, caminhos de leitura da API e mapa de testes.

Esta separação tornou os diagramas mais úteis, porque cada um passou a responder a uma pergunta técnica concreta. O foco principal foi alinhar os diagramas com o comportamento real da runtime, sobretudo nos pontos onde a leitura errada poderia levar a conclusões incorretas: momento do `BasicAck`, papel da inbox, caminho da quarentena, diferença entre observabilidade e estado operacional, e nova fronteira de cálculo de risco.

Além do `implementation.md` e do `architecture.md`, foram também atualizados documentos auxiliares para reduzir desalinhamentos entre documentação e branch atual. Isto incluiu o `README.md`, `docs/README.md`, `docs/architecture/README.md`, `current-capabilities-and-how-to-run.md`, `project-completion-roadmap.md` e `tests/README.md`. O objetivo foi tornar mais claro o que já é baseline demonstrável, o que é experimental, o que depende de setup local e que partes ainda devem ser tratadas como trabalho futuro.

---

## 3. Organização das ferramentas de documentação técnica

Outro eixo importante desta quinzena foi a organização das ferramentas de documentação técnica do projeto. O objetivo foi perceber que ferramentas já estavam funcionais, que partes precisavam de correção e como cada uma se encaixa no processo global de documentação.

No caso do Doxygen, foi validada a instalação das dependências relevantes, nomeadamente Java, PlantUML e Graphviz. A partir daí, foi possível confirmar que a geração local da documentação estava operacional. Houve trabalho de limpeza de configuração, regeneração controlada de documentação e reforço das páginas manuais.

Foi criado e ajustado um `Doxyfile.local`, pensado para geração local em ambiente Windows, com output local separado. A documentação passou a conseguir gerar páginas HTML, XML, diagramas e relações automáticas entre classes, ficheiros, diretórios e chamadas. Também foram identificados alguns warnings e corrigidos os que faziam sentido nesta fase, em especial referências frágeis e comentários XML incompletos.

No caso do DocFX, o foco foi mais organizacional. A preocupação principal foi consolidar a explicação sobre o que a ferramenta representa no projeto, que ficheiros são fonte, que ficheiros são gerados, como limpar outputs e como utilizar a documentação localmente.

No Structurizr, o trabalho foi mais corretivo. A primeira validação mostrou problemas reais de sintaxe e de compatibilidade com a versão da ferramenta usada. Também ficou claro que a utilização local deveria depender de Docker, em vez de instalações manuais ou ferramentas descontinuadas. A solução passou por usar a imagem `structurizr/structurizr`, tanto para validação como para exportação e execução local.

Também foram organizados scripts auxiliares em `scripts/docs`, para tornar a geração, limpeza e validação da documentação menos dependente de comandos manuais dispersos. Esta organização ajuda a tornar o processo documental mais repetível e mais fácil de recuperar no futuro.

Foi também criada uma primeira camada de documentação operacional em `docs/setup/`, focada na configuração da baseline local. Esta documentação descreve os pré-requisitos, o ficheiro `.env`, o arranque dos serviços Docker, o bootstrap do control plane, a execução dos hosts .NET, os modos de InfluxDB, o estado atual do candidato de frontend em `webUI` e problemas frequentes de setup.

Em paralelo, foram criados scripts em `scripts/setup/` para validar o ambiente local de forma assistida. O `Test-LocalPrerequisites.ps1` verifica ferramentas e ficheiros necessários ou recomendados, como .NET SDK, Docker, Docker Compose, PowerShell, Git, `.env`, Node/npm, Strawberry Perl e MiKTeX. O `Test-LocalBaseline.ps1` verifica o estado da baseline depois de os serviços estarem ativos, incluindo RabbitMQ, PostgreSQL, InfluxDB, Grafana e Backoffice API.

---

## 4. Diagnóstico operacional da pipeline em execução real

Uma das frentes técnicas mais importantes desta quinzena foi o diagnóstico da pipeline em execução real. A necessidade surgiu porque, apesar de o fluxo nominal já estar definido arquiteturalmente, era necessário perceber como o sistema se comportava quando o simulador produzia leituras em sequência e o `Prevention.Host` tinha de as receber, persistir, processar e confirmar no RabbitMQ.

O foco deixou de ser apenas validar a lógica da pipeline e passou a ser observar o seu comportamento operacional. Foram analisados aspetos como o número de mensagens recebidas, a ordem de processamento, o tempo gasto em cada etapa, a acumulação de mensagens e os pontos onde podiam surgir atrasos ou falhas.

Este trabalho mostrou que a correção funcional não é suficiente para validar a baseline. Mesmo que a pipeline esteja logicamente correta, é necessário perceber se consegue acompanhar a taxa de entrada de eventos, se o consumidor se mantém estável, se a fila cresce de forma inesperada e se as operações de persistência e observabilidade introduzem atrasos relevantes.

A análise também confirmou que a pressão sobre o consumidor depende da relação entre a cadência do simulador, o número de sensores ativos, a configuração carregada do plano de controlo e o custo de cada operação realizada durante o processamento. Isto foi especialmente importante porque, com o modo `ControlPlaneEnabled`, a runtime efetiva não depende apenas do `appsettings.json`, mas também do cenário e dos sensores ativos persistidos em PostgreSQL.

Durante esta fase ficou mais claro que a pipeline deve ser analisada como uma cadeia operacional completa: receção da mensagem, validação, registo durável, validação semântica, processamento, persistência dos resultados, atualização de projeções, escrita de observabilidade e confirmação ao RabbitMQ.

---

## 5. Instrumentação, cronómetros, logs e observabilidade transversal

Para tornar o comportamento da pipeline mais visível, foram acrescentados cronómetros e logs em pontos críticos do fluxo. A intenção foi produzir evidência concreta sobre o percurso de cada mensagem e sobre o tempo gasto em operações específicas.

Esta instrumentação foi usada para perceber quando a mensagem era recebida pelo consumidor, quando era registada ou materializada na inbox, quando começava o processamento de negócio, quanto tempo demorava a execução do pipeline de risco, quando eram atualizadas projeções, quanto tempo era gasto em persistência e em que momento acontecia o `ack` ao RabbitMQ.

O resultado prático foi uma melhoria significativa da capacidade de diagnóstico. Em vez de apenas concluir que “o sistema está lento” ou que “as mensagens estão a acumular”, passou a ser possível localizar melhor onde o tempo estava a ser gasto e que parte da pipeline justificava investigação adicional.

Este trabalho também evoluiu para uma base mais estruturada de observabilidade em `NatureProtector.Shared/Observability`. A intenção foi evitar medições dispersas e criar uma forma mais consistente de identificar operações, tags e métricas nos vários serviços. Esta base comum foi depois refletida em pontos como a Backoffice API, o Simulator Host, o Prevention Host, o serviço de escrita para InfluxDB e a publicação de leituras por RabbitMQ.

A medição mostrou que o principal gargalo local atual não estava necessariamente no PostgreSQL nem na query de estado mais recente da área, mas sim no custo associado às escritas para InfluxDB. Esta conclusão foi importante porque alterou a prioridade das otimizações seguintes: antes de refatorar a pipeline de forma ampla, fazia mais sentido permitir desligar ou controlar melhor a observabilidade temporal em ambiente local.

---

## 6. InfluxDB como observabilidade configurável e não crítica

Na continuação do diagnóstico, foi revista a infraestrutura de InfluxDB. A análise mostrou que as escritas para InfluxDB estavam no caminho síncrono do processamento e que, embora sejam importantes para observabilidade, não devem ser confundidas com a persistência operacional principal da pipeline.

A decisão arquitetural assumida foi separar com mais clareza o papel de PostgreSQL e InfluxDB. O PostgreSQL continua a representar o estado durável e operacional da pipeline, incluindo inbox, tentativas de processamento, leituras aceites, avaliações de risco, snapshots e projeções. O InfluxDB é tratado como camada de observabilidade temporal, útil para séries temporais, dashboards e diagnóstico, mas não como fonte principal de verdade operacional.

Durante a revisão verificou-se que a infraestrutura para desligar o InfluxDB já estava praticamente montada: a opção `InfluxDb:Enabled` já existia, o dependency injection já resolvia `IInfluxWriteService` para `NoOpInfluxWriteService` quando `Enabled=false`, e o `Prevention.Host` já arrancava com InfluxDB desligado por omissão na baseline local.

O trabalho feito nesta fase foi, por isso, mais de fecho e validação do que de redesenho. Foi reforçado o logging do `NoOpInfluxWriteService`, tornando explícito que o InfluxDB está desativado e que a pipeline continua sem escrever telemetria temporal. Também foi acrescentada documentação curta no README sobre o comportamento por omissão em local.

Foi ainda adicionado um teste específico à `ReadingRiskPipeline` para confirmar que, com `NoOpInfluxWriteService`, a pipeline continua a persistir corretamente a leitura aceite, a avaliação de risco, o snapshot da área e as projeções operacionais. Isto protege o cenário em que a observabilidade temporal está desligada, mas o processamento principal continua ativo.

Esta alteração não mudou a ordem funcional da `ReadingRiskPipeline`, não alterou o `BasicAck`, não alterou contratos RabbitMQ, não mudou o simulador, não modificou as regras de score e não alterou a persistência PostgreSQL. O objetivo foi garantir que o modo local sem InfluxDB é explícito, testado e documentado.

---

## 7. Idempotência concorrente e robustez dos adaptadores PostgreSQL

Depois de estabilizado o modo local de observabilidade, foi tratada uma frente mais ligada à robustez da pipeline durável: a idempotência concorrente dos adaptadores PostgreSQL.

O problema identificado era o padrão `read-then-insert`. Em execução simples, verificar se um registo existe e inserir apenas quando não existe funciona. No entanto, em concorrência real, dois workers ou duas tentativas podem verificar ao mesmo tempo que o registo não existe e competir pela mesma unique constraint. Nesse caso, uma das inserções ganha e a outra pode falhar com `DbUpdateException`, apesar de representar apenas um duplicado legítimo.

A correção passou por tratar unique violations esperadas como resultado idempotente, sem esconder erros reais. Para isso, foi criado um helper localizado, `ExpectedUniqueViolationDetector`, capaz de reconhecer apenas constraints esperadas: em PostgreSQL por `ConstraintName`, e em SQLite por erro e assinatura de tabela/colunas nos testes.

Foram corrigidos vários pontos relevantes:

- `PostgresReadingEventInbox.StoreIncomingAsync`, onde a colisão concorrente em `EventId` passou a ser tratada como duplicado idempotente;
- `PostgresReadingEventInbox.TryStartDueRetryAsync`, onde uma corrida na criação de nova tentativa deixou de ser tratada como falha operacional;
- `PostgresAcceptedReadingRepository.AddAsync`, onde duplicados por `EventId` passaram a ser sucesso idempotente;
- `PostgresRiskAssessmentRepository.AddAsync`, onde duplicados por `SourceEventId` passaram a ser sucesso idempotente;
- `PostgresAreaRiskSnapshotRepository.SaveAsync`, onde duplicados por chave do snapshot passaram a ser idempotentes;
- `PostgresAreaOperationalProjectionStore.SaveCellAsync` e `SaveAsync`, onde a primeira criação concorrente passou a recair para update quando a insert perde a corrida.

Nesta frente foi também corrigido um ponto subtil: o `AreaRiskSnapshot` era persistido com um identificador novo a cada tentativa. Isto significava que retries ou reentregas do mesmo evento podiam criar snapshots lógicos duplicados. A pipeline passou então a construir o snapshot derivado com `Id = envelope.EventId`, tornando a identidade do snapshot estável por evento.

Esta alteração melhorou a robustez sem alterar contratos RabbitMQ, `BasicAck`, scoring, simulador ou política de InfluxDB. O objetivo foi tornar a persistência mais resiliente aos casos que uma arquitetura com RabbitMQ, reentregas e retries deve naturalmente tolerar.

---

## 8. Validação semântica `area_id` ↔ `sensor_id`

Outra frente importante foi a validação semântica entre o `area_id` do envelope e o `sensor_id` do payload. Até aqui, a pipeline já fazia validação técnica do envelope, mas ainda faltava confirmar se o sensor declarado no evento existia no plano de controlo, se estava ativo e se pertencia de facto à área indicada.

A decisão arquitetural foi manter a rejeição antes da inbox apenas para invalidez técnica, e tratar inconsistências semânticas depois da inbox, usando quarentena. Esta decisão é importante porque estes eventos são tecnicamente válidos: têm JSON legível, envelope válido e payload estruturado. O problema é que o conteúdo é incompatível com o plano de controlo. Por isso, faz sentido materializá-los para auditoria e depois terminá-los como não processáveis.

Foi criado um `IReadingSemanticValidator` e uma implementação `ReadingSemanticValidator`. Este validador consulta o plano de controlo e verifica três condições mínimas:

- o sensor existe;
- o sensor está ativo;
- o sensor pertence à área declarada no envelope.

A integração foi feita no `ReadingEventProcessingService`, depois da materialização durável na inbox e antes da chamada à `ReadingRiskPipeline`. Se a validação falhar, o evento é colocado em quarentena com motivo explícito, como `sensor_not_found`, `sensor_inactive` ou `sensor_area_mismatch`. Se a consulta à base de dados falhar por problema operacional, a exceção não é convertida em erro semântico; a política atual de retry/quarentena continua a tratar esse caso como falha operacional.

O resultado é uma pipeline mais segura. Eventos semanticamente incompatíveis deixam de contaminar `accepted_reading_log`, `risk_assessment_log`, `area_risk_snapshot_log`, projeções operacionais e InfluxDB. Ao mesmo tempo, a decisão fica auditável na inbox e na quarentena.

Esta alteração reforçou uma separação importante: o `PreventionWorker` trata transporte e contrato técnico; o `ReadingEventProcessingService` trata processamento durável, validação semântica e política de falha; a `ReadingRiskPipeline` assume que só recebe leituras processáveis.

---

## 9. Normalização, `RiskInput` e elegibilidade para risco

Depois da robustez operacional e semântica, foi preparada a fronteira interna do cálculo de risco. Esta frente foi necessária porque a evolução para índices reais não deve consistir em substituir diretamente os thresholds do `SimpleRiskScoringService` por fórmulas mais complexas. Antes disso, a pipeline precisava de deixar de alimentar o motor de risco diretamente a partir do envelope bruto.

A primeira alteração foi introduzir `NormalizedReading` e `RiskInput`. A pipeline passou a atravessar uma fronteira explícita:

`EventEnvelope<SensorReadingProducedPayload> -> NormalizedReading -> RiskInput -> IRiskScoringService -> RiskAssessment`

O `NormalizedReading` representa a leitura já validada tecnicamente e semanticamente, preservando os campos essenciais do envelope e do payload. O `RiskInput` representa o input mínimo do motor de risco atual, com informação como área, sensor, evento de origem, tipo de métrica, valor, unidade e tempo do evento.

Esta alteração foi feita sem mudança de comportamento observável. O score atual, os thresholds, a persistência PostgreSQL, a semântica da inbox/retry/quarentena, o `BasicAck`, o payload RabbitMQ e a política de InfluxDB mantiveram-se inalterados. A diferença é arquitetural: o cálculo de risco deixou de depender diretamente do envelope bruto.

A seguir, foi introduzida uma fronteira explícita de elegibilidade para risco:

`NormalizedReading -> RiskEligibilityResult -> RiskInput`

Foi criado `IRiskEligibilityService`, `RiskEligibilityService`, `RiskEligibilityResult` e `RiskEligibilityReason`. O serviço default é deliberadamente permissivo, para preservar a baseline. No entanto, a pipeline passou a ter o ponto certo para decidir se uma leitura aceite e normalizada deve, ou não, seguir para cálculo de risco.

Por fim, foi implementada a semântica interna de leitura `NotEligible`. Neste caso:

- a leitura continua a ser normalizada;
- a `accepted reading` continua a ser persistida;
- a pipeline termina com sucesso;
- não há `RiskAssessment`;
- não há `AreaRiskSnapshot`;
- não há atualização de projeções de risco;
- não há retry;
- não há quarentena.

Esta decisão é importante para o futuro, porque alguns dados podem ser válidos e úteis para auditoria, mas não elegíveis para determinado modelo de risco. Por exemplo, no futuro podem existir leituras com métrica não suportada pelo modelo ativo, unidade incompatível, falta de dados auxiliares, janela temporal incompleta ou cadência errada. A pipeline agora tem uma forma limpa de representar esse caso sem o tratar como erro operacional.

Esta frente preparou diretamente a evolução para índices reais, mas sem implementar ainda FWI, KBDI ou Haines. A decisão foi manter a baseline estável e só avançar para esses modelos depois de terminar a pesquisa sobre inputs, cadência, estado anterior, precipitação, janelas temporais e proveniência.

---

## 10. Simulator, bootstrap e plano de controlo

Nesta quinzena também houve trabalho relacionado com o simulador, o bootstrap e a ligação ao plano de controlo persistido em PostgreSQL.

No simulador, foram revistas alterações relacionadas com a construção do contexto de simulação e com a ligação aos dados configurados no plano de controlo. Componentes como o `ScenarioContextFactory`, o `SimulationContext`, o `PostgresSimulationContextSource` e o `SimulationRunner` foram analisados para aproximar a execução do simulador das áreas, sensores e cenários definidos na configuração persistida.

Esta evolução é importante porque a simulação deixa de ser apenas um produtor isolado de valores artificiais. Passa a fazer parte de uma cadeia configurável e auditável, em que uma execução concreta pode ser associada a uma área, a um cenário, a sensores ativos e a uma versão de configuração.

Durante o diagnóstico da pipeline, ficou claro que o bootstrap tem impacto direto na carga operacional do sistema. Se a seleção de células piloto ou o número de sensores ativos for reduzido no catálogo, mas sensores antigos não forem desativados no plano de controlo, a runtime pode continuar a produzir mais mensagens do que o esperado. Por isso, a sincronização entre catálogo de cenários, bootstrap, sensores ativos e runtime passou a ser um ponto importante a controlar.

Esta frente reforçou a importância de tratar a configuração persistida como parte central da baseline. O comportamento real do sistema não depende apenas dos ficheiros locais, mas também do que está efetivamente carregado em PostgreSQL.

---

## 11. Backoffice API, contratos e testes

Na Backoffice API foram revistas alterações ao serviço que consulta e projeta dados do plano de controlo, nomeadamente o `PostgresControlPlaneService`. Este serviço é responsável por transformar dados persistidos em contratos de resposta usados pelo backoffice, incluindo configurações, áreas, sensores, cenários, execuções de simulação, estados operacionais e alertas.

Também foi introduzida instrumentação no caminho de leitura da API, permitindo medir operações e tempos de consulta. Isto é útil porque a API é uma das formas principais de explorar o estado do sistema e precisa de ser observável, especialmente à medida que passa a consultar mais dados persistidos.

Durante a integração com alterações vindas do repositório remoto, foi necessário adaptar código e testes ao contrato atualizado de `AreaSummaryResponse`, que passou a incluir o identificador da área. Esta alteração obrigou a corrigir mocks e fakes usados nos testes da Backoffice API, para manter compatibilidade com o contrato atual.

Os testes passaram a ter ainda maior valor documental. Em particular, os testes de `ReadingRiskPipeline`, `ReadingEventProcessingService`, `PreventionWorker`, `InboxRetryWorker`, repositórios PostgreSQL, validação semântica, normalização, input de risco e elegibilidade passaram a funcionar como especificação executável do comportamento da pipeline.

Na validação mais recente, os comandos com restore/configuração NuGet falharam por problema de ambiente relacionado com acesso ao `NuGet.Config` global do utilizador. Para validar o código já restaurado, foram usados `dotnet build --no-restore` e `dotnet test --no-restore`, ambos com resultado positivo.

---

## 12. Manutenção do repositório, merge e validação de build

Para além das frentes técnicas e documentais, houve trabalho de manutenção do repositório. Foram feitos ajustes ao `.gitignore`, à solução `NatureProtector.sln`, a configurações comuns de build e à organização dos ficheiros que devem ou não entrar no controlo de versões.

Esta parte tornou-se especialmente importante porque foram introduzidos novos diagramas, imagens, scripts, ficheiros de documentação e outputs associados a ferramentas como Doxygen, DocFX e Structurizr. Foi necessário distinguir melhor entre fontes que devem ser versionadas e artefactos gerados que devem ser ignorados.

Também foi necessário resolver conflitos depois de atualizar o repositório com alterações remotas. O `git pull` trouxe alterações novas, incluindo uma frente de `webUI` e alterações em contratos da API. Para preservar o trabalho local, foi necessário usar `stash`, aplicar o `pull`, reaplicar as alterações locais e resolver conflitos no `.gitignore` e no `PostgresControlPlaneService`.

Depois da resolução dos conflitos, foi feita validação com `dotnet build`. O build ajudou a identificar problemas concretos: um teste da Backoffice API estava desatualizado face ao novo contrato de `AreaSummaryResponse`, e a exploração do `AppHost` ainda apresentava erros relacionados com os tipos gerados `Projects.*`.

A correção do teste foi integrada, mas o `AppHost` ficou identificado como frente exploratória ainda não estabilizada. Por isso, esta parte deve ser tratada com cuidado no commit: só deve entrar como trabalho funcional se o build ficar limpo, ou então deve ficar fora da solução até ser corrigida.

Ao longo das alterações da pipeline, foram feitas validações sucessivas. A contagem de testes aumentou com a introdução de novos testes de idempotência, validação semântica, normalização, input de risco e elegibilidade. No estado final desta sequência, a solução passou com 647 testes.

Nesta fase também foram revistos ficheiros de documentação e suporte que não alteram a lógica de negócio, mas melhoram a operabilidade do projeto. As alterações incluíram documentação de topo, documentação de arquitetura, roadmap, documentação de testes, documentação de setup e scripts de validação local. Foi ainda atualizado o `package-lock.json` do `webUI` na sequência da instalação de dependências com `npm install`, embora o frontend continue a ser tratado como candidato em configuração e não como parte final estabilizada da baseline.

---

## 13. Resultado da quinzena e próximos passos

Em síntese, esta quinzena foi marcada por uma consolidação importante da baseline técnica e documental do projeto. A apresentação ficou mais alinhada com o que já existe, a documentação passou a explicar melhor a implementação real, os diagramas ficaram mais próximos da runtime e a pipeline passou a ser analisada com base em medições concretas.

O ponto mais relevante foi a passagem de uma visão arquitetural da pipeline para uma leitura operacional e semântica. Foram adicionados logs, cronómetros e elementos de observabilidade que permitiram perceber melhor o comportamento sob carga, a ordem real de processamento, a pressão causada pelo volume de mensagens e o custo das operações de persistência e observabilidade.

Depois disso, a pipeline foi reforçada em três níveis:

1. robustez operacional, com InfluxDB desligável localmente e idempotência concorrente nos adaptadores PostgreSQL;
2. integridade semântica, com validação `area_id` ↔ `sensor_id` antes da pipeline de risco;
3. preparação para modelos futuros, com `NormalizedReading`, `RiskInput`, elegibilidade para risco e semântica de leituras `NotEligible`.

Esta evolução é importante porque permite defender a baseline com mais segurança. O projeto já não depende apenas de uma cadeia funcional feliz. Passa a demonstrar preocupação com duplicados, retries, inconsistências semânticas, separação entre estado durável e observabilidade, e preparação gradual para modelos de risco mais exigentes.

O cálculo atual de risco continua a ser uma baseline demonstrativa. A decisão mais correta neste momento é não implementar ainda FWI, KBDI ou Haines enquanto a pesquisa não estiver fechada e enquanto não estiverem definidos os inputs, a cadência, o estado anterior, a precipitação, as janelas temporais e a proveniência/versionamento dos modelos.

### Trabalho a fazer na continuação desta frente

1. Fechar commit da baseline atual, garantindo que documentação, código e testes ficam coerentes.
2. Terminar a pesquisa sobre índices de risco reais, distinguindo o que é necessário para FWI, KBDI, Haines ou outros modelos, e identificando inputs obrigatórios, escalas temporais, estado anterior e limitações.
3. Decidir qual será o primeiro índice realista a implementar, evitando uma pseudo-implementação que apenas substitua thresholds por fórmulas incompletas.
4. Definir se o primeiro modelo real será calculado por leitura, por janela temporal, por hora, por dia ou por modo híbrido.
5. Introduzir, quando fizer sentido, estado persistido de modelo, como `DailyCellState` ou `RiskModelState`, sem usar projeções operacionais como estado implícito do motor de risco.
6. Ativar o primeiro caso real de inelegibilidade, por exemplo métrica ou unidade não suportada pelo modelo ativo, mantendo a semântica já preparada: accepted reading sim, score não.
7. Rever `GetLatestByAreaAsync`, que continua aceitável para a demo de 20 ciclos mas pode tornar-se caro em simulações longas, preferindo uma query database-side ou um estado atual explícito.
8. Endurecer, se necessário, a validação semântica com coerência adicional entre `SensorNode.GridCellId` e `GridCell.AreaId`, sem entrar ainda em regras complexas de versão/configuração ativa.
9. Avaliar `alert_state`, que ainda tem risco residual de duplicação lógica de alertas abertos por área/código e pode exigir uma decisão de schema/semântica própria.
10. Medir novamente a pipeline com InfluxDB desligado e com InfluxDB ativo, comparando tempos de processamento, backlog RabbitMQ, retry/quarentena e custo das escritas temporais.
11. Garantir que catálogo de cenários, bootstrap do plano de controlo e runtime do simulador permanecem sincronizados, especialmente no número de sensores ativos.
12. Manter o `AppHost`/Aspire como frente exploratória até estar estável, evitando que contamine a baseline demonstrável.
13. Continuar a atualizar `implementation.md`, `architecture.md` e os diagramas sempre que uma fronteira semântica passar de intenção para código.

# Recapitulação Quinzenal

## Período

6 de maio a 18 de maio de 2026

## Objetivo desta entrada

Registar o trabalho de consolidação da segunda fase de pesquisa do NatureProtector, com foco na transformação da investigação dispersa numa base metodológica e técnica para orientar a V1 do subsistema de prevenção, o `Proposal`, a implementação futura, os anexos, a bibliografia, a validação técnica/runtime e a preparação de execuções reprodutíveis de cenários.

## Índice

* Resumo Estruturado
* 1. Reenquadramento da pesquisa
* 2. Cadeia metodológica da V1
* 3. Índices, variáveis e fontes
* 4. Simulação, sensores e cenários
* 5. Pipeline, normalização e elegibilidade
* 6. `RiskInput`, `DailyCellState` e score
* 7. Validação, anexos e bibliografia
* 8. Implementação, evidência runtime e orquestração de runs
* 9. Resultado da quinzena e próximos passos

## Resumo Estruturado

### O que foi feito

1. Foi consolidado o segundo momento de pesquisa do NatureProtector, passando de investigação dispersa para uma base metodológica orientada à V1.
2. Foi clarificado que a V1 não deve ser apresentada como modelo científico final de previsão de incêndios, mas como baseline metodológica, técnica, rastreável e evolutiva.
3. Foi estabilizada a decisão central de que o risco operacional não deve nascer diretamente de mensagens raw, envelopes RabbitMQ ou payloads técnicos.
4. Foi definida a cadeia conceptual da V1: `ScenarioDefinition -> DailyCellState -> TruthSnapshot -> LocalObservation -> OperationalEvent -> NormalizedReading -> RiskInput -> RiskAssessment -> AlertState -> OperationalProjection`.
5. Foi clarificada a relação entre a pesquisa anterior e a Pesquisa II: a primeira funciona como levantamento de índices, variáveis, fontes, sensores e validação; a segunda transforma esse levantamento em decisões implementáveis.
6. Foi definido o papel dos principais índices: FWI como referência meteorológica principal, KBDI como complemento de secura persistente, PIR/RCM como enquadramento português, EFFIS como referência europeia, Haines e NFDRS como estado da arte ou trabalho futuro.
7. Foi reforçado que o score NatureProtector não é equivalente a FWI, IPMA/PIR/RCM, EFFIS ou qualquer índice oficial.
8. Foram organizadas as variáveis da V1 em grupos: observacionais mínimas, meteorológicas de contexto, territoriais, operacionais de qualidade e derivadas.
9. Ficou definido que temperatura, humidade relativa e velocidade média do vento são o núcleo observacional mínimo da V1.
10. A precipitação foi tratada como contexto diário necessário para FWI, KBDI e `DailyCellState`, mesmo que não seja sensor local obrigatório no MVP.
11. Foi reforçada a separação entre verdade física, observação local e evento operacional, evitando que a simulação seja apenas geração direta de leituras.
12. Ficou definido que o simulador deve gerar primeiro `TruthSnapshot`, depois `LocalObservation` com erro observacional, e só depois `OperationalEvent`.
13. Foram clarificados os cenários A, B e C: A como dia normal plausível, B como dia severo plausível e C como versão degradada de A ou B, não como terceiro clima.
14. Foi consolidada a lógica da pipeline: validade técnica, inbox, consistência semântica, duplicação, lateness, ordering, normalização, elegibilidade e construção de `RiskInput`.
15. Foi reforçada a diferença entre persistir uma leitura para auditoria e usá-la no cálculo de risco.
16. Foi formalizada a necessidade de `ClassifierResult`, quality flags, estados por camada e regras explícitas de elegibilidade.
17. Foi definido `RiskInput` como fronteira entre pipeline e motor de prevenção.
18. Ficou claro que `RiskInput` não deve conter resultados como `base_risk`, `adjusted_score`, `risk_level`, `alert_state` ou `operational_projection`.
19. Foi introduzido `DailyCellState` como artefacto necessário para guardar memória temporal, estado antecedente, precipitação diária e suporte a índices como FWI e KBDI.
20. O score operacional foi tratado como baseline interna, explicável e versionada, não como índice científico validado.
21. Pesos, thresholds, normalizações, `FuelRisk`, janelas temporais, cooldown, retry, hold-last-valid e agregação por área foram classificados como `Candidate Parameter Set V1.0`.
22. Foi reforçada a regra de que `Blocked` não significa risco zero, mas sim ausência de condições para calcular novo score válido.
23. Foram revistos os alertas, com Warning, Alarm, histerese, persistência mínima e cooldown como práticas de engenharia, não como valores calibrados cientificamente.
24. A validação foi reformulada de forma prudente, separando validação interna, critérios metodológicos, testes definidos e validação científica externa futura.
25. Os anexos foram reorganizados para funcionarem como contrato técnico: glossário, schemas, flags, parâmetros, pseudocódigo, testes mínimos, matriz de evidência, auditoria ao repositório e checklist final.
26. Foi reforçada a matriz de evidência, distinguindo o que cada fonte suporta e o que não suporta.
27. Foram acrescentadas e revistas fontes para índices, QA/QC, RabbitMQ, idempotência, streaming, métricas estatísticas, forecast verification e validação.
28. Foram feitas várias rondas de auditoria e correção ao `Proposal.tex`, ao PDF, aos anexos e ao BibTeX.
29. Foram corrigidas regressões, especialmente no Anexo D, Anexo C, Anexo A e Anexo G.
30. Ficou como pendência importante o preenchimento de H.0 com branch, commit, ambiente, comandos de build e testes reais.
31. Foi executada uma frente técnica incremental C0-C7 para aproximar a implementação da V1 descrita documentalmente.
32. Foram introduzidos ou consolidados `OperationalEvent`, `ClassifierResult`, `ClassifierStatus`, `ClassifierSeverity`, `RiskInputStatus`, `DailyCellState`, `RiskAssessment` com `BaseRisk`/`AdjustedScore` e compatibilidade `RiskScore`.
33. Foi reforçada a elegibilidade explícita para risco, distinguindo leituras completas, parciais e bloqueadas.
34. A pipeline passou a impedir que leituras bloqueadas gerem novo `RiskAssessment`, preservando a regra `Blocked != risco zero`.
35. Foi implementada uma política interna de alertas V1 com `None`, `Warning`, `Alarm` e histerese, sem a apresentar como calibração científica.
36. A API/Backoffice passou a expor `alertState` a partir das projeções, sem recalcular risco.
37. Foi criada uma recolha de evidência runtime para C7, com queries e script PowerShell reutilizável.
38. A evidência runtime confirmou infraestrutura ativa, control plane carregado, eventos processados, risk assessments, projeções e API operacional, com classificação geral `OK com limitações`.
39. Foram identificados e separados erros históricos ou esperados, como eventos rejeitados por `invalid_operational_state`, quarentenas antigas por `retries_exhausted` e o erro histórico `EmptyProjectionMember`.
40. Foi criada uma frente O1/O1.2 para orquestração de runs, permitindo executar cenários por `run-spec.json` sem alterar manualmente CSV, scripts ou bootstrap.
41. O `Simulator.Host` passou a aceitar `RunOverrides` para `sensorCount`, `numberOfCycles`, `intervalSeconds`, `seed`, `degradationProfile` e `orchestratorCorrelationId`.
42. Foi validada uma run curta de `scenario_b` em `proenca-a-nova`, com 6 sensores, 5 ciclos, intervalo de 5 segundos e seed 12345, terminando com `Completed` e overrides como `observed_match`.
43. A orquestração passou a gravar evidência por run, incluindo `summary.md`, `run-spec.resolved.json`, logs do simulador e relatório runtime.
44. Foi realizada uma vaga adicional de testes para proteger as alterações da V1 e aumentar a cobertura da suite.
45. A cobertura consolidada passou para `97.6%` de line coverage, `90.1%` de branch coverage e `97.1%` de method coverage.
46. Os testes passaram a cobrir melhor domínio, validações, elegibilidade, `RiskInput`, `DailyCellState`, `RiskAssessment`, alertas V1, API/Backoffice, Influx configurável, inbox, contexto do simulador e orquestração de runs.
47. Ficou definido que a cobertura não deve ser aumentada artificialmente à custa de testes frágeis sobre telemetry glue, RabbitMQ real, Influx real ou branches de observabilidade sem valor funcional.
48. Foi reorganizada a documentação técnica, reduzindo documentação operacional intermédia, movendo a documentação do orquestrador para `docs/architecture/` e consolidando o plano V1 em `docs/planning/v1-implementation-map.md`.
49. Foi criado `docs/NatureProtector-V1-overview.md` como documento de entrada canónico para explicar o estado atual da V1, ligando arquitetura, runtime, contratos, pipeline, testes, evidência, limitações e roadmap.
50. Foi criada uma linha de diagramas simplificados para apresentação, separada dos diagramas técnicos detalhados, para explicar o projeto com poucos blocos e menor carga visual.
51. Foi iniciada a integração da orquestração e observabilidade runtime no website, criando uma vista `Runtime Monitor` e uma vista `Developer Runtime Control`.
52. O `Runtime Monitor` passou a expor, a partir de dados persistidos, estado da última run, inbox, attempts, rejeições, quarentenas, risk assessments, alertas, estado agregado da área, freshness e gráficos de apoio.
53. A vista `Developer Runtime Control` passou a reunir diagnósticos fixos, reset controlado de runtime e submissão de runs com parâmetros operacionais.
54. Foi criado/corrigido um launcher local para arrancar Backoffice API, Prevention Host e webUI de forma coordenada, lendo `.env`, validando PostgreSQL, detetando portas ocupadas e registando PIDs, portas e URLs em evidência local.
55. Foi corrigido o tratamento de erro quando a API não consegue ligar ao PostgreSQL, passando a devolver erro controlado em vez de `Internal Server Error` genérico.
56. Foi adicionada uma ação de reset runtime controlado no website, com dry-run e confirmação explícita, permitindo limpar runs, inbox, attempts, projections e alertas sem apagar o control plane.
57. A análise das runs no Runtime Monitor evidenciou que projeções e estado agregado podem incluir carry-forward, pelo que o score de área não deve ser interpretado como simples média dos eventos da última run.
58. Ficou identificado que a freshness por célula é necessária para distinguir estado recente, stale e expirado, limitando a influência de leituras antigas nas projeções.
59. A observação de runs do `scenario_b` mostrou diferença entre expectativa metodológica e comportamento agregado observado, reforçando que cenários severos ainda são instrumentos de demonstração técnica e não validação científica de risco extremo.
60. Foi identificado que o `scenario_c` não apresentava degradação operacional clara face ao `scenario_b`, apesar de estar definido como cenário degradado.
61. Foi realizada uma auditoria técnica adicional, focada na lógica backend/runtime de simulação e prevenção, excluindo deliberadamente UI, `.env`, segurança local, documentação e cobertura global de testes.
62. A auditoria identificou riscos de perda de eventos, rejeições não persistidas, estados `Processing` presos, validação operacional insuficiente antes do scoring, contaminação de snapshots entre runs e ambiguidade na seleção de cenários.
63. Foi implementado um hardening incremental da pipeline: recovery por lease para eventos antigos em `Processing`, rejeição segura de payload nulo/ausente, validação explícita de estados operacionais, métricas e unidades antes do scoring.
64. Foi reforçada a regra de que leituras `Dropped`, métricas não suportadas ou unidades incompatíveis não devem gerar `RiskAssessment` normal.
65. Os `RiskAssessment` e snapshots passaram a ser associados a `SimulationRunId`, evitando mistura de avaliações entre runs ou cenários diferentes na mesma área.
66. Foi corrigido um problema de migrations EF Core em que a migration que adicionava `SimulationRunId` existia com `Up/Down`, mas não era reconhecida pelo EF por falta de metadata/designer adequado.
67. A seleção de cenários no simulador foi tornada explícita, impedindo coexistência ambígua de `ScenarioId` e `ControlPlaneScenarioCode`.
68. O `scenario_c` passou a declarar degradação efetiva `missing-readings`, preservando o `scenario_b` sem degradação automática.
69. A validação runtime final confirmou que `scenario_b` e `scenario_c` completam, que o pipeline processa sem novos `db_update_failed`, que há risk assessments e projeções com `SimulationRunId`, e que o `scenario_c` produz menos leituras aceites devido a `missing-readings`.
70. Foi iniciada uma nova frente de alinhamento entre o repositório e a `PesquisaII`, usando a matriz comparativa `PesquisaII -> repo -> gap -> ação recomendada` como base de trabalho.
71. Essa frente procurou aproximar a implementação da cadeia conceptual completa: `ScenarioDefinition -> DailyCellState -> TruthSnapshot -> LocalObservation -> OperationalEvent -> NormalizedReading -> RiskInput -> RiskAssessment -> AlertState -> OperationalProjection`.
72. Foram introduzidos ou reforçados artefactos ligados à separação entre verdade física simulada, observação local e payload operacional, nomeadamente `TruthSnapshot` e `LocalObservation` no simulador.
73. Foi iniciada a materialização runtime de `DailyCellState` como estado diário por célula, com persistência, índices de contexto meteorológico/secura e ligação ao `RiskInput`.
74. O `RiskInput` foi enriquecido para transportar mais contexto operacional e metodológico, mantendo a regra de que não deve conter campos de resultado como `BaseRisk`, `AdjustedScore`, `RiskScore`, `RiskLevel` ou estado de alerta.
75. Foram introduzidas alterações relacionadas com score V1, agregação por área, provenance de FWI/KBDI, quality flags, classificadores temporais, persistência/cooldown de alertas e exposição de auditoria runtime na API/UI.
76. A validação técnica pós-implementação mostrou que build, testes e migrations podiam passar, mas que isso não era suficiente para aceitar o patch sem nova validação runtime B/C.
77. Uma nova execução runtime do `scenario_b` revelou regressão no `reading_risk_pipeline`: parte significativa dos eventos foi para quarantine por `processing_failed`.
78. A causa foi isolada na integração entre `RiskInput` e `DailyCellState`: o código exigia que `RiskInput.SensorId` coincidisse com `DailyCellState.SensorId`, tratando erradamente o estado diário como estado por sensor.
79. A análise mostrou que `DailyCellState` deve ser entendido como estado por área/célula/dia/run, podendo ser atualizado por sensores diferentes de humidade, temperatura e vento.
80. Foi definida a correção mínima: remover a validação obrigatória por `SensorId`, validar antes `AreaId`, `GridCellId`, `SimulationRunId` e dia lógico, e tratar `SensorId` apenas como proveniência do último input que atualizou o estado.
81. Foram ajustados os testes de `DailyCellState` para refletir a nova semântica: sensores diferentes devem poder atualizar o mesmo estado diário quando pertencem à mesma célula, run e dia.
82. Esta regressão reforçou uma conclusão metodológica importante: testes unitários e migrations verdes não substituem validação runtime ponta a ponta após alterações estruturais.

### Resultado principal da quinzena

O principal resultado foi transformar a pesquisa numa base metodológica muito mais madura e implementável. O NatureProtector deixou de ser descrito apenas como uma pipeline que recebe leituras e calcula um score. Passou a ser descrito como uma cadeia causal e operacional que separa cenário, estado diário, verdade física, observação, evento, normalização, elegibilidade, input de risco, avaliação, alerta e projeção.

Esta mudança torna o projeto mais defensável. Uma mensagem recebida pela pipeline não é automaticamente risco. Pode estar atrasada, duplicada, degradada, incompleta ou imprópria para cálculo. Por isso, a V1 deve calcular risco apenas a partir de dados canónicos, contextualizados, auditáveis e explicitamente elegíveis.

Também ficou mais claro o papel dos índices. O FWI orienta a componente meteorológica, o KBDI apoia a secura persistente, o PIR/RCM enquadra o contexto português e o EFFIS funciona como referência europeia. No entanto, nenhum destes produtos valida automaticamente o score interno, os pesos, os thresholds ou os fatores de confiança do NatureProtector.

Na fase final da quinzena, esta base metodológica foi parcialmente materializada em código e evidência runtime. A implementação passou a incluir camadas e contratos internos compatíveis com a V1, alertas operacionais internos, projeções expostas pela API e uma primeira camada de orquestração de runs. A validação continua a ser técnica/runtime, não científica, mas passou a ser mais reprodutível e auditável.

Também foi reforçada a base de testes automatizados. A suite passou a cobrir de forma mais completa os comportamentos de domínio, validação, elegibilidade, scoring interno, alertas, projeções, API, Influx configurável e execução de cenários. A cobertura consolidada atingiu `97.6%` de line coverage, `90.1%` de branch coverage e `97.1%` de method coverage, mantendo a opção de não perseguir `100%` artificial em componentes de observabilidade ou integrações externas difíceis de testar sem infraestrutura real.

Na fase final, a validação deixou de ser apenas demonstrativa e passou a testar riscos concretos de runtime. A auditoria backend identificou pontos onde a pipeline podia perder eventos, aceitar dados inelegíveis ou misturar runs. As correções incrementais reforçaram a durabilidade do inbox, a validação antes do scoring, o isolamento por `SimulationRunId` e a semântica efetiva do `scenario_c`.

A integração inicial no website também tornou a operação mais observável. O projeto passou a ter uma primeira superfície visual para monitorizar runtime, lançar runs, executar diagnósticos e limpar estado operacional local. Esta superfície ainda é de desenvolvimento, mas ajudou a tornar visíveis problemas como carry-forward, freshness, diferença entre risk assessments e projeções, e evidência incompleta quando a run é iniciada pela UI.

---

## 1. Reenquadramento da pesquisa

Durante este período, a pesquisa deixou de ser apenas um conjunto de fontes e passou a funcionar como base de decisão para a V1. O objetivo passou a ser transformar o conhecimento recolhido em decisões concretas para o relatório, implementação, validação e apresentação.

A pesquisa anterior manteve valor como levantamento de estado da arte. A Pesquisa II passou a ter uma função diferente: organizar esse conhecimento e decidir como ele entra no sistema. Assim, a primeira pesquisa responde ao que é relevante conhecer; a segunda responde a como esse conhecimento deve ser usado na V1.

Esta mudança ajudou a evitar dois riscos: apresentar o projeto como se já tivesse validação científica completa ou, pelo contrário, deixar a pesquisa como uma lista de possibilidades sem consequência técnica.

---

## 2. Cadeia metodológica da V1

A decisão mais importante foi estabilizar a cadeia metodológica da V1:

`ScenarioDefinition -> DailyCellState -> TruthSnapshot -> LocalObservation -> OperationalEvent -> NormalizedReading -> RiskInput -> RiskAssessment -> AlertState -> OperationalProjection`

Esta cadeia separa claramente o que pertence ao cenário, ao ambiente físico, ao sensor, ao transporte, à pipeline, ao cálculo e à projeção operacional.

A consequência principal é que o sistema deixa de ser descrito como “sensor envia leitura, sistema calcula risco”. Essa descrição era frágil porque misturava ambiente, erro de sensor, transporte, processamento e decisão. A nova cadeia permite explicar onde nasce cada dado, onde pode surgir erro, onde se decide elegibilidade e onde se calcula o risco.

Esta estrutura também tornou o `Proposal` mais implementável, porque cada conceito pode ser ligado a schemas, pseudocódigo, testes, parâmetros e roadmap.

Durante a implementação, nem toda a cadeia foi materializada de forma completa. `OperationalEvent` foi introduzido como camada interna, enquanto `TruthSnapshot` e `LocalObservation` continuaram a ser tratados como conceitos metodológicos ou planeados, não como contratos externos implementados.

---

## 3. Índices, variáveis e fontes

A revisão dos índices permitiu delimitar melhor o que entra na V1.

O FWI foi definido como a principal referência meteorológica, mas com a condição de não ser fingido se a implementação não tiver precipitação diária, continuidade temporal e estado antecedente.

O KBDI foi tratado como complemento de secura acumulada, útil para representar défice hídrico persistente, mas ainda dependente de parametrização local e validação.

O PIR/RCM foi tratado como enquadramento português e benchmark externo. A V1 não deve afirmar que calcula PIR ou RCM se não reproduzir a metodologia oficial.

O EFFIS ficou como referência europeia. Haines e NFDRS foram mantidos como estado da arte ou trabalho futuro, por exigirem dados e pressupostos fora da baseline imediata.

Também foi reorganizada a lista de variáveis. Temperatura, humidade relativa e vento ficaram como núcleo observacional mínimo. A precipitação passou a ser obrigatória como contexto diário, mesmo que não seja medida por sensor local no MVP. As variáveis territoriais, como altitude, declive, exposição, cobertura do solo e combustível dominante, ficaram ligadas ao contexto da célula.

As variáveis de qualidade também passaram a ter importância própria: flags, estado do sensor, confiança observacional, integridade operacional, proveniência, duplicação, lateness e ordering.

---

## 4. Simulação, sensores e cenários

A simulação foi reorganizada como modelo observacional em camadas. O simulador não deve gerar diretamente eventos operacionais. Deve primeiro gerar uma verdade física plausível por célula e por instante lógico.

Depois, sensores lógicos observam essa verdade física e introduzem erro: bias, drift, ruído, quantização, clipping, lag, missing, stuck values, outliers ou efeitos de instalação. Só depois a observação local deve ser transformada em evento operacional.

Esta separação permitiu distinguir erro de medição e falha de pipeline. Um stuck value pertence à camada de observação; duplicação, redelivery ou out-of-order pertencem à camada de transporte.

Os cenários também foram clarificados. O Cenário A representa um dia normal plausível. O Cenário B representa um dia severo plausível. O Cenário C não deve ser um terceiro clima; deve reutilizar a mesma verdade física de A ou B e degradar apenas observação, sensor, transporte ou pipeline.

Isto permite comparar cenário limpo e degradado sem confundir falha operacional com maior perigo real.

No final da quinzena, a execução de cenários foi tornada mais controlável através de um orquestrador local baseado em `run-spec.json`. Esta camada permite indicar área, cenário, número de sensores, ciclos, intervalo, seed e recolha de evidência, sem alterar manualmente CSV, scripts de bootstrap ou parâmetros dispersos.

A análise das execuções mostrou, no entanto, que a semântica dos cenários precisava de ficar mais explícita. O `scenario_b`, embora represente um contexto severo, não deve ser apresentado como prova de risco extremo. A sua leitura depende da seleção de sensores, do estado projetado, do carry-forward e dos thresholds candidatos.

Também foi identificado que o `scenario_c` se comportava de forma demasiado próxima do `scenario_b` quando era executado com `degradationProfile=none` ou quando a definição persistida do control plane não refletia degradação efetiva. Esta observação reforçou a decisão metodológica inicial: C não deve ser um terceiro clima, mas uma versão operacionalmente degradada de uma execução comparável.

Na fase final, a semântica dos cenários foi reforçada também em runtime. A seleção por control plane passou a exigir uma escolha inequívoca entre `ScenarioId` e `ControlPlaneScenarioCode`, evitando que um identificador por omissão mascarasse o cenário pretendido. O `scenario_c` passou a declarar explicitamente `missing-readings` como perfil de degradação, mantendo o `scenario_b` sem degradação automática. A validação runtime posterior confirmou que o `scenario_c` produziu menos leituras aceites do que o `scenario_b` em execução comparável.

---

## 5. Pipeline, normalização e elegibilidade

A pipeline passou a ser descrita como uma sequência de decisões, não apenas como infraestrutura.

A ordem consolidada foi:

`receção -> validade técnica -> inbox durável -> consistência semântica -> duplicação -> lateness -> ordering -> normalização -> elegibilidade -> RiskInput -> RiskAssessment`

Esta ordem impede que o cálculo de risco aconteça antes de existir uma decisão clara sobre validade, qualidade e elegibilidade.

Também foi reforçada a distinção entre persistir e usar. Uma leitura pode ser guardada para auditoria e, mesmo assim, não entrar no cálculo de risco. Isto é essencial para duplicados, leituras stale, payloads incompletos, falhas semânticas ou eventos fora da janela operacional.

Foram formalizados classificadores mínimos e a ideia de `ClassifierResult`, para que cada decisão da pipeline tenha nome, resultado, flags, estado, próxima ação e razão auditável.

A implementação passou a refletir esta separação com estados de elegibilidade mais explícitos. Leituras bloqueadas deixam de produzir assessment numérico, evitando a interpretação errada de `Blocked` como risco zero. A evidência runtime confirmou eventos processados, tentativas de processamento sem erro recente, risk assessments e projeções operacionais.

Após auditoria backend, esta fronteira foi reforçada em código. A pipeline passou a recuperar eventos antigos presos em `Processing` através de uma política explícita de lease/timeout, evitando que mensagens já materializadas no inbox fiquem indefinidamente sem novo processamento após cancelamento ou crash.

Também foi acrescentada validação defensiva para payload nulo ou ausente, garantindo rejeição persistida antes do `BasicAck` quando tecnicamente possível. Na elegibilidade, leituras com estado operacional `Dropped`, métricas não suportadas, unidades incompatíveis ou enums indefinidos passaram a ser bloqueadas ou rejeitadas antes de qualquer cálculo de risco. Esta alteração protege a regra de que dados tecnicamente recebidos não são automaticamente dados elegíveis para scoring.

A análise runtime também mostrou que persistir e projetar estado não é equivalente a recalcular tudo a partir da última run. O estado operacional pode transportar carry-forward, o que é útil para continuidade, mas exige regras explícitas de freshness, expiração e peso temporal para evitar que leituras antigas dominem a interpretação atual.

---

## 6. `RiskInput`, `DailyCellState` e score

O `RiskInput` foi definido como a entrada legítima do motor de risco. Ele só deve nascer depois de validação, classificação, normalização e elegibilidade.

Também ficou definido o que ele não deve conter: `base_risk`, `adjusted_score`, `risk_level`, `alert_state` ou `operational_projection`. Esses campos pertencem ao resultado ou à projeção, não ao input.

O `DailyCellState` foi introduzido como estado diário por célula. A sua função é suportar índices com memória temporal, especialmente FWI e KBDI, guardando precipitação diária, temperatura máxima, estado antecedente, parâmetros e proveniência.

O score operacional foi mantido como baseline interna. A fórmula com componentes meteorológica, secura e território, os fatores `C/I`, os thresholds de alerta, as janelas temporais e a agregação por área foram tratados como `Candidate Parameter Set V1.0`.

A regra importante foi manter honestidade metodológica: estes valores tornam a V1 executável e testável, mas não são calibração científica final.

Também foi protegida a semântica de `Blocked`: não significa risco zero. Significa que não existem condições para calcular novo score válido.

A implementação passou a distinguir `BaseRisk`, `AdjustedScore` e compatibilidade `RiskScore`, mantendo a separação entre input, cálculo e resultado. Esta alteração reforçou a legibilidade da fronteira entre pipeline e scoring.

Numa fase posterior, a implementação tentou aproximar mais diretamente o código da `PesquisaII`, enriquecendo `RiskInput`, persistindo `DailyCellState` e introduzindo contexto adicional para score, índices e agregação. Esta frente foi útil, mas também revelou um problema conceptual na primeira integração runtime.

Inicialmente, `DailyCellState` foi tratado no código como se fosse estado por sensor, através de uma validação que exigia que `RiskInput.SensorId` coincidisse com `DailyCellState.SensorId`. A validação runtime mostrou que esta regra era incorreta: numa mesma célula e no mesmo dia, o estado diário deve poder acumular leituras de sensores diferentes, por exemplo humidade, temperatura e vento.

A regra foi então corrigida conceptualmente: `DailyCellState` deve ser identificado por área, célula, dia lógico e, quando aplicável, `SimulationRunId` e versão de configuração. O `SensorId`, se mantido no modelo, deve representar proveniência ou último sensor fonte, não a identidade do estado diário. Esta correção preserva a lógica metodológica da `PesquisaII`, onde o estado diário pertence à célula e não a um sensor isolado.
---

## 7. Validação, anexos e bibliografia

A validação foi reformulada com linguagem mais cautelosa. O documento passou a distinguir validação interna, critérios de avaliação, testes executáveis e validação científica externa futura.

A V1 pode demonstrar plausibilidade, rastreabilidade, determinismo, robustez da pipeline, classificação, elegibilidade, score completo/parcial/bloqueado e comportamento dos alertas. Não deve afirmar previsão real de incêndios, calibração científica final, equivalência com produtos oficiais ou generalização multiárea.

Os anexos foram reforçados para funcionarem como parte normativa do documento:

* Anexo A: glossário e siglas;
* Anexo B: schemas mínimos;
* Anexo C: flags, classificadores e estados;
* Anexo D: parâmetros V1;
* Anexo E: pseudocódigo;
* Anexo F: testes mínimos;
* Anexo G: matriz de evidência;
* Anexo H: auditoria técnica ao repositório;
* Anexo I: backlog e checklist.

A bibliografia também foi trabalhada para evitar fontes mal usadas. A regra principal ficou clara: uma fonte pode sustentar um conceito, método, variável ou prática, mas não valida automaticamente os valores internos do NatureProtector.

Foram acrescentadas fontes para RabbitMQ, idempotência, streaming, QA/QC, métricas estatísticas e forecast verification. Também foram corrigidas pendências de BibTeX, citações indefinidas e entradas não citadas.

A validação técnica foi complementada com recolha runtime. Esta evidência demonstrou funcionamento operacional da baseline, mas continuou a ser tratada como validação técnica e não como validação científica do modelo.

A validação automatizada também foi reforçada através de várias rondas de testes. Foram cobertos casos de domínio, limites, invariantes, caminhos negativos, indisponibilidade da API, validações de configuração, política de alertas, classificadores de falha, inbox em memória, parsing de configuração de InfluxDB e execução controlada do simulador. Esta melhoria aumentou a confiança técnica na implementação, sem ser apresentada como validação científica do modelo de risco.

A organização documental também foi revista para facilitar leitura e manutenção. Foi criado `docs/NatureProtector-V1-overview.md` como documento canónico de entrada, capaz de explicar o estado atual da V1 sem obrigar à leitura inicial de todos os documentos técnicos. Em paralelo, a documentação de implementação foi simplificada, o plano consolidado passou para `docs/planning/v1-implementation-map.md`, e a documentação do orquestrador passou a estar enquadrada em `docs/architecture/`.

Foi ainda criada uma linha de diagramas simplificados para apresentação, separada dos diagramas técnicos detalhados. Esta decisão surgiu da necessidade de explicar o projeto com poucos blocos e menor carga visual, mantendo os diagramas antigos como material técnico de suporte. Foram produzidos diagramas de visão geral, fluxo runtime, pipeline de risco, alertas/API e orquestração de runs.

---

## 8. Implementação, evidência runtime e orquestração de runs

Para além da consolidação metodológica, foi realizada uma frente técnica incremental sobre a V1. Esta frente começou por vocabulário, contratos, catálogo de eventos e fronteiras conceptuais, e avançou para flags, classificadores, elegibilidade, input de risco, assessment, alertas, projeções e API.

Foram criados e atualizados documentos em `docs/contracts/`, `docs/implementation/` e `docs/evidence/`, com o objetivo de manter a implementação rastreável e distinguível da intenção documental. Também foram criados scripts de evidência em `scripts/evidence/`.

A evidência runtime C7 confirmou a presença dos schemas `control`, `pipeline` e `projection`, o carregamento do control plane, a existência da área `proenca-a-nova`, sensores, cenários, simulation runs, eventos na inbox, attempts de processamento, risk assessments, snapshots/projeções e API operacional. Foram identificados erros históricos ou esperados, sem os tratar automaticamente como falhas atuais da implementação.

A política de alertas V1 foi implementada como política interna com `Warning`, `Alarm` e histerese. Esta política foi ligada às projeções e exposta pela API como estado operacional, sem recalcular risco no Backoffice.

Na parte final, foi criada a primeira versão do Scenario Run Orchestrator. A versão O1.1 introduziu `run-spec.json`, exemplos de execução e `run-scenario.ps1`. A versão O1.2 adicionou suporte real no `Simulator.Host` para `RunOverrides`, permitindo controlar `sensorCount`, `numberOfCycles`, `intervalSeconds`, `seed`, `degradationProfile` e `orchestratorCorrelationId`.

Foi validada uma run curta do `scenario_b` para `proenca-a-nova`, com 6 sensores, 5 ciclos, intervalo de 5 segundos e seed 12345. A run terminou com `Completed`, os overrides ficaram como `observed_match`, o `MetadataJson` passou a registar valores pedidos e resolvidos, e a evidência foi gravada numa pasta própria da run. Esta abordagem prepara o caminho para futura orquestração pelo Backoffice/site, onde será possível lançar runs, acompanhar estado e consultar dashboards/evidência a partir do mesmo ponto.

Em paralelo, foi feita uma ronda de reforço da suite de testes. Foram acrescentados testes para `Area`, `GridCell`, `SensorDeployment`, `ClassifierResult`, `RiskEligibilityResult`, `RiskInput`, `DailyCellState`, `RiskAssessment`, `RiskCell`, `SimulationRun`, `DefaultProcessingFailureClassifier`, `V1AlertPolicy`, `ExpectedUniqueViolationDetector`, `PreventionHostOptionsValidator`, controllers da Backoffice API, `SafeInfluxWriteService`, `PostgresSimulationContextSource`, `SimulationRunner`, `InMemoryReadingEventInbox` e `InfluxDbSettingsLoader`.

A cobertura consolidada passou para `97.6%` de line coverage, `90.1%` de branch coverage e `97.1%` de method coverage. A melhoria foi feita apenas com testes, sem alterar contratos RabbitMQ, scoring, alert policy, schemas ou comportamento de produção. Ficaram assumidas como limites saudáveis algumas zonas de baixa cobertura residual, sobretudo `PostgresBootstrapTelemetry`, branches de `ActivitySource`, integração RabbitMQ real e escrita Influx real, por serem áreas de observabilidade ou integração que exigiriam testes frágeis, infraestrutura externa ou refactor específico.

### Runtime Monitor e Developer Runtime Control

Depois do merge com os avanços do website, foi iniciada uma frente de observabilidade e controlo runtime no frontend. O `Runtime Monitor` passou a apresentar estado da última run, inbox, tentativas de processamento, rejeições, quarentenas, risk assessments, alertas, risco agregado da área, freshness e gráficos de apoio.

A vista `Developer Runtime Control` passou a concentrar diagnósticos fixos, submissão de runs com parâmetros operacionais, reset runtime e acesso ao Runtime Monitor. Esta vista foi pensada como ferramenta local/development, não como interface operacional final, e ajudou a tornar visíveis diferenças entre eventos recebidos, assessments persistidos, projeções atualizadas e alertas ativos.

### Launcher local e controlo de ambiente

Foi também corrigido o arranque local coordenado. O script `start-local-runtime.ps1` passou a ler `.env`, usar a porta efetiva do PostgreSQL, validar a disponibilidade da base de dados, detetar portas ocupadas, suportar `-ForceRestart`, arrancar a webUI com `--strictPort` e gravar um resumo com PIDs, portas, URLs e destino PostgreSQL efetivo.

Esta alteração resolveu problemas de `Internal Server Error` causados por processos antigos, portas ocupadas ou API ligada ao PostgreSQL errado. A API passou ainda a devolver erro controlado quando não consegue ligar ao PostgreSQL, permitindo que o frontend mostre uma mensagem útil em vez de uma falha genérica.

### Reset runtime controlado

Foi adicionada uma ação de reset runtime controlado no website. A ação exige dry-run e confirmação textual explícita, limpa apenas tabelas runtime/pipeline/projection e preserva o control plane. Depois do reset, o Runtime Monitor passou a mostrar corretamente ausência de run, inbox vazia, attempts vazios, sem risk assessments, sem projeção de área e freshness nula.

Esta funcionalidade tornou possível repetir demonstrações a partir de um estado limpo, reduzindo a influência de runs antigas. Ao mesmo tempo, a análise mostrou que o carry-forward continua a ser uma característica importante das projeções, exigindo regras futuras de freshness, stale/expired e limites temporais.

### Auditoria backend e hardening runtime pós-C7

Depois da primeira validação runtime, foi feita uma auditoria lógica focada no backend de simulação e prevenção. Foram excluídos desta passagem os temas de `.env`, segurança local, UI, drift documental e cobertura global, para concentrar a análise em riscos de runtime.

A auditoria identificou quatro classes principais de problemas: eventos que poderiam ficar presos em `Processing`, rejeições de payload inválido que poderiam não ficar persistidas, dados operacionais não elegíveis que poderiam chegar ao scoring e snapshots agregados sem fronteira clara por `SimulationRunId`.

A correção foi feita em patches incrementais. Primeiro, foi introduzida uma política de lease para recuperar eventos antigos em `Processing`, mantendo o `BasicAck` na posição anterior para eventos válidos. Em seguida, foram reforçadas as validações antes do scoring, impedindo que `Dropped`, métricas não suportadas, unidades incompatíveis ou enums inválidos gerassem `RiskAssessment` normal.

Depois, os `RiskAssessment`, snapshots e projeções agregadas passaram a transportar `SimulationRunId`, permitindo separar runs diferentes da mesma área. Esta alteração exigiu uma migration EF Core para adicionar `SimulationRunId` a `projection.risk_assessment_log`, com índices e foreign key para `control.simulation_runs`.

Durante a validação, foi detetado um problema na própria migration: o ficheiro com o `Up/Down` real existia, mas não era reconhecido pelo EF Core por falta de metadata/designer adequado. Uma migration duplicada criada acidentalmente gerou erro de compilação por duplicação de `Up/Down`. O estado das migrations foi corrigido, mantendo uma única migration válida, reconhecida pelo EF, aplicada à base de dados e confirmada por queries PostgreSQL.

Após estas correções, foi feita nova validação runtime. O pipeline deixou de produzir `db_update_failed`, as tentativas de processamento passaram a terminar com sucesso, não surgiram rejeições nem quarentenas novas, e os risk assessments passaram a ser persistidos com `SimulationRunId`.

| Validação | Resultado |
|---|---|
| `scenario_b` | `Completed` |
| `scenario_c` | `Completed` |
| `scenario_b`, eventos esperados/leituras aceites | 30 / 29 |
| `scenario_c`, eventos esperados/leituras aceites | 30 / 20 |
| `scenario_c` degradation profile | `missing-readings` |
| Rejected | 0 |
| Quarantined | 0 |
| Processing attempts recentes | `Succeeded` |
| Risk assessments | persistidos com `SimulationRunId` |
| Area operational state | atualizado com risco `High` |

Esta validação continua a ser técnica/runtime. Demonstra coerência operacional e reprodutibilidade básica da pipeline, mas não constitui validação científica do score de risco.

### Runs pelo website e evidência

A execução de runs a partir do website foi iniciada e demonstrou que o site consegue submeter runs, acompanhar o estado no Runtime Monitor e validar erros como `sensorCount` superior aos sensores ativos. Foram também identificadas lacunas de feedback e auditabilidade: a confirmação após `Start Run` ainda precisava de ser mais explícita e a recolha de evidência com `collectEvidence=true` devia produzir mais do que um `request.json`.

Ficou definido que as runs lançadas pela UI devem gerar evidência completa, incluindo request, response, summary, diagnósticos before/after e relatório pós-run. Esta frente prepara a passagem de uma orquestração local por script para uma orquestração mais integrada no Backoffice/site.

### Alinhamento adicional com a PesquisaII e regressão encontrada em runtime

Depois da validação B/C inicial, foi iniciada uma nova frente para reduzir os gaps identificados pela matriz comparativa entre `PesquisaII` e repositório. Esta frente incluiu alterações mais amplas do que os patches anteriores: introdução de `TruthSnapshot` e `LocalObservation`, persistência de `DailyCellState`, enriquecimento de `RiskInput`, ajustes no score V1, agregação por área, provenance de FWI/KBDI, flags/classificadores, alertas e exposição de auditoria runtime na API/UI.

A primeira validação técnica indicou que a solução compilava, os testes passavam e as migrations estavam reconhecidas/aplicadas. No entanto, uma nova execução runtime do `scenario_b` mostrou uma regressão crítica: apenas parte dos eventos foi processada com sucesso, enquanto vários eventos de temperatura e vento foram colocados em quarantine após retries.

A análise dos `processing_attempts` identificou a causa:

`RiskInput SensorId does not match DailyCellState SensorId.`

O erro revelou que `DailyCellState` tinha sido integrado com uma premissa incorreta. O estado diário estava a ser tratado como se pertencesse a um sensor específico, quando metodologicamente deve pertencer à célula/dia/run. Como as primeiras leituras de humidade criavam o estado diário, as leituras posteriores de temperatura e vento da mesma célula falhavam ao tentar atualizar esse estado.

A correção definida foi mínima e sem alteração do contrato externo: remover a validação obrigatória por `SensorId`, manter validações por área, célula, run e dia, e ajustar os testes para garantir que sensores diferentes podem contribuir para o mesmo `DailyCellState`.

Esta ocorrência reforçou que alterações estruturais devem ser sempre validadas com runs B/C reais depois dos testes automatizados, porque a regressão só ficou visível no comportamento runtime completo da pipeline.

---

## 9. Resultado da quinzena e próximos passos

Em síntese, esta quinzena consolidou a Pesquisa II como base metodológica e técnica do NatureProtector V1. O documento passou a explicar melhor o que o sistema faz, o que ainda não faz, que decisões estão fechadas, que parâmetros são apenas candidatos e que validação ainda falta.

O resultado mais importante foi a definição de uma cadeia defensável entre cenário, verdade física, observação, evento, leitura normalizada, input de risco, avaliação, alerta e projeção. Esta cadeia permite defender que o sistema não calcula risco a partir de mensagens raw, mas sim a partir de dados canónicos, elegíveis, contextualizados e auditáveis.

A fase final da quinzena também transformou parte desta visão em execução técnica: a pipeline ganhou fronteiras mais explícitas, os alertas passaram a ter política interna testável, a API passou a expor estado operacional sem recalcular risco, foi criada uma primeira camada de orquestração de runs com evidência por execução, e a suite de testes foi reforçada até uma cobertura consolidada de `97.6%` em linhas, `90.1%` em branches e `97.1%` em métodos.

Na última parte da frente, o trabalho avançou para observabilidade web e hardening runtime. O Runtime Monitor e o Developer Runtime Control tornaram visíveis problemas que antes estavam escondidos, como carry-forward, freshness, diferença entre assessments e projeções, cenário C sem degradação observável e evidência incompleta em runs iniciadas pela UI. A auditoria backend e os patches posteriores reforçaram a durabilidade do inbox, a validação pre-scoring, o isolamento por `SimulationRunId` e a degradação efetiva do `scenario_c` através de `missing-readings`.

O documento ficou mais forte, mas ainda há trabalho antes de considerar tudo fechado. A principal pendência documental continua a ser manter `Proposal.tex`, PDF, anexos, documentação técnica e evidência sincronizados com o estado real do código.

A frente mais recente mostrou progresso real no alinhamento com a `PesquisaII`, mas também evidenciou o risco de introduzir várias alterações estruturais de uma só vez. A regressão em `DailyCellState` foi útil porque clarificou uma fronteira conceptual importante: o estado diário não pertence ao sensor, pertence à célula. Assim, a validação runtime passou a ter um papel ainda mais central antes de aceitar a implementação como fechada.

### Trabalho a fazer na continuação desta frente

1. Fazer uma auditoria adversarial final ao documento consolidado.
2. Preencher ou atualizar H.0 com branch, commit, ambiente, SDK, comandos de build, comandos de teste, runtime evidence e estado da working tree.
3. Evitar novas reescritas grandes; a partir daqui, fazer apenas microcorreções controladas.
4. Rever visualmente tabelas densas, sobretudo Anexo G e anexos técnicos.
5. Confirmar que `Proposal.tex`, PDF e BibTeX estão sincronizados.
6. Confirmar que não existem citações indefinidas, referências indefinidas, entradas não citadas ou marcas internas.
7. Garantir que `RiskInput` continua sem campos de resultado.
8. Garantir que `Blocked` continua a significar ausência de score válido, não risco zero.
9. Confirmar que a documentação técnica reflete o estado real de C0-C7 e O1/O1.2.
10. Fazer limpeza pré-commit de ficheiros acidentais, evidência duplicada e outputs fora da pasta esperada.
11. Correr `dotnet test` final, gerar o relatório de coverage consolidado e guardar a evidência relevante.
12. Decidir se a evidência runtime final deve ser versionada na totalidade ou reduzida aos ficheiros essenciais.
13. Separar commits, se possível, entre código/testes e documentação/evidência.
14. Continuar a evolução do orquestrador apenas depois de fechar a frente atual, preparando futura integração no Backoffice/API.
15. Só depois avançar para FWI/KBDI, alertas finais com política operacional mais completa, agregação por área calibrada e validação externa.
16. Manter a política de coverage saudável: cobrir comportamento funcional e caminhos críticos, mas não perseguir `100%` artificial em telemetry glue, branches de observabilidade ou integrações externas sem infraestrutura própria.
17. Guardar evidência da validação runtime pós-hardening, incluindo migrations aplicadas, comparação B vs C, risk assessments por `SimulationRunId`, processing attempts com sucesso e screenshots do Runtime Monitor.
18. Rever a UI/orquestração para garantir que mostra claramente o cenário efetivo, o `degradationProfile` resolvido, missing events e diferença entre score final e degradação observacional.
19. Confirmar que a documentação técnica reflete as alterações de hardening: lease/recovery de `Processing`, validação pre-scoring, `SimulationRunId` em assessments/snapshots e `scenario_c` com `missing-readings`.
20. Fechar a evidência automática de runs iniciadas pela UI, garantindo que `collectEvidence=true` produz artefactos suficientes para auditoria.
21. Estabilizar regras de freshness, stale/expired e carry-forward, para limitar a influência de leituras antigas no estado operacional.
22. Separar, se possível, commits de hardening runtime, migrations, cenários/manifests, UI/runtime monitor e evidência.
23. Só depois avançar para testes UI, drift documentação-código, integração mais completa no Backoffice/API e validação externa.


# Recapitulação Quinzenal

## Período

19 de maio a 2 de junho de 2026

## Objetivo desta entrada

Registar o trabalho realizado na reorganização e melhoria do website do NatureProtector, na preparação dos diagramas e da apresentação, na estabilização da infraestrutura local necessária para a demonstração, e na consolidação técnica da V1 enquanto pipeline operacional de risco.

O foco foi tornar o sistema mais claro para apresentação, mais alinhado com o fluxo real da V1, mais reprodutível para desenvolvimento, validação, onboarding e demonstração, e mais consistente com a pesquisa realizada sobre cálculo de risco, componentes meteorológicas, secura, território, qualidade operacional e comparação com índices externos.

Nesta quinzena foi também iniciada uma evolução metodológica natural após a base funcional da V1: a integração e exposição dos índices FWI e KBDI, a criação de classes qualitativas para esses índices, a introdução de um proxy contextual português candidato, e a correção de problemas de persistência e interpretação encontrados durante a validação runtime.

## Resumo Estruturado

### O que foi feito

1. Foi analisado o estado atual do website e identificou-se que já existiam várias funcionalidades úteis, mas distribuídas de forma pouco clara para apresentação.

2. A interface foi reenquadrada para deixar de ser apenas um conjunto de dashboards e passar a funcionar como uma superfície de demonstração e explicação do sistema.

3. Foi definido um novo mapa de navegação para o website, organizado em cinco grandes áreas:

   * `Monitoring`;
   * `Scenario Lab`;
   * `Flow Explorer`;
   * `Evidence & Comparison`;
   * `Model & Provenance`.

4. A página inicial foi revista para apresentar melhor o projeto, incluindo uma descrição geral, informação sobre os participantes e seleção da área de monitorização.

5. A área `Monitoring` passou a concentrar a informação operacional da área selecionada, incluindo visão geral, mapa, células, sensores, risco e alertas.

6. A área `Scenario Lab` passou a reunir a execução de cenários, definição dos cenários, informação da última run e controlo/reset do estado runtime.

7. A área `Evidence & Comparison` passou a dar maior destaque à auditoria das runs e à comparação entre `scenario_b` e `scenario_c`.

8. A comparação B/C foi promovida a uma vista própria, tornando mais fácil demonstrar o efeito da degradação operacional através de leituras em falta no `scenario_c`.

9. A área `Flow Explorer` foi criada para ajudar a explicar o fluxo interno do sistema, desde a run até ao inbox, attempts, risco, projeções, alertas e API/UI.

10. A área `Model & Provenance` foi criada para ligar a interface ao modelo conceptual do projeto, à proveniência dos dados e à relação entre conceitos e implementação.

11. Foi identificada a necessidade de a UI representar melhor o que acontece no backend, mas sem sobrecarregar o ecrã com todos os detalhes técnicos.

12. Ficou definido que a informação técnica deve ser apresentada por camadas: primeiro a visão de demonstração, depois o detalhe técnico quando necessário.

13. Foram identificados problemas visuais e funcionais após a reorganização inicial, nomeadamente dupla navbar, duplicação do botão light/dark, dashboards embebidos incorretamente e dashboards por célula sem `sensor_id`.

14. Foi definido que a interface não deve mostrar painéis Grafana partidos nem abrir dashboards com parâmetros em falta.

15. Foi corrigida a regressão dos dashboards por célula, causada por desalinhamento entre o formato real recebido da API (`item1`/`item2`) e o formato esperado pela UI (`id`/`type`).

16. Os dashboards por célula passaram a resolver corretamente sensores de temperatura, humidade e vento, usando os identificadores persistidos necessários para o Grafana e mostrando nomes legíveis na interface.

17. Foi analisada a forma de embutir Grafana na UI e decidiu-se usar URLs `d-solo` com `panelId`, para apresentar apenas o painel necessário e não a interface completa do Grafana.

18. Foi reforçada a necessidade de distinguir dados reais, dados persistidos, dados não expostos e informação meramente explicativa.

19. A tab `Run Timings` foi inicialmente criada de forma simples, mas percebeu-se que o frontend não tinha dados suficientes para representar tempos reais de processamento.

20. Para resolver essa limitação, foi criado um endpoint backend read-only para expor timings persistidos por run.

21. O novo endpoint passou a expor dados como duração da run, primeiro evento recebido, primeira tentativa de processamento, primeiro risk assessment, primeiro alerta e duração de attempts.

22. A UI passou a consumir esses dados na área `Evidence & Comparison > Run Timings`.

23. Ficou documentada a limitação de que os stopwatches presentes nos logs ainda não estão estruturalmente associados a `SimulationRunId`, pelo que ainda não podem ser usados diretamente pela UI.

24. Foi clarificado que o website deve preparar-se para RBAC no futuro, mas sem fingir que esconder tabs no frontend é segurança real.

25. Ficou definido que permissões futuras devem distinguir perfis como visualização, análise, operação, desenvolvimento e administração.

26. Foi reforçado que ações sensíveis, como reset runtime e diagnósticos avançados, devem futuramente depender de permissões mais elevadas.

27. Foram identificados processos locais antigos a bloquear builds e a dificultar a validação da versão atualizada do site.

28. Ficou definido que, para ver a versão atualizada do website, não é necessário mandar Docker abaixo quando este apenas corre infraestrutura; é necessário reiniciar os processos locais da aplicação.

29. Foram revistos os diagramas técnicos existentes e percebeu-se que vários estavam demasiado densos para apresentação, apesar de serem úteis como documentação técnica.

30. Foram criadas duas linhas de diagramas: uma linha técnica canónica para documentação e uma linha simplificada para apresentação.

31. Foram criados diagramas simplificados para apresentação, com poucos blocos, setas explícitas e linguagem mais próxima da narrativa oral da demo.

32. Foram criados diagramas técnicos canónicos adicionais para representar a cadeia runtime V1 e o orquestrador de runs.

33. Depois de feedback dos professores, foi decidido simplificar ainda mais os diagramas de apresentação, mantendo a ideia de três blocos principais, mas substituindo caixas técnicas excessivas por tópicos do que acontece dentro de cada bloco.

34. Foi revisto o plano da apresentação para 15 minutos, com cerca de 5 minutos reservados para demonstração.

35. Foi decidido que a apresentação deve reduzir o número de slides, evitar excesso de texto, manter o template original e usar frases curtas orientadas à ação.

36. Foi preparada uma estratégia de apresentação com introdução, apresentação do projeto, solução geral, pesquisa/metodologia, diagramas internos essenciais e demonstração.

37. Foi identificada a necessidade de apresentar tanto a primeira como a segunda fase de pesquisa, ligando a pesquisa ao que ficou implementado na V1.

38. Foi analisado o problema de reprodutibilidade da infraestrutura local, especialmente a criação da store temporal `np_telemetry` no InfluxDB.

39. Foi confirmado que o container `np-influxdb-init` não criava a database `np_telemetry`; apenas tratava permissões de volume.

40. Foi criado um script idempotente para garantir a existência da database `np_telemetry` no InfluxDB.

41. Foi criado um script de validação da baseline local para confirmar Docker, PostgreSQL, RabbitMQ, InfluxDB, Grafana e dados mínimos de controlo.

42. Foi criado um script destrutivo controlado, `reset-local-infra.ps1`, para remover volumes e recriar a infraestrutura apenas com confirmação textual explícita.

43. O teste destrutivo revelou que, após remover volumes, o InfluxDB novo não reconhecia automaticamente o token definido no `.env`.

44. Para resolver essa falha, foi criado o script `Ensure-InfluxAdminTokenFile.ps1`, que materializa o `INFLUXDB_TOKEN` do `.env` num ficheiro local não versionado usado pelo InfluxDB 3 no arranque.

45. O `docker-compose.yml` foi ajustado para montar esse ficheiro no container `np-influxdb` e configurar `INFLUXDB3_ADMIN_TOKEN_FILE`.

46. Os scripts `up.ps1` e `reset-local-infra.ps1` passaram a preparar o ficheiro de token antes de arrancar o InfluxDB.

47. Foi validado que, após reset completo dos volumes, a infraestrutura volta a subir, o InfluxDB aceita o token local, a database `np_telemetry` é criada automaticamente, o PostgreSQL é bootstrapado e a baseline passa sem falhas.

48. Foi revisto o processo de setup local para distinguir melhor diagnóstico, instalação, infraestrutura, runtime e validação.

49. Foi reforçada a decisão de que `up.ps1` não deve instalar dependências, não deve apagar volumes e não deve arrancar API/webUI; deve apenas preparar e levantar a infraestrutura local.

50. Foi criado ou ajustado `Test-LocalPrerequisites.ps1` para funcionar como diagnóstico read-only de dependências locais, incluindo Docker, Docker Compose, .NET SDK, Node.js, npm, PowerShell, `.env` e portas relevantes.

51. Foi criado `Install-LocalPrerequisites.ps1` como script opt-in para sugerir ou instalar dependências em falta através de `winget`, sem executar instalações por defeito.

52. Foi criado `Setup-LocalEnvironment.ps1` como script de onboarding guiado, responsável por verificar pré-requisitos, preparar `.env`, chamar `up.ps1`, validar a baseline e, opcionalmente, arrancar runtime e abrir o browser.

53. Foi definido que a instalação de dependências deve ser sempre explícita e autorizada, porque pode exigir privilégios administrativos, alteração de `PATH`, reinício da shell ou abertura manual do Docker Desktop.

54. Foi atualizado o `local-baseline-setup.md` para documentar o processo completo: pré-requisitos, diagnóstico, instalação opt-in, setup guiado, arranque normal, validação, InfluxDB, token local, reset destrutivo e troubleshooting.

55. Durante a validação do setup guiado, foi identificado um problema no wrapper que chamava scripts PowerShell e interpretava output de `stderr` como erro, especialmente quando `docker compose` escrevia mensagens informativas sobre containers.

56. A função de invocação de scripts foi ajustada para capturar `stdout` e `stderr` de forma controlada, tratando `stderr` como erro apenas quando o processo termina com exit code diferente de zero.

57. Foi detetada uma incompatibilidade adicional com Windows PowerShell 5.1, relacionada com `ProcessStartInfo.ArgumentList`, e a chamada aos scripts foi adaptada para usar `Arguments` com citação manual, mantendo compatibilidade com o ambiente usado.

58. Foram executadas validações de parse dos scripts, validação de pré-requisitos, validação do instalador em modo `WhatIf`, setup guiado sem runtime, baseline de infraestrutura e build/testes do projeto.

59. Foi consolidada a fórmula operacional da V1 para deixar explícito que o NatureProtector não calcula risco diretamente a partir de raw messages, mas a partir de uma cadeia com validação, normalização, elegibilidade, `RiskInput`, scoring, `RiskAssessment` e projeções.

60. A fórmula NatureProtector passou a ser exposta de forma mais clara na UI, incluindo `BaseRisk`, `AdjustedScore`, componentes `M/D/T`, subcomponentes territoriais `H/F/G`, confiança/integridade `C/I`, driver dominante, estado de cálculo e limitações.

61. Foi reforçada a designação `Candidate Parameter Set V1.0`, para deixar claro que pesos, thresholds, normalizações, classes internas e penalizações são candidatos operacionais, não calibração científica final.

62. A UI foi ajustada para distinguir melhor `Current Area Score` e `Latest NP Assessment`, evitando a confusão entre score agregado de área/projeção e última avaliação persistida em `risk_assessment_log`.

63. Foi analisada a diferença entre `Assessment Count`, `Recent Risk Rows`, score agregado e última avaliação, ficando claro que estes valores podem divergir por agregação, janela temporal, carry-forward ou estado operacional.

64. Foram estabilizados conceitos de `freshness`, `coverage` e `carry-forward`, incluindo o caso em que cenários históricos aparecem como `Expired` por comparação com o relógio real da runtime.

65. Foi integrada a exposição dos índices FWI e KBDI na UI como camada de comparação e proveniência, considerada uma evolução metodológica natural após a V1 funcional.

66. Foi identificado que FWI e KBDI apareciam inicialmente como `n/a` ou incompletos porque o `daily_reference`, em particular a precipitação diária, não estava a ser materializado corretamente no estado diário usado pelo scoring.

67. Foi corrigida a interpretação de precipitação diária igual a `0.0`, garantindo que ausência de chuva é tratada como valor meteorológico válido e não como campo em falta.

68. O FWI passou a aparecer na UI com valor calculado, normalização, classe qualitativa e estado de cálculo, permitindo comparar o score NatureProtector com um índice meteorológico reconhecido.

69. Foi acrescentada interpretação qualitativa para FWI, incluindo a noção de que um valor como cerca de `16.95` pode ser `Moderado` mas próximo da classe seguinte.

70. Foi clarificado que o FWI não é, por si só, o risco final de incêndio rural, sendo antes uma componente meteorológica que deve ser contextualizada com território e outros fatores.

71. O KBDI passou a aparecer na UI com valor, normalização, classe de secura e limitações, deixando de ser apenas um campo ausente ou não interpretável.

72. Foi identificado que o KBDI, por ser acumulativo, não pode ser interpretado corretamente se for calculado apenas a partir de um estado antecedente default ou de histórico demasiado curto.

73. Foi reforçada a regra de que o KBDI deve evoluir por dia lógico e não por leitura/evento, evitando simular vários dias dentro da mesma run.

74. Foi introduzida ou clarificada a limitação `LimitedAntecedentHistory`, para indicar que o valor de KBDI existe mas não deve ser lido como seca acumulada plenamente validada sem histórico suficiente.

75. Foi definida a necessidade de separar valores calculados internamente de valores de referência importados, tanto para FWI como para KBDI.

76. Foi criado ou exposto um `Portuguese Context Proxy`, isto é, um proxy candidato inspirado na ideia de combinar meteorologia e território, mas explicitamente não equivalente ao RCM/PIR/IPMA oficial.

77. O proxy contextual português passou a mostrar interpretações como `FWI Moderado × Territory High -> Elevado`, permitindo explicar porque o contexto local pode aumentar a leitura qualitativa apesar de FWI ou KBDI isolados não serem extremos.

78. Foi clarificado que o `Portuguese Context Proxy` não deve ser apresentado como metodologia oficial, porque não usa a perigosidade rural oficial nem a matriz oficial institucional.

79. Foi acrescentada a indicação de que o percentil local de FWI ainda não está disponível quando não existe distribuição histórica local materializada, evitando inventar anomalias ou percentis sem base de dados.

80. Foi melhorada a leitura do `scenario_c`, incluindo perfis de degradação como `missing-readings`, `noise`, `lag/delay`, `outlier` e `stuck-value`, com maior atenção à diferença entre perfil pedido, resolvido, aplicado e observado.

81. Foi identificado que a tabela de efeitos de degradação deve distinguir variação natural de efeito injetado, para não apresentar `noise` ou `lag/delay` como aplicados quando o perfil está inativo ou abaixo de threshold.

82. Durante a validação runtime foi encontrada uma regressão em que o `scenario_b` publicava e aceitava eventos, mas produzia `0` risk assessments e colocava todos os eventos em quarentena com `db_data_exception`.

83. A causa da regressão foi diagnosticada como erro de persistência em `projection.daily_cell_state`, causado por campos textuais limitados a `varchar(100)` que passaram a receber strings compostas de proveniência, estado antecedente e limitações.

84. Foi corrigida a persistência de campos compostos em `daily_cell_state`, passando campos como `AntecedentState`, `DroughtContext`, `Provenance`, `FireIndexProvenance`, `FireWeatherLimitations` e `KbdiLimitations` para `text`, em vez de truncar informação.

85. Foi mantida a limitação de tamanho apenas para campos de vocabulário controlado, como status e versão do candidate parameter set, preservando a distinção entre estados simples e listas/contextos extensíveis.

86. Após a correção, a runtime voltou a conseguir produzir risk assessments, mostrar score NP, FWI, KBDI e proxy contextual português na UI.

87. Ficou identificado um pequeno ponto de estabilização ainda pendente: a UI mostrou `20` attempts, `19` sucessos, `0` falhas e `0` quarentenas, o que indica a necessidade de expor explicitamente `Other`, `Pending` ou `Unknown attempts` para explicar a tentativa restante.

88. Foi analisado o estado inicial do relatório e identificado que a estrutura existente ainda estava parcialmente herdada de versões anteriores, com conteúdo genérico, referências antigas, apêndices pouco úteis e capítulos desalinhados com a implementação real da V1.

89. Foi definida uma nova estrutura de relatório centrada na baseline V1, com capítulos dedicados a introdução, estado da arte, requisitos e âmbito, arquitetura, estratégia de implementação, implementação, validação técnica e conclusões.

90. O relatório foi reenquadrado como documento técnico da V1, e não como relatório final do projeto completo nem como prova de validação científica do modelo de risco.

91. Foram reescritos e compactados os capítulos principais para reduzir o número de páginas e aproximar o documento do limite recomendado.

92. Foram removidas páginas em branco, secções redundantes, apêndices vazios, listas automáticas pouco úteis e sínteses finais que repetiam conteúdo.

93. Foi reforçada em vários capítulos a distinção entre validação técnica e validação científica, evitando apresentar os scores, limiares e pesos da V1 como valores cientificamente calibrados.

94. Foi criado e consolidado o Capítulo 6 como descrição da implementação V1, incluindo contratos, RabbitMQ, pipeline durável, validação, elegibilidade, `RiskInput`, `RiskAssessment`, projeções, API, alertas e observabilidade.

95. Foi criado e consolidado o Capítulo 7 como descrição da validação técnica e evidência runtime, incluindo evidência C7, queries PostgreSQL, API vs DB, eventos rejeitados, eventos em quarentena, erro histórico `EmptyProjectionMember`, testes de alert policy e veredito `OK com limitações`.

96. Foi feita uma passagem de citações nos capítulos para ligar afirmações externas a referências sobre FWI, KBDI, IPMA, EFFIS, ICNF, dados territoriais, RabbitMQ, acknowledgements, idempotência e transactional outbox.

97. Foi substituída a lista automática de acrónimos por uma tabela manual compacta, devido a problemas de paginação, geração vazia com `glossaries` e duplicação de definições.

98. Foram corrigidos problemas de geração LaTeX relacionados com `minitoc`, contador `mtc`, ficheiros auxiliares, bibliografia, acrónimos, referências e espaçamento excessivo antes da secção de referências.

99. Ficou claro que o relatório atual ainda é provisório: serve para documentar a V1 e ter uma base apresentável, mas não é ainda o relatório final ideal do projeto.

100. Foram identificadas limitações atuais do relatório, nomeadamente falta de imagens e diagramas, explicação ainda insuficiente do âmbito global do projeto e desalinhamento face ao repositório, que já se encontra orientado para a V2.
---

## Resultado principal da quinzena

O principal resultado desta quinzena foi transformar o website numa interface muito mais alinhada com a lógica real do NatureProtector e, em paralelo, estabilizar a infraestrutura necessária para a demonstração e para o onboarding local.

Antes, a aplicação já tinha várias funcionalidades importantes, como dashboards, mapa, runtime monitor, diagnostics, run orchestrator e comparação B/C. No entanto, estas funcionalidades estavam demasiado dispersas e nem sempre ajudavam a explicar o sistema de forma clara.

Com a nova organização, a interface passou a seguir uma narrativa mais próxima do projeto:

`área monitorizada -> cenário/run -> pipeline runtime -> risco/alertas -> evidência -> modelo/proveniência`

Isto torna a aplicação mais útil para a demo, para o design review e para explicar o funcionamento interno do sistema.

A `Monitoring` permite apresentar o estado operacional da área. O `Scenario Lab` permite preparar e executar runs. O `Flow Explorer` ajuda a explicar o percurso dos dados no backend. O `Evidence & Comparison` permite comparar execuções e justificar resultados. O `Model & Provenance` liga a interface à metodologia, aos conceitos e ao código.

Outro resultado importante foi a melhoria da análise temporal. A tab `Run Timings` mostrou que a UI precisava de uma fonte de dados real para representar tempos de processamento. Em vez de inventar dados ou tentar ler logs locais diretamente no browser, foi criado um endpoint read-only baseado em dados persistidos. Isto tornou a análise de timings mais sólida e mais defensável.

A comparação entre `scenario_b` e `scenario_c` também ganhou importância. A UI passou a mostrar de forma mais direta que o cenário C produz menos leituras aceites e mais eventos em falta, evidenciando a degradação operacional esperada. Esta comparação é útil para apresentação porque mostra comportamento observável da pipeline sem afirmar validação científica do risco.

Também foi feito trabalho importante na preparação da apresentação. Os diagramas técnicos existentes eram úteis para documentação, mas excessivamente densos para uma apresentação curta. Depois do feedback recebido, foi decidido usar diagramas simplificados, com menos blocos, setas mais claras e texto mais direto. A apresentação foi reorganizada para reduzir ruído visual, evitar slides demasiado preenchidos e reservar tempo suficiente para a demonstração.

Por fim, foi resolvido um problema relevante de reprodutibilidade da infraestrutura local. Confirmou-se que, depois de remover volumes Docker, o InfluxDB não aceitava automaticamente o token do `.env` e a database `np_telemetry` não ficava garantida. Foram criados scripts para preparar o token admin local, garantir a database temporal e validar a baseline. O teste final confirmou que a infraestrutura consegue ser recriada a partir de volumes limpos e voltar a ficar funcional.

Na continuação deste trabalho, o processo de setup foi tornado mais explícito e seguro. A equipa separou diagnóstico de dependências, instalação opt-in, arranque da infraestrutura, arranque do runtime e validação final. Esta separação evita que um comando como `up.ps1` passe a fazer instalações ou alterações perigosas sem o utilizador perceber, mas ainda assim dá um caminho mais simples para alguém novo conseguir preparar a máquina e correr o projeto.

Para além da reorganização da UI, da preparação da apresentação e da estabilização da infraestrutura, a quinzena passou também a incluir uma frente importante de consolidação da V1 enquanto pipeline operacional de risco.

A fórmula NatureProtector deixou de ser tratada apenas como um score final e passou a ser exposta como uma decomposição auditável: `BaseRisk`, `AdjustedScore`, componentes `M/D/T`, subcomponentes `H/F/G`, confiança/integridade `C/I`, driver dominante, estado de cálculo e limitações. Esta decomposição torna a V1 mais explicável e mais alinhada com a pesquisa, porque permite demonstrar como meteorologia, secura, território e qualidade operacional contribuem para o resultado.

Também foi iniciada uma camada de evolução metodológica que pode ser entendida como o passo natural seguinte após a V1 funcional. Esta camada inclui a integração de FWI e KBDI como índices de comparação, proveniência e contexto. O objetivo não é substituir o score NatureProtector nem afirmar validação científica final, mas permitir comparar o score interno com índices conhecidos, mostrando quando convergem, quando divergem e porquê.

O FWI passou a ser mostrado com valor, normalização, classe qualitativa e proximidade à classe seguinte. O KBDI passou a ser mostrado como indicador de secura acumulada, com classe qualitativa e limitação explícita quando falta histórico antecedente suficiente. Esta distinção é importante porque o FWI é uma componente meteorológica diária, enquanto o KBDI é acumulativo e depende de estado anterior.

Foi também criado um `Portuguese Context Proxy`, que combina FWI e território interno para aproximar a interpretação ao contexto português. Este proxy foi tratado como candidato e não oficial, evitando afirmar equivalência ao RCM/PIR/IPMA. A sua utilidade é explicar casos em que FWI moderado, quando combinado com território elevado, pode justificar uma leitura contextual portuguesa mais alta.

Durante esta frente foi encontrada e corrigida uma regressão real de runtime. Depois das alterações aos índices e à proveniência, o sistema passou a tentar persistir strings compostas em campos de `daily_cell_state` demasiado curtos. Isto causava `Npgsql.PostgresException 22001`, quarentena de todos os eventos e ausência de risk assessments. A correção consistiu em alterar campos de contexto, proveniência e limitações para `text`, mantendo limites apenas em campos de status controlado. Esta correção reforçou a robustez da pipeline e mostrou a importância de tratar limitações e proveniência como dados persistidos relevantes.

---

## 1. Reorganização da interface

A reorganização do website teve como objetivo reduzir a confusão entre funcionalidades de operação, desenvolvimento, diagnóstico e explicação.

A nova navegação separa melhor os diferentes usos da aplicação:

* `Monitoring` para acompanhar a área;
* `Scenario Lab` para executar e consultar runs;
* `Flow Explorer` para entender a pipeline;
* `Evidence & Comparison` para auditar e comparar resultados;
* `Model & Provenance` para explicar conceitos, proveniência e ligação ao código.

Esta separação torna a interface mais fácil de apresentar e reduz a necessidade de navegar por páginas longas com muitos blocos misturados.

Também foi reforçada a ideia de usar tabs para evitar scroll excessivo e permitir que cada zona tenha um objetivo claro.

---

## 2. Monitoring e visualização operacional

A área de `Monitoring` passou a reunir as vistas mais diretamente relacionadas com o estado da área monitorizada.

Foram organizadas tabs para visão geral, mapa/células, dashboards de sensores, risco da área e alertas.

Esta zona deve permitir responder rapidamente a perguntas como:

* que área está selecionada;
* qual foi a última run;
* qual é o risco atual;
* existem alertas ativos;
* que sensores/células estão representados;
* que dados estão recentes ou desatualizados.

Durante a validação, foram encontrados problemas nos dashboards embebidos, principalmente quando o iframe não apontava corretamente para Grafana ou quando faltava `sensor_id` nos dashboards por célula.

Um problema concreto foi a regressão no mapeamento dos dashboards por célula. A API expunha os sensores associados às células em formato `Tuple<Guid,string>`, serializado como `item1` e `item2`, mas a UI reorganizada passou a esperar campos como `id` e `type`. Como resultado, a célula era encontrada, mas a UI não conseguia resolver os sensores de temperatura, humidade e vento, mostrando mensagens como `Sensor: Not available`.

A correção consistiu em tornar o resolver da UI compatível com o contrato real exposto pela API. A UI passou a suportar `item1`/`item2`, `id`/`type`, strings, nomes, códigos e informação complementar dos `sensorNodes`. Com isto, os dashboards por célula voltaram a resolver sensores como `pilot-temperature-0001`, `pilot-humidity-0001` e `pilot-wind-0001`, usando internamente os GUIDs necessários para o `var-sensor_id` no Grafana.

Também foi analisado o problema de a UI mostrar a interface completa do Grafana dentro dos iframes. Para a apresentação, foi decidido usar URLs `d-solo` com `panelId`, para que cada iframe mostre apenas o painel necessário, sem a navegação completa do Grafana.

A regra definida foi não mostrar dashboards partidos e apresentar estados vazios claros sempre que os dados necessários não estejam expostos.

---

## 3. Scenario Lab e controlo de runs

O `Scenario Lab` passou a concentrar a execução e leitura de cenários.

O run orchestrator continua a permitir configurar e lançar runs com parâmetros como cenário, número de sensores, ciclos, intervalo, seed e perfil de degradação.

A definição dos cenários foi separada numa tab própria, tornando mais fácil explicar o que cada cenário representa.

A informação da última run também foi reorganizada para ser mais legível.

O reset runtime ficou isolado numa área própria, mantendo a lógica de confirmação e evitando misturar uma operação sensível com a narrativa normal da demo.

Esta separação melhora a segurança operacional e a clareza da apresentação.

Durante os testes, foi também identificado que o `scenario_c` se comportava demasiado parecido com o `scenario_b` quando a degradação não era aplicada de forma explícita. A partir daí, foi reforçada a necessidade de distinguir claramente definição do cenário, overrides da run e perfil de degradação. O cenário C passou a estar associado a `missing-readings`, enquanto o cenário B preserva comportamento sem degradação automática.

---

## 4. Evidence & Comparison

A área `Evidence & Comparison` passou a ser uma das zonas mais importantes para demonstração.

A comparação entre `scenario_b` e `scenario_c` foi destacada, permitindo observar diferenças entre uma execução sem degradação e uma execução com missing readings.

Esta comparação passou a mostrar dados como:

* eventos esperados;
* leituras aceites;
* eventos em falta;
* risk assessments;
* rejeições;
* quarentenas;
* estatísticas por métrica.

A tab `Run Timings` também foi melhorada. Inicialmente, a UI mostrava vários campos como não expostos. Depois foi criado um endpoint backend read-only para expor timings reais a partir da base de dados.

Com isso, a UI passou a conseguir mostrar duração da run, tempos até etapas relevantes e resumo de attempts.

Ainda ficou por resolver a integração dos stopwatches dos logs, que exigirá estruturação futura dos dados por run.

---

## 5. Flow Explorer

O `Flow Explorer` foi criado para ajudar a explicar o que acontece por baixo dos panos.

A ideia central é representar o percurso da run pelo sistema:

`Scenario Run -> Event Inbox -> Processing Attempts -> Risk -> State -> Alerts -> API/UI`

Esta vista é importante porque aproxima a UI dos diagramas técnicos e ajuda a explicar a pipeline de forma visual.

A primeira versão já cria a estrutura, mas ainda precisa de evoluir para mostrar mais estados reais e evidência por etapa.

A melhoria futura mais importante é tornar o fluxo nominal menos estático, mostrando para cada passo se foi concluído, se tem dados parciais, se falhou ou se ainda não está exposto.

---

## 6. Model & Provenance

A área `Model & Provenance` foi criada para explicar a relação entre conceitos, dados, proveniência e código.

Esta área não é uma vista operacional. A sua função é apoiar a explicação técnica e metodológica do projeto.

Ela deve ajudar a responder a perguntas como:

* que conceitos existem na V1;
* quais estão implementados;
* quais são apenas conceptuais;
* quais são persistidos;
* quais aparecem na UI;
* que código está associado a cada parte.

A primeira versão já cria a base desta área, mas ainda precisa de ser melhorada para funcionar como matriz de rastreabilidade.

A estrutura desejada é aproximar cada conceito ao seu estado, evidência e implementação.

---


## 6A. Consolidação da fórmula V1 e integração FWI/KBDI

Durante esta quinzena foi feita uma frente de consolidação da fórmula operacional do NatureProtector, com o objetivo de alinhar melhor a implementação com a pesquisa da V1 e tornar o cálculo de risco mais explicável na UI.

A principal decisão foi manter a V1 como uma pipeline técnica e metodológica candidata, e não como modelo científico final calibrado. O sistema passou a expor mais claramente que o score NatureProtector é calculado a partir de uma cadeia de dados e decisões, e não diretamente a partir de mensagens raw.

A cadeia conceptual estabilizada foi:

```text
leitura/evento -> validação -> normalização -> elegibilidade -> RiskInput -> scoring -> RiskAssessment -> projeções -> UI
```

Esta distinção é importante porque evita que uma mensagem recebida seja tratada automaticamente como risco. Antes de chegar ao scoring, os dados precisam de passar por validação, normalização, qualidade, elegibilidade e construção de input apropriado para risco.

A fórmula NatureProtector passou a ser apresentada com maior decomposição:

```text
BaseRisk / AdjustedScore
M / D / T
H / F / G
C / I
dominant driver
calculation status
limitations
```

Esta decomposição permite explicar melhor a origem de cada resultado. A componente `M` representa meteorologia dinâmica, `D` representa secura persistente e `T` representa território. Dentro de `T`, os subcomponentes `H`, `F` e `G` ajudam a explicar hazard estrutural, combustível/cobertura e geomorfologia ou contexto físico simplificado. As componentes `C` e `I` representam confiança observacional e integridade operacional.

Também foi reforçada a distinção entre `BaseRisk` e `AdjustedScore`. O `BaseRisk` representa o risco antes de modificadores de confiança/integridade; o `AdjustedScore` representa o score final depois de considerar qualidade operacional, parcialidade ou limitações. Esta separação evita confundir risco físico/territorial com confiança nos dados observados.

Na UI, foi ajustada a distinção entre `Current Area Score` e `Latest NP Assessment`. O primeiro representa a projeção agregada da área e pode incluir carry-forward. O segundo representa a última avaliação persistida no `risk_assessment_log`. Esta alteração reduz a ambiguidade quando os valores divergem ou quando a área mantém estado projetado a partir de informação anterior.

Para além da consolidação da V1, foi iniciada a integração de FWI e KBDI como camada de comparação e proveniência. Esta camada pode ser entendida como uma evolução natural para uma V2 metodológica, porque só faz sentido comparar com índices externos depois de a pipeline interna já estar funcional.

O FWI foi integrado como índice meteorológico de referência. Inicialmente aparecia como `n/a` ou parcial porque a precipitação diária não estava a chegar corretamente ao cálculo. Foi confirmado que `0.0 mm` de precipitação é um valor válido e não deve ser tratado como ausência de dado. Após correção da materialização do `daily_reference`, o FWI passou a aparecer na UI com valor, normalização, classe qualitativa e limitações.

O KBDI foi integrado como indicador de secura acumulada. A principal preocupação metodológica foi garantir que o KBDI não fosse interpretado como índice instantâneo. O KBDI depende de histórico e estado antecedente, pelo que um valor baixo num cenário de risco pode significar que existe pouco histórico antecedente ou que o cálculo parte de default candidato. Por isso, passou a ser importante expor estados como `LimitedAntecedentHistory` e explicar que o valor existe, mas tem limitação interpretativa.

Foi também introduzido um `Portuguese Context Proxy`. Este proxy combina uma leitura meteorológica baseada em FWI com uma leitura territorial interna, permitindo aproximar a interpretação à lógica portuguesa de combinar meteorologia e perigosidade territorial. No entanto, ficou explicitamente definido que este proxy não é RCM oficial, não é PIR oficial e não reproduz metodologia IPMA/ICNF. É apenas uma aproximação candidata útil para demonstração e análise metodológica.

A UI passou a conseguir mostrar simultaneamente:

* score NatureProtector;
* classe do score;
* `BaseRisk` e `AdjustedScore`;
* componentes `M/D/T`;
* subcomponentes `H/F/G`;
* confiança e integridade `C/I`;
* FWI calculado e respetiva classe;
* KBDI e respetiva classe de secura;
* precipitação diária;
* proxy contextual português;
* limitações e proveniência.

Durante a validação desta frente surgiu uma regressão importante. O `scenario_b` completava a run, publicava e aceitava eventos, mas não produzia risk assessments. Todos os eventos entravam em quarentena com `db_data_exception`. A análise mostrou que a falha não era causada por migrations pendentes nem por modelo EF desalinhado. A causa real estava no PostgreSQL: `value too long for type character varying(100)` ao inserir em `projection.daily_cell_state`.

O problema surgiu porque campos como `AntecedentState`, `DroughtContext`, `FireIndexProvenance`, `Provenance`, `FireWeatherLimitations` e `KbdiLimitations` passaram a receber valores compostos com várias limitações e informações de proveniência. Estes campos não devem ser tratados como strings curtas. A correção foi alterar estes campos para `text`, preservando a rastreabilidade em vez de truncar informação. Campos de status controlado, como `FireWeatherCalculationStatus` e `KbdiCalculationStatus`, mantiveram limites curtos.

Depois da correção, a runtime voltou a produzir risk assessments e a UI passou a apresentar novamente o score NP, FWI, KBDI e Portuguese Context Proxy. Ficou apenas identificado um detalhe adicional: em algumas runs a soma de attempts por estado não fechava exatamente o total, por exemplo `20` attempts, `19` successful, `0` failed e `0` quarantined. Isto deve ser tratado futuramente expondo `Other`, `Pending` ou `Unknown attempts`, para explicar a tentativa restante.

---

## 7. Diagramas e preparação da apresentação

Durante esta quinzena foi feita uma revisão dos diagramas existentes e da forma como estes deviam ser usados na apresentação.

Os diagramas técnicos existentes tinham valor para documentação, porque representavam a arquitetura, a pipeline, a persistência, o bootstrap, os cenários, os testes e o fluxo de rejeição/retry/quarentena. No entanto, para apresentação, eram demasiado densos. Tinham muitos blocos, muitos nomes técnicos e demasiado detalhe para serem compreendidos rapidamente por quem assiste.

Inicialmente foram criados diagramas simplificados para apresentação, como:

* visão geral do projeto;
* fluxo runtime simples;
* pipeline de risco simples;
* alertas e API;
* orquestrador de runs.

Também foram criados diagramas técnicos canónicos adicionais para documentação, incluindo a cadeia runtime V1 e o funcionamento do scenario run orchestrator.

Depois do feedback dos professores, ficou claro que mesmo alguns diagramas simplificados continuavam demasiado próximos de diagramas técnicos. A sugestão recebida foi manter a lógica dos três blocos principais, mas retirar o excesso de caixas internas. Em vez de mostrar muitos componentes, os slides devem mostrar blocos maiores com tópicos do que acontece dentro de cada bloco e setas que expliquem que informação passa entre eles.

A partir desse feedback, ficou definido que os diagramas para a apresentação devem seguir estas regras:

* poucos blocos;
* texto curto;
* frases orientadas à ação;
* setas com significado claro;
* menos nomes de classes e componentes internos;
* mais foco no que o sistema faz;
* detalhe técnico reservado para perguntas ou slides extra;
* evitar slides visualmente cheios;
* manter o template original da apresentação.

Também foi revista a estrutura geral da apresentação. Como o tempo total previsto é de 15 minutos, com cerca de 5 minutos para demonstração, ficou definido que a parte inicial deve ser objetiva: introdução, apresentação do projeto, solução geral, investigação/metodologia, diagramas internos essenciais e passagem para a demo.

Foi considerado importante apresentar não só a primeira fase da pesquisa, mas também a evolução posterior. A apresentação deve mostrar como a pesquisa influenciou a V1, sem afirmar que a V1 é uma validação científica final. A mensagem central deve manter-se: a V1 é uma pipeline técnica e metodológica funcional, com validação técnica e runtime, enquanto a calibração científica final fica como trabalho futuro.

---

## 8. Estabilização da infraestrutura local e InfluxDB

Foi analisado um problema de reprodutibilidade da infraestrutura local, relacionado com o InfluxDB e a criação da database temporal `np_telemetry`.

O problema surgiu quando se quis garantir que uma pessoa que fizesse pull do repositório conseguiria levantar tudo sem passos manuais. A expectativa era que, ao correr os scripts locais, PostgreSQL, RabbitMQ, InfluxDB, Grafana, API, Prevention Host e webUI ficassem prontos. No entanto, percebeu-se que `np_telemetry` podia não existir em ambientes novos ou depois de remover volumes Docker.

A primeira análise mostrou que o container `np-influxdb-init` existia, mas não fazia o provisioning lógico do InfluxDB. Ele apenas tratava permissões de volume. Portanto, o ambiente tinha um container chamado init, mas esse init não criava a database temporal, não validava token e não garantia a store usada pela observabilidade.

Para resolver a primeira parte do problema, foi criado o script `scripts/influx/Ensure-InfluxDatabase.ps1`. Este script lê a configuração, autentica no InfluxDB e garante que `np_telemetry` existe. O script é idempotente: se a database já existir, não faz alterações desnecessárias; se faltar, cria-a.

Também foi criado ou revisto o script `scripts/setup/Test-LocalBaseline.ps1`, para validar sistematicamente a baseline local. Durante a validação, foi encontrado um problema adicional: o script usava `docker compose exec` e `docker compose ps` de forma dependente do contexto atual do PowerShell. Isto causava erros como `no configuration file provided`. Como a baseline local usa containers com nomes fixos (`np-postgres`, `np-rabbitmq`, `np-influxdb`, `np-grafana`), o script foi ajustado para usar `docker exec` diretamente sobre esses containers.

Depois disso, `Test-LocalBaseline.ps1 -InfrastructureOnly` passou a validar corretamente Docker, PostgreSQL, RabbitMQ, InfluxDB, Grafana e a existência de `np_telemetry`.

Para testar a reconstrução do ambiente do zero, foi criado `infra/scripts/reset-local-infra.ps1`. Este script é destrutivo e remove volumes locais, mas ficou protegido por confirmação textual explícita: `-Confirm RESET_LOCAL_INFRA`. Antes de o usar, foi validado que ele recusa executar se a confirmação não for exatamente a esperada.

Ao correr o reset destrutivo, foi descoberta a falha principal: com volumes novos, o InfluxDB arrancava, mas não reconhecia automaticamente o `INFLUXDB_TOKEN` do `.env`. O script `Ensure-InfluxDatabase.ps1` falhava com `401 Unauthorized`. Isto provou que o ambiente anterior funcionava porque o volume antigo já tinha estado interno configurado, e não porque o repositório garantia um bootstrap completo do InfluxDB.

A correção final foi criar `scripts/influx/Ensure-InfluxAdminTokenFile.ps1`. Este script lê o `INFLUXDB_TOKEN` do `.env`, valida que começa por `apiv3_` e gera um ficheiro local não versionado em `data/runtime/influx/admin-token.json`. O ficheiro é ignorado pelo Git e serve apenas para o InfluxDB 3 criar/aceitar o token admin no primeiro arranque sobre volumes novos.

O `docker-compose.yml` foi ajustado para montar esse ficheiro no container `np-influxdb` e configurar `INFLUXDB3_ADMIN_TOKEN_FILE`. Os scripts `up.ps1` e `reset-local-infra.ps1` foram atualizados para preparar o ficheiro antes de arrancar o InfluxDB.

Foi ainda encontrada uma incompatibilidade com Windows PowerShell 5.1, porque `Set-Content -Encoding utf8NoBOM` não existe nessa versão. A escrita do ficheiro foi ajustada para usar `System.IO.File.WriteAllText` com `System.Text.UTF8Encoding($false)`, mantendo UTF-8 sem BOM e compatibilidade com o ambiente usado.

O teste final confirmou o comportamento esperado. Depois de remover volumes, a infraestrutura voltou a subir, o InfluxDB aceitou o token do `.env`, a database `np_telemetry` foi criada automaticamente, o bootstrap PostgreSQL recriou o plano de controlo e `Test-LocalBaseline.ps1 -InfrastructureOnly` passou com 0 falhas e 0 avisos.

Esta alteração melhora a robustez da V1 porque torna a baseline local reprodutível e menos dependente de estado pré-existente na máquina. Também evita passos manuais no InfluxDB e torna mais claro o papel de cada script:

* `up.ps1` levanta a infraestrutura sem destruir dados;
* `down.ps1` para containers preservando volumes;
* `reset-local-infra.ps1` remove volumes apenas com confirmação explícita;
* `Ensure-InfluxAdminTokenFile.ps1` prepara o token local do InfluxDB;
* `Ensure-InfluxDatabase.ps1` garante a database `np_telemetry`;
* `Test-LocalBaseline.ps1` valida se a infraestrutura está pronta.

---

## 9. Setup guiado e instalação de dependências

Depois de estabilizar a infraestrutura Docker e o InfluxDB, foi revista a experiência de setup local para alguém novo no projeto.

A questão principal foi perceber se o projeto já podia ser corrido de forma simples por uma pessoa que fizesse pull do repositório. A conclusão foi que a infraestrutura já estava mais robusta, mas ainda faltava separar melhor três momentos diferentes:

* verificar se a máquina tem dependências;
* instalar ou orientar instalação de dependências em falta;
* levantar a infraestrutura e runtime do projeto.

Foi decidido que o `up.ps1` não deve instalar dependências. Esta decisão foi importante porque instalar Docker Desktop, Node.js, .NET SDK ou Git pode exigir permissões de administrador, alterar `PATH`, obrigar a reiniciar a shell ou exigir que o Docker Desktop seja aberto manualmente. Se o `up.ps1` fizesse isso automaticamente, deixaria de ser previsível e poderia alterar a máquina do utilizador sem clareza.

A solução foi manter o `up.ps1` focado apenas em infraestrutura e criar scripts próprios para diagnóstico e onboarding.

O `scripts/setup/Test-LocalPrerequisites.ps1` passou a assumir o papel de diagnóstico read-only. Este script verifica ferramentas e condições necessárias para o ambiente local, incluindo PowerShell, Git, Docker CLI, Docker engine, Docker Compose v2, .NET SDK, Node.js, npm, `.env`, `.env.example` e portas relevantes. O objetivo é dizer claramente o que está pronto, o que falta e o que pode bloquear o arranque.

O `scripts/setup/Install-LocalPrerequisites.ps1` foi criado como instalador/sugestor opt-in. Sem flags destrutivas ou automáticas, o script apenas sugere comandos, normalmente via `winget`. Quando executado em modo `WhatIf`, não instala nada. A instalação real só deve acontecer com flags explícitas. Isto evita que a preparação da máquina se misture com o arranque normal do projeto.

O `scripts/setup/Setup-LocalEnvironment.ps1` foi criado como fluxo guiado de onboarding. Este script chama a verificação de pré-requisitos, prepara `.env` se faltar, chama `up.ps1`, valida a infraestrutura com `Test-LocalBaseline.ps1 -InfrastructureOnly` e, opcionalmente, arranca o runtime com `start-local-runtime.ps1` quando são usados os parâmetros `-StartRuntime` e `-OpenBrowser`.

A separação final ficou assim:

* `Test-LocalPrerequisites.ps1` diagnostica dependências;
* `Install-LocalPrerequisites.ps1` sugere ou instala dependências apenas com autorização explícita;
* `Setup-LocalEnvironment.ps1` orquestra o setup local;
* `up.ps1` sobe a infraestrutura;
* `start-local-runtime.ps1` arranca API, Prevention Host e webUI;
* `Test-LocalBaseline.ps1` valida infraestrutura e runtime;
* `reset-local-infra.ps1` é o único caminho destrutivo e continua protegido por confirmação textual.

Durante a validação do `Setup-LocalEnvironment.ps1`, foi encontrado um problema no wrapper que chamava outros scripts PowerShell. O script capturava `stderr` com `2>&1`, e algumas mensagens informativas do Docker Compose eram tratadas como erro pelo Windows PowerShell. Isto fazia com que o setup guiado falhasse apesar de a infraestrutura estar correta. O wrapper foi ajustado para capturar `stdout` e `stderr` através de `System.Diagnostics.ProcessStartInfo`, tratando `stderr` como erro apenas quando o processo termina com exit code diferente de zero.

Depois surgiu outra incompatibilidade com Windows PowerShell 5.1: `ProcessStartInfo.ArgumentList` podia estar indisponível ou nulo. A chamada foi então adaptada para montar a string de argumentos manualmente em `ProcessStartInfo.Arguments`, mantendo compatibilidade com o ambiente usado.

A validação incluiu parse dos scripts, execução de `Test-LocalPrerequisites.ps1`, execução de `Install-LocalPrerequisites.ps1 -WhatIf`, execução de `Setup-LocalEnvironment.ps1` sem runtime e validação da infraestrutura com `Test-LocalBaseline.ps1 -InfrastructureOnly`. Também foram executados build e testes do projeto para confirmar que as alterações de scripts e documentação não introduziram regressões funcionais.

Esta frente melhora a capacidade de onboarding do projeto. Em vez de depender de instruções dispersas ou conhecimento tácito, passa a existir um percurso mais claro:

```powershell
.\scripts\setup\Test-LocalPrerequisites.ps1
.\scripts\setup\Install-LocalPrerequisites.ps1 -WhatIf
.\scripts\setup\Setup-LocalEnvironment.ps1
```

Para o uso normal, depois de a máquina estar preparada, o fluxo continua curto:

```powershell
.\infra\scripts\up.ps1
.\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

Isto reduz a fricção para novos utilizadores sem tornar o arranque normal perigoso ou imprevisível.

---

## 10. Problemas encontrados e estabilização

A reorganização revelou alguns problemas práticos.

O primeiro foi a existência de duas navbars no Workspace, o que também causava duplicação do botão light/dark. Foi definido que no Workspace deve existir apenas uma topbar.

O segundo foi a existência de iframes Grafana partidos, especialmente na área de risco. Foi decidido que a UI não deve mostrar uma segunda instância da própria aplicação dentro de um iframe nem painéis com `Not Found`.

O terceiro foi a falta de `sensor_id` nos dashboards por célula. A regra definida foi que a UI só deve abrir dashboards quando conseguir resolver o sensor correto; caso contrário, deve mostrar uma mensagem clara.

O quarto problema foi a existência de processos locais antigos a bloquear DLLs e builds. Ficou claro que, para validar o site atualizado, é necessário reiniciar os processos locais da aplicação.

O quinto problema foi a falta de reprodutibilidade total do InfluxDB após remoção de volumes. Esta falha foi corrigida com o bootstrap explícito do token admin e com a criação automática de `np_telemetry`.

O sexto problema foi a falta de um fluxo claro para onboarding local. A infraestrutura já tinha scripts de arranque e validação, mas ainda não existia uma separação suficientemente clara entre verificar dependências, instalar dependências, subir infraestrutura, arrancar runtime e validar o sistema. Isto foi estabilizado com a criação dos scripts de setup guiado e instalação opt-in.

O sétimo problema foi a compatibilidade com Windows PowerShell 5.1. Alguns detalhes que funcionariam em PowerShell 7, como `utf8NoBOM` em `Set-Content` ou `ArgumentList` em `ProcessStartInfo`, não funcionaram no ambiente usado. Estes pontos foram corrigidos para manter compatibilidade com o PowerShell disponível na máquina.

Quando Docker está apenas a correr PostgreSQL, RabbitMQ, InfluxDB ou Grafana, não é necessário mandar Docker abaixo para atualizar a UI ou a API. No entanto, quando se quer testar reprodutibilidade total, o reset destrutivo deve ser feito apenas através do script próprio e com confirmação explícita.


Outro problema relevante surgiu durante a validação da integração da fórmula V1 com FWI, KBDI e Portuguese Context Proxy. Após uma run do `scenario_b`, a UI mostrava que a run tinha completado e que os eventos tinham sido aceites, mas não existiam risk assessments e todos os processing attempts tinham terminado em quarentena com `db_data_exception`.

A análise mostrou que o problema não estava no simulador, no RabbitMQ, no inbox ou nas migrations pendentes. A run publicava eventos, o Prevention Host consumia-os e as leituras eram aceites. A falha acontecia dentro da `reading_risk_pipeline`, no momento de persistir estado em `projection.daily_cell_state`.

O erro real era `Npgsql.PostgresException 22001`, indicando que um valor era demasiado longo para uma coluna `character varying(100)`. A causa foi a introdução de strings mais longas de contexto, proveniência e limitações associadas a FWI, KBDI, histórico antecedente e candidate defaults.

A correção foi alterar campos compostos de `daily_cell_state` para `text`, nomeadamente `AntecedentState`, `DroughtContext`, `Provenance`, `FireIndexProvenance`, `FireWeatherLimitations` e `KbdiLimitations`. Não foi feito truncamento silencioso, porque isso destruiria precisamente a informação necessária para auditoria. Os campos de status simples mantiveram limites controlados.

Este problema mostrou que, à medida que a V1 se torna mais explicável, a persistência também precisa de suportar melhor proveniência e limitações. Não basta guardar apenas o score; é necessário guardar também as razões, defaults, estados parciais e limitações que justificam esse score.

Ficou ainda identificado que a UI deve melhorar a agregação de processing attempts. Quando o total de attempts não coincide com a soma de sucessos, falhas e quarentenas, deve ser mostrado um estado adicional, como `Other`, `Pending` ou `Unknown`, em vez de deixar a diferença implícita.

---

## 11. Validação realizada

A validação incluiu vários checkpoints.

Foi validada a sintaxe dos scripts PowerShell alterados.

Foi confirmado que apenas o script `reset-local-infra.ps1` remove volumes Docker, e que esse script não executa sem a confirmação textual correta.

Foi validada a criação do ficheiro local de token do InfluxDB, confirmando que o ficheiro fica em `data/runtime/influx/admin-token.json` e é ignorado pelo Git.

Foi validado o `docker-compose.yml`, garantindo que o ficheiro de token é montado no container `np-influxdb`.

Foi executado um reset destrutivo controlado, removendo volumes locais e recriando a infraestrutura.

Foi confirmado que `np_telemetry` estava ausente depois do reset, foi criada automaticamente e ficou verificada.

Foi confirmado que o bootstrap PostgreSQL recriou o plano de controlo, incluindo área, células, sensores, perfis, redes, cenários e ligações de datasets.

Foi executado `Test-LocalBaseline.ps1 -InfrastructureOnly`, que terminou com:

`0 required failure(s), 0 total failure(s), 0 warning(s)`

Isto confirmou que a baseline de infraestrutura ficou funcional após reconstrução a partir de volumes limpos.

Também foi validado o novo fluxo de setup local. Foram corridos os scripts de diagnóstico de pré-requisitos, instalação em modo `WhatIf`, setup guiado sem runtime e validação da infraestrutura. O instalador não executou instalações por defeito, apenas sugeriu ações. O setup guiado conseguiu preparar e validar a infraestrutura sem apagar volumes.

Por fim, foram executados build e testes do projeto, que passaram. O warning `NU1902` associado ao pacote `OpenTelemetry.Exporter.OpenTelemetryProtocol` permaneceu como aviso conhecido, não introduzido por esta frente.


Também foi validada a frente de consolidação da fórmula V1 e integração de FWI/KBDI.

Foi confirmado que a UI passou a expor os componentes principais do score NatureProtector, incluindo `BaseRisk`, `AdjustedScore`, `M/D/T`, `H/F/G`, `C/I`, driver dominante, estado de cálculo e limitações. Isto permitiu validar que o frontend não está apenas a mostrar um score opaco, mas sim uma decomposição útil para análise e apresentação.

Foi validado que o FWI passou a aparecer com valor e classe qualitativa, deixando de estar bloqueado por ausência indevida de precipitação quando o valor real era `0.0`. Também foi validado que o KBDI passou a aparecer com valor, normalização e classe de secura, acompanhado de limitação quando o histórico antecedente é insuficiente.

Foi validada a existência do `Portuguese Context Proxy`, com indicação da combinação entre FWI e território. Ficou confirmado que este campo é útil para explicação, mas deve continuar a ser apresentado como proxy candidato e não oficial.

Foi diagnosticada uma regressão runtime em `projection.daily_cell_state`, provocada por limites de tamanho em campos textuais. A causa foi isolada através de logs e queries à base de dados, chegando ao erro `value too long for type character varying(100)`. Depois da alteração dos campos compostos para `text`, a runtime voltou a conseguir produzir risk assessments e a UI voltou a apresentar os índices e componentes esperados.

Foram executados build e testes após as alterações principais. A validação runtime mostrou que o sistema ficou novamente capaz de apresentar score NP, FWI, KBDI e proxy contextual português, embora permaneça como próximo ponto de estabilização a explicação de attempts que não entram diretamente nas categorias `successful`, `failed` ou `quarantined`.

Foi ainda validado, numa fase posterior, o caminho clone-to-run após limpeza de containers, volumes e estado local. O fluxo `up.ps1` → `bootstrap-control-plane.ps1` → `Test-LocalBaseline.ps1 -InfrastructureOnly` → `npm ci` → `start-local-runtime.ps1` permitiu reconstruir a baseline, abrir a UI, autenticar com o utilizador local de desenvolvimento e executar `scenario_b` com sucesso.

---
## 12. Reestruturação e consolidação provisória do relatório V1

Nesta quinzena foi também realizado trabalho significativo sobre o relatório do projeto. O objetivo não foi ainda produzir a versão final ideal do relatório, mas sim transformar uma base inicial desatualizada num documento coerente, apresentável e alinhado com aquilo que foi efetivamente implementado até à V1 do NatureProtector.

A versão inicial do relatório ainda mantinha uma estrutura herdada de fases anteriores do projeto. Existiam partes genéricas, conteúdo desalinhado com o estado real da implementação, apêndices e referências pouco úteis, páginas em branco e capítulos que ainda não descreviam adequadamente a arquitetura, a pipeline, a evidência runtime e a validação técnica da V1. Também havia um problema de dimensão: o documento estava muito acima do limite recomendado.

Foi então feita uma reestruturação global do relatório para o organizar em torno da baseline V1. A estrutura consolidada passou a conter os seguintes capítulos:

1. Introdução;
2. Estado da Arte;
3. Requisitos e Âmbito;
4. Arquitetura e Suporte de Desenvolvimento;
5. Estratégia de Implementação e Evolução do Âmbito;
6. Implementação do NatureProtector V1;
7. Validação Técnica e Evidência em Tempo de Execução;
8. Conclusões e Trabalho Futuro.

Esta nova estrutura permitiu separar melhor o problema, o enquadramento técnico, o âmbito fechado da V1, a arquitetura, a implementação, a validação técnica, a evidência runtime e as limitações. O foco passou a estar explicitamente na V1 enquanto base técnica e metodológica do módulo de prevenção, e não numa tentativa de apresentar o NatureProtector como sistema final ou cientificamente calibrado de previsão de incêndios.

Foi reforçada ao longo do relatório a distinção entre validação técnica e validação científica. A V1 é apresentada como uma pipeline operacional rastreável, capaz de receber eventos simulados, persistir dados, processar leituras, produzir avaliações candidatas de risco, atualizar projeções operacionais, expor estado através da API e recolher evidência. Não é apresentada como índice oficial, modelo calibrado ou substituto de sistemas como FWI, IPMA, EFFIS, RCM ou PIR.

O relatório foi também reduzido de forma agressiva para se aproximar do limite indicado. Foram removidas páginas em branco, apêndices vazios, referências desatualizadas, listas automáticas pouco úteis e secções de síntese redundantes. O índice foi reduzido para mostrar apenas capítulos. Vários capítulos foram reescritos em rondas sucessivas para ficarem mais curtos, mantendo apenas o essencial.

Os capítulos mais trabalhados foram o 3, 5, 6, 7 e 8. O Capítulo 3 foi reduzido para clarificar o âmbito, o fora de âmbito, os requisitos e a distinção entre validação técnica e científica. O Capítulo 5 passou a explicar a evolução da visão inicial para uma baseline V1 fechada. O Capítulo 6 foi consolidado como capítulo de implementação, cobrindo contratos, eventos, pipeline durável, validação, elegibilidade, risco, projeções, API, alertas e observabilidade. O Capítulo 7 passou a documentar a evidência C7, incluindo pipeline, projeções, API, alert policy, testes e classificação de erros históricos. O Capítulo 8 foi reduzido para uma conclusão curta com contributos, limitações e trabalho futuro.

Foi feita ainda uma passagem para acrescentar citações. As referências foram concentradas sobretudo no Capítulo 2, onde existe maior necessidade de enquadramento externo sobre incêndios, fatores meteorológicos, combustível, secura persistente, FWI, KBDI, IPMA, EFFIS, dados territoriais e monitorização. Também foram acrescentadas citações mínimas nos capítulos técnicos quando havia claims externos sobre mensageria, acknowledgements, idempotência, transactional outbox, sistemas oficiais e validação científica futura.

Foram tratados vários problemas de LaTeX e paginação. Entre eles: páginas em branco entre capítulos, excesso de espaço antes das referências, problemas com `minitoc`, erro no contador `mtc`, lista de acrónimos vazia, duplicação de acrónimos por uso simultâneo de `glossaries` e `\input`, erros de tabela por caracteres especiais, bibliografia sem ciclo completo BibTeX e necessidade de reduzir a página de referências. Para controlar melhor a paginação, a lista automática de acrónimos foi substituída por uma tabela manual compacta.

O relatório atual deve ser entendido como uma versão provisória, mas coerente, da documentação da V1. Serve para ter uma base apresentável e alinhada com o que foi implementado até à baseline V1. Não deve ser tratado como relatório final definitivo do projeto.

Permanecem limitações importantes no relatório. A principal é a falta de imagens e diagramas. O documento ainda é demasiado textual e beneficiaria de diagramas sobre a arquitetura geral, a pipeline de processamento, os schemas PostgreSQL, a política de alertas e a validação técnica. Também ainda não explica com todo o detalhe desejável o âmbito global do NatureProtector, ou seja, a diferença entre o projeto completo, o módulo de prevenção, a V1, a V2 e fases futuras.

Outra limitação importante é o desalinhamento temporal face ao repositório. O relatório documenta a V1, mas o projeto já avançou para trabalho próximo da V2, nomeadamente com integração e exposição de FWI, KBDI, Portuguese Context Proxy, melhoria da fórmula NatureProtector, componentes de secura, histórico diário, comparação com índices e reforço da proveniência. Assim, o relatório atual é útil como fecho da V1, mas já não representa tudo o que existe ou está a ser estabilizado no repositório.

A formulação correta para esta fase é, portanto, que o relatório atual é uma base provisória da V1: suficientemente coerente para documentar a baseline técnica, mas ainda incompleta como relatório final do projeto NatureProtector.

Nesta fase foram também preparados artefactos complementares para entrega e discussão com os professores: o documento de organização do repositório e o poster do projeto. Estes documentos servem objetivos diferentes do relatório: o documento de organização explica a estrutura e execução do repositório, enquanto o poster sintetiza visualmente o projeto para apresentação.

## 12A. Fecho do caminho clone-to-run e preparação da baseline para beta

Depois da consolidação inicial do setup local, foi feita uma validação mais próxima do cenário de um utilizador que clona o repositório pela primeira vez. O objetivo foi confirmar que o projeto não dependia apenas de conhecimento tácito ou de estado local já preparado, mas que podia ser executado seguindo uma sequência documentada.

Para simular esse cenário, foram parados processos locais do projeto, removidos containers e volumes Docker da baseline, apagado o ficheiro `.env` local, removido estado runtime local e limpos artefactos ignorados pelo Git. Esta validação permitiu perceber melhor que o caminho correto de primeira execução não deve depender de `dotnet-ef` como passo obrigatório, porque essa ferramenta pode não estar instalada no ambiente do utilizador.

Ficou estabilizado que o caminho principal de setup deve ser:

```powershell
Copy-Item .env.example .env

.\infra\scripts\up.ps1

.\scripts\postgres\bootstrap-control-plane.ps1

.\scripts\setup\Test-LocalBaseline.ps1 -InfrastructureOnly

cd .\webUI
npm ci
cd ..

powershell -ExecutionPolicy Bypass -File .\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

Com esta validação, foi clarificado que o bootstrap PostgreSQL é o mecanismo principal para preparar a base de dados local. O `dotnet-ef` ficou reservado para validação avançada ou desenvolvimento, e não como requisito normal para executar a baseline.

Também foi confirmado que, num clone limpo, a pasta `webUI/node_modules` não existe. Ao tentar arrancar o runtime sem instalar dependências frontend, o Vite falhava com a mensagem `'vite' is not recognized`. Por isso, o setup passou a incluir explicitamente o passo `npm ci` dentro da pasta `webUI`, antes de arrancar o runtime local.

O script `start-local-runtime.ps1` foi ajustado para tornar esta falha mais clara. Em vez de esperar 60 segundos pela porta `5173` sem explicar a causa, o script passou a verificar se as dependências da webUI existem e a indicar diretamente que é necessário correr `cd .\webUI; npm ci; cd ..`.

Foi ainda validado o comportamento do login local. Em ambiente de desenvolvimento, a UI pode ser acedida com:

* utilizador: `admin`;
* password: `admin123`.

Este fluxo não foi tratado como autenticação de produção, mas como mecanismo local de desenvolvimento e demonstração. A documentação passou a indicar que estas credenciais são apenas para a baseline local.

Durante esta fase, foi também analisado o comportamento do `Test-LocalBaseline.ps1 -Full`. Verificou-se que esse modo pode reportar falha no endpoint da Backoffice API com erro `401 Unauthorized`, porque alguns endpoints estão protegidos por autenticação. Esse resultado não invalida a baseline quando a infraestrutura está funcional, a UI responde, o login local funciona e o Run Orchestrator consegue executar runs. Por isso, a validação recomendada para primeira instalação passou a ser `Test-LocalBaseline.ps1 -InfrastructureOnly`, deixando o modo `Full` como diagnóstico adicional.

## 12B. Validação final do Run Orchestrator e evidência runtime

Após a limpeza do ambiente e a reconstrução da baseline, foi executada uma nova validação do `scenario_b` através do fluxo atual de execução. A run ficou registada em `control.simulation_runs` com `StartedAt`, `EndedAt` e `Status = 3`, confirmando que a execução terminou corretamente.

A validação SQL confirmou:

* `processing_attempts = 30`;
* `Outcome = 1`;
* `ErrorCode` vazio;
* `risk_assessments = 30`;
* score mínimo aproximadamente `0.4178`;
* score máximo aproximadamente `0.4482`.

Foi ainda confirmado que, depois da run, não existia processo `NatureProtector.Simulator.Host` ativo. Isto resolveu uma preocupação anterior sobre o possível comportamento pendurado do simulador depois de uma execução lançada pelo orquestrador.

Esta evidência confirmou que o caminho nominal da demo está funcional: infraestrutura ativa, base de dados inicializada, webUI acessível, login local funcional, Run Orchestrator operacional, eventos processados, avaliações de risco geradas e simulador encerrado após a run.

Permaneceu como reserva a necessidade de manter evidência recente do `scenario_c`, caso este cenário seja usado como parte central da demo para demonstrar degradações operacionais. O `scenario_b` ficou validado como fluxo nominal; o `scenario_c` deve ser validado quando for necessário apresentar a comparação B/C ou demonstrar perfis de erro.

## 12C. Ajustes finais de documentação, README e limitações operacionais

Na fase final, o `README.md` foi revisto para deixar de apresentar instruções antigas ou ambíguas de arranque manual dos hosts. O fluxo principal passou a apontar para o documento de setup local, evitando duplicar instruções e reduzindo o risco de o utilizador seguir caminhos desatualizados.

O documento `docs/setup/local-baseline-setup.md` foi atualizado para refletir o caminho real de execução. Foram acrescentadas instruções sobre:

* criação do `.env` a partir do `.env.example`;
* utilização do token local de desenvolvimento já presente no `.env.example`;
* arranque da infraestrutura com `up.ps1`;
* bootstrap PostgreSQL;
* validação com `Test-LocalBaseline.ps1 -InfrastructureOnly`;
* instalação das dependências da webUI com `npm ci`;
* arranque do runtime local;
* login `admin` / `admin123`;
* execução de `scenario_b` no Run Orchestrator;
* validação por queries SQL;
* troubleshooting de Docker, PostgreSQL, InfluxDB, Grafana, webUI, erro `vite is not recognized`, erro `401` e processos locais pendurados.

Foi também documentada uma limitação operacional importante: quando o runtime local está ativo, os processos `NatureProtector.Backoffice.Api` e `NatureProtector.Prevention.Host` podem bloquear DLLs em `bin\Debug\net9.0`. Nessa situação, o `dotnet build` pode falhar não por erro de código, mas porque o Windows não consegue substituir ficheiros em uso. A solução documentada foi confirmar e parar os processos locais antes de compilar.

Ficou também registado que, antes de correr a suite de testes completa, é recomendável subir a infraestrutura com:

```powershell
.\infra\scripts\up.ps1
```

Isto é necessário porque alguns testes de API ou integração dependem dos serviços locais da baseline, como PostgreSQL, RabbitMQ e restantes dependências configuradas.

## 12D. Evidência das bases de dados PostgreSQL e InfluxDB

Foi iniciada a recolha estruturada de evidência sobre o estado das bases de dados. Para PostgreSQL, foram exportadas informações sobre:

* tabelas existentes;
* colunas e tipos;
* constraints;
* índices;
* contagens por tabela;
* amostras de dados;
* dump apenas do schema.

Esta evidência cobre os schemas `control`, `pipeline` e `projection`, permitindo demonstrar a separação entre configuração operacional, processamento de eventos e projeções de risco.

Foi também preparado o método de recolha equivalente para InfluxDB, com o objetivo de documentar a base temporal `np_telemetry`, as tabelas/measurements existentes, colunas, tipos, contagens e amostras. Ao contrário do PostgreSQL, o InfluxDB é tratado como suporte de telemetria e observabilidade, não como fonte primária do estado funcional da pipeline.

Durante esta análise, ficou reforçada a distinção entre os papéis das duas bases de dados:

* PostgreSQL é a base principal para controlo, pipeline, projeções, risco e alertas;
* InfluxDB suporta telemetria e observabilidade temporal da baseline local.

## 12E. Preparação dos artefactos de entrega

Para além do trabalho técnico no repositório, foi preparado material de entrega e comunicação.

Foi produzida uma primeira versão do relatório do projeto. Esta versão foi assumida como provisória e ainda incompleta, mas suficientemente coerente para apresentar aos orientadores uma base de discussão. Foi reconhecido que o relatório ainda tem limitações, nomeadamente a ausência de imagens, diagramas e elementos visuais. Também ficou claro que o relatório principal deve beneficiar da criação de anexos técnicos, para que os detalhes mais extensos possam ser deslocados para documentação complementar e o corpo principal do relatório se concentre melhor na narrativa, compreensão do problema, solução desenvolvida, decisões tomadas e resultados obtidos.

Foi também preparado o documento de organização do repositório, com o objetivo de explicar como o projeto está estruturado, como aceder ao repositório, que pastas existem, como correr a baseline local e quais são as principais notas de execução e validação.

Além disso, foi criado o poster do projeto, funcionando como material visual de síntese para apresentação. O poster complementa o relatório e a demonstração, apresentando de forma mais direta o problema, a proposta, a arquitetura geral, os principais componentes e a validação.

Foi ainda preparada uma mensagem formal para enviar aos professores, acompanhando a primeira versão do relatório e o documento de organização. Nessa mensagem foi explicado que o relatório ainda é uma primeira versão, que a tarefa foi subestimada inicialmente e que seria importante ter maior acompanhamento dos professores nesta fase, sobretudo na estrutura, nível de detalhe e forma de apresentação do projeto.

## 12F. Revisão final antes da beta/demo

Antes da entrega beta/demo, foi realizada uma revisão final leve ao estado do projeto. O objetivo não foi abrir nova fase de desenvolvimento, mas avaliar readiness: Git, setup, README, scripts principais, infraestrutura, documentação e evidência runtime.

A revisão final concluiu que o estado mais adequado era `Pronto com reservas`. Não foram identificados P0 ativos. O caminho nominal da demo estava provado com `scenario_b`, o setup clone-to-run estava documentado e a infraestrutura local podia ser reconstruída a partir de um ambiente limpo.

As reservas principais ficaram associadas a:

* necessidade de evidência recente do `scenario_c`, se este for usado na demo;
* decisão sobre versionar, resumir ou ignorar exports completos de evidência das bases de dados;
* manutenção da documentação de setup alinhada com os scripts reais;
* limitação operacional de DLLs bloqueadas quando se tenta compilar com runtime local ativo.

Foi decidido não abrir alterações estruturais nesta fase. A solução robusta para evitar DLLs bloqueadas seria correr os hosts a partir de uma pasta `publish` isolada por execução, mas isso ficou fora do âmbito imediato por falta de tempo. Para a beta, a limitação foi documentada com comandos de diagnóstico e paragem dos processos locais.

## Atualização aos próximos passos

Alguns próximos passos anteriormente listados foram entretanto concluídos ou parcialmente fechados. Em particular:

* o caminho de setup local foi validado com infraestrutura limpa;
* o uso obrigatório de `dotnet-ef` foi removido do fluxo principal;
* o bootstrap PostgreSQL passou a ser o caminho recomendado para preparar a base de dados;
* o passo `npm ci` foi acrescentado como requisito para a webUI num clone novo;
* o login local `admin` / `admin123` foi documentado;
* o `scenario_b` foi validado com 30 processing attempts e 30 risk assessments;
* o comportamento do `Simulator.Host` foi validado, não ficando pendurado após a run;
* a limitação de DLLs bloqueadas por runtime ativo foi documentada;
* o README foi revisto para apontar para o setup atualizado;
* foram preparados o relatório, o documento de organização do repositório e o poster.

Os próximos passos que permanecem relevantes são:

1. Validar uma run recente de `scenario_c` caso a comparação B/C seja usada na demo.
2. Recolher screenshots finais da UI, incluindo score NP, FWI, KBDI e Portuguese Context Proxy.
3. Integrar imagens e diagramas essenciais no relatório.
4. Rever a estrutura de anexos técnicos para aliviar o relatório principal.
5. Decidir que evidência de base de dados deve ser versionada, resumida ou mantida apenas como artefacto local.
6. Rever o relatório com os professores, em especial a estrutura, o nível de detalhe e a separação entre relatório principal e anexos.
7. Continuar a clarificar no relatório a distinção entre V1, V2, validação técnica, comparação metodológica e validação científica futura.
8. Preparar uma explicação curta para a demo sobre a diferença entre score NatureProtector, FWI, KBDI e Portuguese Context Proxy.
9. Manter explícito que FWI, KBDI e Portuguese Context Proxy são componentes candidatos de comparação e proveniência, não produtos oficiais.
10. Planear, numa fase posterior, a execução dos hosts a partir de uma pasta `publish` isolada para evitar bloqueios de DLL durante builds com runtime ativo.

---

## 13. Próximos passos

1. Validar manualmente a versão atual do website depois de reiniciar os processos locais da aplicação.
2. Confirmar que a tab `Run Timings` consome corretamente o endpoint de timings.
3. Corrigir definitivamente qualquer duplicação visual remanescente, como dupla navbar ou botão light/dark duplicado.
4. Garantir que dashboards Grafana por célula usam `d-solo` e `panelId`, mostrando apenas o painel necessário.
5. Garantir que dashboards por célula nunca abrem com `sensor_id` vazio.
6. Melhorar o `Nominal Flow`, mostrando estado e evidência por etapa.
7. Melhorar o `Model & Provenance` para funcionar como matriz de rastreabilidade.
8. Rever textos e acentos da Home.
9. Decidir a consistência linguística da UI.
10. Preparar a UI para RBAC futuro, sem apresentar isso como segurança enquanto não houver enforcement backend.
11. Separar claramente vistas de demo e vistas de developer.
12. Fazer nova run B/C limpa e recolher screenshots para apresentação.
13. Atualizar a documentação técnica para refletir a nova organização do website e os scripts de infraestrutura.
14. Preparar slides que façam o paralelismo entre diagramas, UI e fluxo real do sistema.
15. Simplificar os diagramas finais de apresentação, mantendo poucos blocos e setas com significado claro.
16. Fazer uma validação final em projetor, avaliando legibilidade, densidade visual e necessidade de scroll.
17. Confirmar que a infraestrutura local continua a subir corretamente com `up.ps1` depois do reset destrutivo validado.
18. Evitar versionar evidência que exponha tokens, especialmente outputs de `docker compose config` ou ficheiros em `data/runtime`.
19. Validar `Setup-LocalEnvironment.ps1 -StartRuntime -OpenBrowser` num ambiente limpo, sem processos antigos nas portas `5254` e `5173`.
20. Decidir se `Install-LocalPrerequisites.ps1` deve permanecer apenas como guia/WhatIf ou se deve suportar instalação real com `-InstallMissing -Yes`.
21. Garantir que a documentação de setup distingue claramente primeira utilização, uso diário, validação e reset destrutivo.

22. Validar uma run limpa do `scenario_b` depois da correção de `daily_cell_state`, garantindo `Quarantined = 0` e existência de risk assessments.
23. Validar uma run limpa do `scenario_c` com perfis de degradação, garantindo que `missing-readings`, `noise`, `lag/delay`, `outlier` e `stuck-value` são apresentados com distinção entre pedido, resolvido, aplicado e observado.
24. Corrigir ou explicar a diferença entre `Attempt count`, `Successful attempts`, `Failed attempts` e `Quarantined attempts`, acrescentando uma categoria `Other`, `Pending` ou `Unknown` quando a soma não fecha.
25. Confirmar se os campos `FWI calculated / reference` e `KBDI calculated / reference` estão semanticamente corretos e não invertidos na UI.
26. Garantir que a UI apresenta sempre o `Portuguese Context Proxy` como proxy candidato e não como metodologia oficial IPMA/RCM/PIR.
27. Melhorar a documentação da fórmula NatureProtector, incluindo `BaseRisk`, `AdjustedScore`, `M/D/T`, `H/F/G`, `C/I`, dominant driver, calculation status e limitations.
28. Atualizar a matriz entre pesquisa e implementação, incluindo o estado atual de FWI, KBDI, Portuguese Context Proxy, percentil local de FWI, KBDI com histórico antecedente e degradações.
29. Preparar uma explicação curta para a apresentação sobre a diferença entre `Current Area Score` e `Latest NP Assessment`.
30. Preparar uma explicação curta para a apresentação sobre a diferença entre FWI, KBDI, score NatureProtector e Portuguese Context Proxy.
31. Manter explícito que FWI e KBDI são índices de comparação e proveniência, não validação científica final do NatureProtector.
32. Melhorar o suporte a histórico diário para KBDI, permitindo que o índice seja calculado com estado antecedente mais defensável.
33. Preparar, se existirem dados suficientes, uma futura distribuição histórica local para calcular percentil/anomalia de FWI por área ou época do ano.
34. Rever as limitações persistidas em `daily_cell_state`, garantindo que continuam úteis para auditoria e não apenas como texto acumulado sem estrutura.
35. Melhorar a metadata de quarentena para incluir `SqlState`, `MessageText`, tabela, coluna e constraint quando a falha vier de PostgreSQL.
36. Fazer nova recolha de screenshots para apresentação depois de confirmar uma run limpa com score NP, FWI, KBDI e Portuguese Context Proxy visíveis.
37. Garantir que o relatório compila de forma estável com acrónimos, referências e bibliografia ativas.
38. Confirmar que o relatório final permanece abaixo do limite de páginas definido, idealmente até 40 páginas.
39. Rever visualmente a tabela de acrónimos e símbolos, garantindo que não ocupa espaço excessivo nem aparece duplicada.
40. Confirmar que todas as citações usadas nos capítulos têm chave válida no ficheiro `References.bib`.
41. Remover entradas bibliográficas antigas, genéricas ou desalinhadas com o NatureProtector.
42. Acrescentar imagens e diagramas essenciais ao relatório, especialmente arquitetura geral, pipeline de processamento, schemas PostgreSQL, política de alertas e evidência runtime.
43. Rever o Capítulo 3 para explicar melhor o âmbito global do NatureProtector, distinguindo projeto completo, módulo de prevenção, V1, V2 e trabalho futuro.
44. Acrescentar uma nota clara de que o relatório atual documenta a V1, embora o repositório já tenha trabalho posterior associado à V2.
45. Fazer uma revisão final de terminologia, garantindo consistência entre `âmbito`, `em tempo de execução`, `pipeline`, `baseline`, `validação técnica`, `validação científica`, `parâmetros candidatos` e `projeções operacionais`.
46. Verificar se o relatório ainda contém vestígios de conteúdo antigo ou desalinhado com o projeto atual.
47. Avaliar se o relatório deve incluir uma pequena secção ou nota sobre a evolução V1 → V2, sem tentar documentar toda a V2 em detalhe.
48. Fazer uma última compilação limpa do PDF e arquivar a versão gerada como evidência documental da baseline V1.
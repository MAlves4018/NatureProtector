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

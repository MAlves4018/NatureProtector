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

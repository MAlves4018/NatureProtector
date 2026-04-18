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

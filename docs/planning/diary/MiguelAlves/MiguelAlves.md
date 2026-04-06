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

workspace "NatureProtector" "Modelo C4 da implementação atual do NatureProtector." {

    model {
        operator = person "Operador de backoffice" "Consulta a API e os dashboards para observar o estado da runtime."
        developer = person "Programador" "Executa os hosts localmente e opera a baseline Docker Compose."

        natureProtector = softwareSystem "NatureProtector" "Plataforma suportada pelo repositório para simulação, prevenção, plano de controlo e observabilidade." {
            backofficeApi = container "Backoffice.Api" "API ASP.NET Core que expõe consultas sobre o plano de controlo e o estado operacional." "C# / ASP.NET Core"
            postgresBootstrap = container "Postgres.Bootstrap" "Utilitário de linha de comandos que materializa a baseline do plano de controlo em PostgreSQL." "C# / .NET Console"
            simulatorHost = container "Simulator.Host" "Worker que resolve contexto de simulação, gera leituras e publica eventos." "C# / .NET Worker"
            preventionHost = container "Prevention.Host" "Worker que consome leituras, avalia risco, gere retries e mantém projeções operacionais." "C# / .NET Worker" {
                preventionWorker = component "PreventionWorker" "Consumidor RabbitMQ que valida envelopes, guarda ou rejeita mensagens e dispara o processamento." "NatureProtector.Prevention.Host.PreventionWorker"
                inboxRetryWorker = component "InboxRetryWorker" "Worker de polling que reentra retries devidos no fluxo de processamento." "NatureProtector.Prevention.Host.Processing.InboxRetryWorker"
                readingEventProcessingService = component "ReadingEventProcessingService" "Coordena conclusão, agendamento de retry e transições para quarentena." "NatureProtector.Prevention.Host.Processing.ReadingEventProcessingService"
                readingRiskPipeline = component "ReadingRiskPipeline" "Executa a sequência de leitura aceite, avaliação de risco, snapshot e projeção." "NatureProtector.Prevention.Host.Processing.ReadingRiskPipeline"
                postgresReadingEventInbox = component "PostgresReadingEventInbox" "Implementação durável da inbox apoiada por PostgreSQL." "NatureProtector.Prevention.Host.Processing.PostgresReadingEventInbox"
                simpleRiskScoringService = component "SimpleRiskScoringService" "Constrói avaliações de risco a partir de leituras aceites." "NatureProtector.Prevention.Risk.SimpleRiskScoringService"
                postgresAcceptedReadingRepository = component "PostgresAcceptedReadingRepository" "Persiste logs de leituras aceites." "NatureProtector.Prevention.Host.Persistence.PostgresAcceptedReadingRepository"
                postgresRiskAssessmentRepository = component "PostgresRiskAssessmentRepository" "Persiste e consulta logs de avaliações de risco." "NatureProtector.Prevention.Host.Persistence.PostgresRiskAssessmentRepository"
                postgresAreaRiskSnapshotRepository = component "PostgresAreaRiskSnapshotRepository" "Persiste snapshots agregados de risco por área." "NatureProtector.Prevention.Host.Persistence.PostgresAreaRiskSnapshotRepository"
                postgresAreaOperationalProjectionStore = component "PostgresAreaOperationalProjectionStore" "Mantém projeções consultáveis por célula, área e alerta." "NatureProtector.Prevention.Host.Projection.PostgresAreaOperationalProjectionStore"
                influxWriteService = component "InfluxWriteService" "Escreve dados temporais de observabilidade." "NatureProtector.Infrastructure.Influx.Services.InfluxWriteService"
            }
            postgres = container "PostgreSQL" "Guarda plano de controlo, runs de simulação, inbox, tentativas, quarentena, logs e projeções operacionais." "PostgreSQL 16"
            influxdb = container "InfluxDB" "Guarda leituras aceites, avaliações de risco e snapshots de área para observabilidade temporal." "InfluxDB 3"
            rabbitmq = container "RabbitMQ" "Transporta eventos de leitura entre o simulador e a runtime de prevenção." "RabbitMQ 4"
            grafana = container "Grafana" "Consulta o InfluxDB e apresenta dashboards de apoio à observabilidade." "Grafana 12"
        }

        operator -> backofficeApi "Consulta plano de controlo e estado operacional" "HTTP/JSON"
        operator -> grafana "Consulta dashboards de observabilidade" "Browser"
        developer -> postgresBootstrap "Materializa a baseline do plano de controlo" "PowerShell / dotnet run"
        developer -> simulatorHost "Executa simulações localmente" "dotnet run"
        developer -> preventionHost "Executa a runtime de prevenção localmente" "dotnet run"
        developer -> backofficeApi "Executa a API localmente" "dotnet run"
        developer -> grafana "Abre dashboards" "Browser"

        postgresBootstrap -> postgres "Cria schema e carrega configuração, área, grelha, sensores, cenários e datasets" "EF Core / Npgsql"
        simulatorHost -> postgres "Lê área, cenário e sensores; grava runs de simulação" "EF Core / Npgsql"
        simulatorHost -> rabbitmq "Publica eventos SensorReadingProduced" "AMQP"
        preventionHost -> rabbitmq "Consome eventos SensorReadingProduced" "AMQP"
        preventionHost -> postgres "Persiste inbox, tentativas, rejeições, quarentena, leituras aceites, avaliações, snapshots e projeções" "EF Core / Npgsql"
        preventionHost -> influxdb "Escreve medições de observabilidade" "InfluxDB client"
        backofficeApi -> postgres "Lê plano de controlo e estado projetado" "EF Core / Npgsql"
        grafana -> influxdb "Consulta medições" "HTTP"

        preventionWorker -> rabbitmq "Consome eventos"
        preventionWorker -> postgresReadingEventInbox "Guarda ou rejeita mensagens recebidas"
        preventionWorker -> readingEventProcessingService "Encaminha eventos aceites"
        inboxRetryWorker -> postgresReadingEventInbox "Procura retries devidos"
        inboxRetryWorker -> readingEventProcessingService "Reprocessa retries devidos"
        readingEventProcessingService -> readingRiskPipeline "Executa a pipeline de processamento"
        readingEventProcessingService -> postgresReadingEventInbox "Conclui, reagenda ou coloca eventos em quarentena"
        readingRiskPipeline -> simpleRiskScoringService "Constrói avaliações de risco"
        readingRiskPipeline -> postgresAcceptedReadingRepository "Persiste leituras aceites"
        readingRiskPipeline -> postgresRiskAssessmentRepository "Persiste e consulta avaliações"
        readingRiskPipeline -> postgresAreaRiskSnapshotRepository "Persiste snapshots"
        readingRiskPipeline -> postgresAreaOperationalProjectionStore "Atualiza projeções consultáveis"
        readingRiskPipeline -> influxWriteService "Publica dados de observabilidade"
        postgresReadingEventInbox -> postgres "Guarda inbox, tentativas, rejeições e quarentena"
        postgresAcceptedReadingRepository -> postgres "Escreve logs de projeção"
        postgresRiskAssessmentRepository -> postgres "Escreve e consulta logs de projeção"
        postgresAreaRiskSnapshotRepository -> postgres "Escreve logs de snapshots"
        postgresAreaOperationalProjectionStore -> postgres "Escreve estado por célula, área e alerta"
        influxWriteService -> influxdb "Escreve medições"

        local = deploymentEnvironment "Desenvolvimento local" {
            workstation = deploymentNode "Posto de desenvolvimento" "Máquina local usada para desenvolvimento, validação e demonstração." "Windows / PowerShell" {
                backofficeApiInstance = containerInstance backofficeApi
                postgresBootstrapInstance = containerInstance postgresBootstrap
                simulatorHostInstance = containerInstance simulatorHost
                preventionHostInstance = containerInstance preventionHost

                compose = deploymentNode "Baseline Docker Compose" "Infraestrutura local iniciada a partir de docker-compose.yml." "Docker" {
                    rabbitmqInstance = containerInstance rabbitmq
                    postgresInstance = containerInstance postgres
                    influxdbInstance = containerInstance influxdb
                    grafanaInstance = containerInstance grafana
                }
            }
        }
    }

    views {
        systemContext natureProtector "contexto-sistema" {
            include operator
            include developer
            include natureProtector
            autolayout lr
        }

        container natureProtector "runtime-atual" {
            include operator
            include developer
            include backofficeApi
            include postgresBootstrap
            include simulatorHost
            include preventionHost
            include postgres
            include influxdb
            include rabbitmq
            include grafana
            autolayout lr
        }

        component preventionHost "componentes-prevention-host" {
            include preventionWorker
            include inboxRetryWorker
            include readingEventProcessingService
            include readingRiskPipeline
            include postgresReadingEventInbox
            include simpleRiskScoringService
            include postgresAcceptedReadingRepository
            include postgresRiskAssessmentRepository
            include postgresAreaRiskSnapshotRepository
            include postgresAreaOperationalProjectionStore
            include influxWriteService
            include postgres
            include influxdb
            include rabbitmq
            autolayout lr
        }

        deployment natureProtector "Desenvolvimento local" "baseline-local-docker-compose" {
            include *
            autolayout lr
        }

        styles {
            element "Person" {
                shape person
            }

            element "Container" {
                shape roundedbox
            }

            element "Database" {
                shape cylinder
            }
        }
    }
}

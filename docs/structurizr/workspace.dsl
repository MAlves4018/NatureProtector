workspace "NatureProtector" "Current architecture, operations and deployment model verified against the 2026-06-28 repository snapshot." {
  model {
    publicUser = person "Public reader" "Views public context and limitations."
    operator = person "Runtime operator" "Runs simulations and observes risk/pipeline state."
    qa = person "QA operator" "Runs closed quality suites and evidence campaigns."
    ops = person "Cloud operations" "Plans and operates staging."
    approver = person "Release approver" "Reviews production, rollback and destroy gates."
    admin = person "Application administrator" "Manages users, roles and application configuration."

    github = softwareSystem "GitHub Actions" "Authoritative CI, quality, release and dispatch workflows."
    gcp = softwareSystem "Google Cloud Platform" "Artifact Registry, GKE/Autopilot, Cloud Deploy, managed identities and environment resources."

    np = softwareSystem "NatureProtector" "Experimental auditable platform for controlled environmental simulation and technical risk assessment." {
      web = container "webUI" "Role-aware React/Vite interface with Mission Control, simulation, quality, evidence, deployment, cloud and approval views." "React / TypeScript"
      api = container "Backoffice.Api" "ASP.NET Core API and server-side authorisation authority." "C# / ASP.NET Core" {
        userPlane = component "User and role plane" "Authentication, role assignments and capability profile."
        runtimePlane = component "Runtime control plane" "Areas, scenarios, runs, diagnostics and runtime operations."
        operationsPlane = component "Engineering operations plane" "Closed operation catalog, store, approvals, confirmations and dispatch."
        cloudCatalog = component "Cloud environment catalog" "Repository-declared environment/resource inventory with explicit observed-state limits."
      }
      simulator = container "Simulator.Host" "Creates deterministic/degraded readings from scenario and run contracts." ".NET Worker"
      prevention = container "Prevention.Host" "Consumes readings, manages durable processing and projects eligible candidate risk." ".NET Worker"
      bootstrap = container "Postgres.Bootstrap" "Creates the local control baseline." ".NET Console"
      postgres = container "PostgreSQL" "Principal system of record for control, runs, inbox, attempts, assessments and projections." "PostgreSQL 16" "Database"
      rabbit = container "RabbitMQ" "Sensor-reading event transport." "RabbitMQ" "Message Broker"
      influx = container "InfluxDB" "Temporal observability data." "InfluxDB 3" "Database"
      grafana = container "Grafana" "Temporal dashboards." "Grafana"
      evidenceStore = container "Evidence store" "Operation artifacts, hashes, manifests and limitations; filesystem/GCS depending on environment." "Files / Cloud Storage"
    }

    publicUser -> web "Reads public context"
    operator -> web "Runs scenarios and observes runtime"
    qa -> web "Runs quality/evidence"
    ops -> web "Plans and operates staging"
    approver -> web "Reviews and approves gates"
    admin -> web "Manages users and roles"
    web -> api "HTTP/JSON; evaluated capability profile"
    api -> postgres "Reads/writes identity, control, runtime and operation records"
    api -> github "Dispatches closed workflows with server-side identity"
    github -> api "Authenticated status/artifact callback"
    github -> gcp "WIF-authenticated release/deploy/cloud operations"
    github -> evidenceStore "Publishes artifacts, manifests and hashes"
    api -> evidenceStore "Indexes/streams authorised evidence"
    simulator -> postgres "Reads scenarios and records runs"
    simulator -> rabbit "Publishes SensorReadingProduced"
    prevention -> rabbit "Consumes reading events"
    prevention -> postgres "Durable inbox, attempts, assessments and projections"
    prevention -> influx "Writes temporal observability"
    grafana -> influx "Queries telemetry"
    api -> postgres "Reads projected state"

    local = deploymentEnvironment "Local" {
      workstation = deploymentNode "Developer workstation" "Windows/PowerShell supported path" {
        webI = containerInstance web
        apiI = containerInstance api
        simulatorI = containerInstance simulator
        preventionI = containerInstance prevention
        bootstrapI = containerInstance bootstrap
        compose = deploymentNode "Docker Compose" {
          postgresI = containerInstance postgres
          rabbitI = containerInstance rabbit
          influxI = containerInstance influx
          grafanaI = containerInstance grafana
        }
      }
    }
    staging = deploymentEnvironment "Staging" {
      stagingProject = deploymentNode "Isolated GCP staging project" "Qualification before production" {
        stagingCluster = deploymentNode "GKE/Autopilot" {
          webS = containerInstance web
          apiS = containerInstance api
          simulatorS = containerInstance simulator
          preventionS = containerInstance prevention
          rabbitS = containerInstance rabbit
        }
        postgresS = containerInstance postgres
        evidenceS = containerInstance evidenceStore
      }
    }
    production = deploymentEnvironment "Production" {
      productionProject = deploymentNode "Isolated GCP production project" "Promotion only after staging evidence and approval" {
        productionCluster = deploymentNode "GKE/Autopilot" {
          webP = containerInstance web
          apiP = containerInstance api
          simulatorP = containerInstance simulator
          preventionP = containerInstance prevention
          rabbitP = containerInstance rabbit
        }
        postgresP = containerInstance postgres
        evidenceP = containerInstance evidenceStore
      }
    }
  }

  views {
    systemContext np "system-context-current" { include *; autolayout lr }
    container np "containers-current" { include *; autolayout lr }
    component api "api-control-planes" { include *; autolayout lr }
    dynamic np "runtime-reading-flow" "Nominal reading processing" {
      operator -> web "1. Requests run"
      web -> api "2. Creates run"
      api -> simulator "3. Launches/dispatches run"
      simulator -> rabbit "4. Publishes reading"
      prevention -> rabbit "5. Consumes reading"
      prevention -> postgres "6. Persists inbox, result and projection"
      web -> api "7. Reads run/risk/evidence"
      autolayout lr
    }
    dynamic np "engineering-operation-flow" "Auditable engineering operation" {
      qa -> web "1. Selects closed operation"
      web -> api "2. Requests operation"
      api -> postgres "3. Records and validates"
      api -> github "4. Dispatches workflow"
      github -> evidenceStore "5. Publishes artifacts"
      github -> api "6. Authenticated callback"
      web -> api "7. Reads timeline/evidence"
      autolayout lr
    }
    deployment np "Local" "deployment-local" { include *; autolayout lr }
    deployment np "Staging" "deployment-staging" { include *; autolayout lr }
    deployment np "Production" "deployment-production" { include *; autolayout lr }
    styles {
      element "Person" { shape person }
      element "Database" { shape cylinder }
      element "Message Broker" { shape pipe }
      element "Container" { shape roundedbox }
    }
  }
}

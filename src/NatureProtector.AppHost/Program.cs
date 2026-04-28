var builder = DistributedApplication.CreateBuilder(args);
// Em desenvolvimento
// A baseline Aspire coexiste com docker-compose e fixa portas equivalentes
// para facilitar a alternância entre os dois modos de desenvolvimento local.
var postgres = builder.AddContainer("postgres", "postgres:16")
    .WithEnvironment("POSTGRES_DB", "natureprotector")
    .WithEnvironment("POSTGRES_USER", "np")
    .WithEnvironment("POSTGRES_PASSWORD", "np_dev_pass")
    .WithEndpoint(name: "tcp", port: 5432, targetPort: 5432, isProxied: false);

var rabbitmq = builder.AddContainer("rabbitmq", "rabbitmq:4.0.6-management")
    .WithEnvironment("RABBITMQ_DEFAULT_USER", "np")
    .WithEnvironment("RABBITMQ_DEFAULT_PASS", "np_dev_pass")
    .WithEndpoint(name: "amqp", port: 5672, targetPort: 5672, isProxied: false)
    .WithEndpoint(name: "management", port: 15672, targetPort: 15672, isProxied: false);

var influxdb = builder.AddContainer("influxdb", "influxdb:3.7.0-core")
    .WithEnvironment("INFLUXDB_NODE_ID", "node1")
    .WithEndpoint(name: "http", port: 8181, targetPort: 8181, isProxied: false);

var grafana = builder.AddContainer("grafana", "grafana/grafana:12.1-ubuntu")
    .WithEnvironment("GF_SECURITY_ADMIN_USER", "admin")
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
    .WithEnvironment("INFLUXDB_URL", "http://localhost:8181")
    .WithEndpoint(name: "http", port: 3000, targetPort: 3000, isProxied: false);

builder.AddProject<Projects.NatureProtector_Backoffice_Api>("backoffice-api")
    .WithEnvironment("POSTGRES_HOST", "localhost")
    .WithEnvironment("POSTGRES_PORT", "5432")
    .WithEnvironment("POSTGRES_DB", "natureprotector")
    .WithEnvironment("POSTGRES_USER", "np")
    .WithEnvironment("POSTGRES_PASSWORD", "np_dev_pass");

builder.AddProject<Projects.NatureProtector_Prevention_Host>("prevention-host")
    .WithEnvironment("POSTGRES_HOST", "localhost")
    .WithEnvironment("POSTGRES_PORT", "5432")
    .WithEnvironment("POSTGRES_DB", "natureprotector")
    .WithEnvironment("POSTGRES_USER", "np")
    .WithEnvironment("POSTGRES_PASSWORD", "np_dev_pass")
    .WithEnvironment("RabbitMq__HostName", "localhost")
    .WithEnvironment("RabbitMq__Port", "5672")
    .WithEnvironment("RabbitMq__UserName", "np")
    .WithEnvironment("RabbitMq__Password", "np_dev_pass")
    .WithEnvironment("InfluxDb__Url", "http://localhost:8181");

builder.AddProject<Projects.NatureProtector_Simulator_Host>("simulator-host")
    .WithEnvironment("POSTGRES_HOST", "localhost")
    .WithEnvironment("POSTGRES_PORT", "5432")
    .WithEnvironment("POSTGRES_DB", "natureprotector")
    .WithEnvironment("POSTGRES_USER", "np")
    .WithEnvironment("POSTGRES_PASSWORD", "np_dev_pass")
    .WithEnvironment("RabbitMq__HostName", "localhost")
    .WithEnvironment("RabbitMq__Port", "5672")
    .WithEnvironment("RabbitMq__UserName", "np")
    .WithEnvironment("RabbitMq__Password", "np_dev_pass");

builder.Build().Run();

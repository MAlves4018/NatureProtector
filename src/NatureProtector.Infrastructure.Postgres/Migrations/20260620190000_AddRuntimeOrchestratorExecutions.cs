using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NatureProtector.Infrastructure.Postgres.Persistence;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations;

[DbContext(typeof(NatureProtectorControlDbContext))]
[Migration("20260620190000_AddRuntimeOrchestratorExecutions")]
public sealed class AddRuntimeOrchestratorExecutions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE control.runtime_orchestrator_executions (
                execution_id uuid PRIMARY KEY,
                idempotency_key character varying(250) NOT NULL,
                provider character varying(50) NOT NULL,
                provider_operation_name character varying(500),
                provider_execution_name character varying(500),
                state character varying(50) NOT NULL,
                accepted_at timestamp with time zone NOT NULL,
                updated_at timestamp with time zone NOT NULL,
                started_at timestamp with time zone,
                finished_at timestamp with time zone,
                failure_code character varying(150),
                failure_message character varying(4000),
                log_correlation character varying(250) NOT NULL,
                evidence_id character varying(250),
                evidence_location character varying(1000),
                launch_lease_token uuid,
                launch_lease_until timestamp with time zone,
                CONSTRAINT uq_runtime_orchestrator_executions_idempotency UNIQUE (idempotency_key)
            );
            CREATE INDEX ix_runtime_orchestrator_executions_state_updated
                ON control.runtime_orchestrator_executions (state, updated_at);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS control.runtime_orchestrator_executions;");
    }
}

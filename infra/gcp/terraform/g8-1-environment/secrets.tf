locals {
  runtime_secret_ids = toset([
    "postgres-app-password",
    "postgres-migration-password",
    "bootstrap-admin-password",
    "jwt-signing-key",
    "rabbitmq-app-username",
    "rabbitmq-app-password",
    "rabbitmq-tls-certificate",
    "rabbitmq-tls-private-key",
    "rabbitmq-ca-certificate",
    "cloud-sql-server-ca"
  ])
}
resource "google_secret_manager_secret" "runtime" {
  for_each            = var.create_data_plane ? local.runtime_secret_ids : toset([])
  project             = var.project_id
  secret_id           = "np-${var.environment}-${each.value}"
  deletion_protection = var.deletion_protection
  replication {
    auto {}
  }
}

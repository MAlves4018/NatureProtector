# Generated application credentials are ephemeral Terraform values. The Google
# provider receives them only through write-only arguments, so payloads are not
# persisted in Terraform state or plan files. Owner-managed TLS material stays
# outside Terraform and is populated as explicit Secret Manager versions.
ephemeral "random_password" "postgres_migration" {
  length  = 40
  special = false
}

ephemeral "random_password" "postgres_app" {
  length  = 40
  special = false
}

ephemeral "random_password" "bootstrap_admin" {
  length  = 40
  special = false
}

ephemeral "random_password" "jwt_signing" {
  length  = 64
  special = false
}

ephemeral "random_password" "rabbitmq_app" {
  length  = 40
  special = false
}

locals {
  generated_secret_values = {
    "postgres-migration-password" = ephemeral.random_password.postgres_migration.result
    "postgres-app-password"       = ephemeral.random_password.postgres_app.result
    "bootstrap-admin-password"    = ephemeral.random_password.bootstrap_admin.result
    "jwt-signing-key"             = ephemeral.random_password.jwt_signing.result
    "rabbitmq-app-username"       = "np_app"
    "rabbitmq-app-password"       = ephemeral.random_password.rabbitmq_app.result
  }
}

resource "google_secret_manager_secret_version" "generated" {
  for_each               = var.create_data_plane && var.materialize_generated_secrets ? local.generated_secret_values : {}
  secret                 = google_secret_manager_secret.runtime[each.key].id
  secret_data_wo         = each.value
  secret_data_wo_version = var.secret_generation
}

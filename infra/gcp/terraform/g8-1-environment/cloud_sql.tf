resource "google_sql_database_instance" "primary" {
  count               = var.create_data_plane ? 1 : 0
  project             = var.project_id
  name                = var.database_instance_name
  region              = var.region
  database_version    = "POSTGRES_16"
  deletion_protection = var.deletion_protection

  settings {
    tier                        = var.database_tier
    edition                     = var.database_edition
    availability_type           = var.database_availability_type
    deletion_protection_enabled = var.deletion_protection
    disk_type                   = var.database_disk_type
    disk_size                   = var.database_disk_size_gb
    disk_autoresize             = true

    ip_configuration {
      ipv4_enabled    = false
      private_network = google_compute_network.main[0].id
      ssl_mode        = "ENCRYPTED_ONLY"
      server_ca_mode  = "GOOGLE_MANAGED_INTERNAL_CA"
    }

    dynamic "backup_configuration" {
      for_each = var.database_backup_enabled ? [1] : []
      content {
        enabled                        = true
        point_in_time_recovery_enabled = var.database_pitr_enabled
        start_time                     = "02:00"
        transaction_log_retention_days = var.database_pitr_enabled ? 7 : null
        backup_retention_settings {
          retained_backups = var.database_retained_backups
          retention_unit   = "COUNT"
        }
      }
    }

    insights_config {
      query_insights_enabled  = true
      query_string_length     = 2048
      record_application_tags = true
      record_client_address   = false
    }

    maintenance_window {
      day          = 7
      hour         = 3
      update_track = "stable"
    }
  }

  lifecycle {
    ignore_changes = [settings[0].disk_size]

    precondition {
      condition = var.environment != "production" || (
        var.database_availability_type == "REGIONAL" &&
        var.database_disk_type == "PD_SSD" &&
        var.database_backup_enabled &&
        var.database_pitr_enabled &&
        var.database_retained_backups >= 14 &&
        var.deletion_protection
      )
      error_message = "Production requires regional Cloud SQL, PD_SSD, backups, PITR, at least 14 retained backups and deletion protection."
    }
  }
  depends_on = [google_service_networking_connection.private_service_access]
}

resource "google_sql_database" "application" {
  count    = var.create_data_plane ? 1 : 0
  project  = var.project_id
  name     = "natureprotector"
  instance = google_sql_database_instance.primary[0].name
}

resource "google_sql_user" "migration" {
  count               = var.create_data_plane && var.materialize_generated_secrets ? 1 : 0
  project             = var.project_id
  instance            = google_sql_database_instance.primary[0].name
  name                = "np_migration"
  password_wo         = ephemeral.random_password.postgres_migration.result
  password_wo_version = var.secret_generation
  deletion_policy     = "ABANDON"
}

resource "google_sql_user" "application" {
  count               = var.create_data_plane && var.materialize_generated_secrets ? 1 : 0
  project             = var.project_id
  instance            = google_sql_database_instance.primary[0].name
  name                = "np_app"
  password_wo         = ephemeral.random_password.postgres_app.result
  password_wo_version = var.secret_generation
  deletion_policy     = "ABANDON"
}

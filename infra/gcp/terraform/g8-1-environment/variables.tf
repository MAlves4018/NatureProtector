variable "project_id" { type = string }
variable "environment" {
  type = string
  validation {
    condition     = contains(["staging", "production"], var.environment)
    error_message = "environment must be staging or production."
  }
}
variable "region" {
  type    = string
  default = "europe-southwest1"
}
variable "network_cidr" { type = string }
variable "pods_cidr" { type = string }
variable "services_cidr" { type = string }
variable "private_service_access_prefix_length" {
  type    = number
  default = 16
}
variable "cluster_name" { type = string }
variable "database_instance_name" { type = string }
variable "database_tier" {
  type    = string
  default = "db-custom-2-7680"
}
variable "database_disk_size_gb" {
  type    = number
  default = 50
}
variable "database_availability_type" {
  type        = string
  default     = "REGIONAL"
  description = "REGIONAL for production; ZONAL is permitted only for a non-production qualification run."
  validation {
    condition     = contains(["REGIONAL", "ZONAL"], var.database_availability_type)
    error_message = "database_availability_type must be REGIONAL or ZONAL."
  }
}
variable "database_disk_type" {
  type        = string
  default     = "PD_SSD"
  description = "Cloud SQL disk type. Production policy requires PD_SSD."
  validation {
    condition     = contains(["PD_SSD", "PD_HDD"], var.database_disk_type)
    error_message = "database_disk_type must be PD_SSD or PD_HDD."
  }
}
variable "database_backup_enabled" {
  type    = bool
  default = true
}
variable "database_pitr_enabled" {
  type    = bool
  default = true
}
variable "database_retained_backups" {
  type    = number
  default = 14
  validation {
    condition     = var.database_retained_backups >= 1 && floor(var.database_retained_backups) == var.database_retained_backups
    error_message = "database_retained_backups must be a positive integer."
  }
}
variable "cloud_deploy_worker_machine_type" {
  type    = string
  default = "e2-standard-4"
}
variable "cloud_deploy_worker_disk_size_gb" {
  type    = number
  default = 100
  validation {
    condition     = var.cloud_deploy_worker_disk_size_gb >= 50
    error_message = "cloud_deploy_worker_disk_size_gb must be at least 50."
  }
}
variable "deletion_protection" {
  type    = bool
  default = true
}
variable "create_data_plane" {
  type    = bool
  default = false
}
variable "create_edge" {
  type    = bool
  default = false
}
variable "api_service_name" {
  type    = string
  default = "natureprotector-api"
}
variable "frontend_service_name" {
  type    = string
  default = "natureprotector-frontend"
}
variable "managed_certificate_domains" {
  type    = list(string)
  default = []
  validation {
    condition     = !var.create_edge || length(var.managed_certificate_domains) > 0
    error_message = "At least one managed certificate domain is required when the production edge is enabled."
  }
}
variable "owner_creation_confirmation" {
  type      = string
  default   = ""
  sensitive = true
  validation {
    condition = !var.create_data_plane || (
      var.owner_creation_confirmation == "AUTHORIZE_EPHEMERAL_STAGING_APPLY_MAX_20_EUR_TTL_4H"
    )
    error_message = "Data-plane creation requires the exact ephemeral staging authorization for the 20 EUR and 4 hour envelope."
  }
}
variable "platform_project_id" { type = string }
variable "workflow_deployer_service_account" {
  type        = string
  description = "Environment-specific GitHub deployment identity created in the platform project."
}
variable "cloud_deploy_execution_service_account" {
  type        = string
  description = "Cloud Deploy execution identity created in the platform project."
}
variable "simulator_job_name" {
  type    = string
  default = "natureprotector-simulator"
}
variable "runtime_namespace" {
  type    = string
  default = ""
}
variable "monitoring_notification_channels" {
  type    = list(string)
  default = []
  validation {
    condition     = var.environment != "production" || !var.create_data_plane || length(var.monitoring_notification_channels) > 0
    error_message = "Production requires at least one Monitoring notification channel before the data plane can be created."
  }
}
variable "api_availability_goal" {
  type    = number
  default = 0.995
  validation {
    condition     = var.api_availability_goal >= 0.99 && var.api_availability_goal < 1
    error_message = "api_availability_goal must be between 0.99 and 1."
  }
}


variable "materialize_generated_secrets" {
  type        = bool
  default     = false
  description = "Create write-only generated credential versions and matching Cloud SQL users."
  validation {
    condition     = !var.materialize_generated_secrets || var.create_data_plane
    error_message = "Generated credentials require the environment data plane."
  }
}
variable "secret_generation" {
  type        = number
  default     = 1
  description = "Monotonic write-only credential generation used for explicit rotation."
  validation {
    condition     = var.secret_generation >= 1 && floor(var.secret_generation) == var.secret_generation
    error_message = "secret_generation must be a positive integer."
  }
}

variable "platform_project_id" { type = string }
variable "staging_project_id" { type = string }
variable "production_project_id" { type = string }
variable "region" {
  type    = string
  default = "europe-southwest1"
  validation {
    condition     = var.region == "europe-southwest1"
    error_message = "The primary NatureProtector region is Madrid (europe-southwest1)."
  }
}
variable "repository" {
  type    = string
  default = "MAlves4018/NatureProtector"
}
variable "repository_id" { type = string }
variable "repository_owner_id" { type = string }
variable "default_branch" {
  type    = string
  default = "master"
}
variable "artifact_repository_id" {
  type    = string
  default = "natureprotector"
}
variable "evidence_bucket_name" { type = string }
variable "terraform_state_bucket_name" { type = string }
variable "staging_cluster_name" {
  type    = string
  default = "np-staging"
}
variable "production_cluster_name" {
  type    = string
  default = "np-production"
}
variable "create_delivery_control_plane" {
  type    = bool
  default = false
}
variable "create_evidence_storage" {
  type        = bool
  default     = false
  description = "Create only the owner evidence bucket without enabling the delivery control plane."
}
variable "create_delivery_pipelines" {
  type        = bool
  default     = false
  description = "Second bootstrap phase: create cross-project Artifact Registry grants, Cloud Deploy targets, pipelines and automations after both environment roots exist."
  validation {
    condition     = !var.create_delivery_pipelines || var.create_delivery_control_plane
    error_message = "Delivery pipelines require the platform control plane foundation to be enabled first."
  }
}
variable "owner_creation_confirmation" {
  type      = string
  default   = ""
  sensitive = true
  validation {
    condition = !(var.create_delivery_control_plane || var.create_evidence_storage) || (
      var.owner_creation_confirmation == "OWNER_APPROVES_NEW_NON_CN_GCP_PROJECTS_AFTER_G10"
    )
    error_message = "G8.1 resources may only be created in new non-CN projects after G10 and explicit owner approval."
  }
}

variable "staging_run_deploy_parameters" {
  type        = map(string)
  default     = {}
  description = "Target-specific Cloud Run values for staging. Values are substituted after rendering by Cloud Deploy."
}
variable "production_run_deploy_parameters" {
  type        = map(string)
  default     = {}
  description = "Target-specific Cloud Run values for production. Values are substituted after rendering by Cloud Deploy."
}
variable "staging_gke_deploy_parameters" {
  type        = map(string)
  default     = {}
  description = "Target-specific GKE values for staging. Values are substituted after Kustomize rendering by Cloud Deploy."
}
variable "production_gke_deploy_parameters" {
  type        = map(string)
  default     = {}
  description = "Target-specific GKE values for production. Values are substituted after Kustomize rendering by Cloud Deploy."
}
variable "staging_cloud_deploy_worker_pool" {
  type        = string
  default     = ""
  description = "Full resource name of the staging private Cloud Build worker pool."
  validation {
    condition     = !var.create_delivery_pipelines || startswith(var.staging_cloud_deploy_worker_pool, "projects/")
    error_message = "A staging private Cloud Build worker pool is required when the delivery control plane is enabled."
  }
}
variable "production_cloud_deploy_worker_pool" {
  type        = string
  default     = ""
  description = "Full resource name of the production private Cloud Build worker pool."
  validation {
    condition     = !var.create_delivery_pipelines || startswith(var.production_cloud_deploy_worker_pool, "projects/")
    error_message = "A production private Cloud Build worker pool is required when the delivery control plane is enabled."
  }
}


variable "staging_gke_node_service_account" {
  type        = string
  default     = ""
  description = "Dedicated GKE Autopilot node service account created by the staging environment root."
  validation {
    condition     = !var.create_delivery_pipelines || endswith(var.staging_gke_node_service_account, ".iam.gserviceaccount.com")
    error_message = "A valid staging GKE node service account is required when the delivery control plane is enabled."
  }
}
variable "production_gke_node_service_account" {
  type        = string
  default     = ""
  description = "Dedicated GKE Autopilot node service account created by the production environment root."
  validation {
    condition     = !var.create_delivery_pipelines || endswith(var.production_gke_node_service_account, ".iam.gserviceaccount.com")
    error_message = "A valid production GKE node service account is required when the delivery control plane is enabled."
  }
}

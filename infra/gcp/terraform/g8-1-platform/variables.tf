variable "platform_project_id" {
  type = string

  validation {
    condition     = var.platform_project_id == "natureprotector-500518"
    error_message = "The platform project must be natureprotector-500518."
  }
}

variable "staging_project_id" {
  type = string

  validation {
    condition     = var.staging_project_id == "natureprotector-500518"
    error_message = "The staging project must be natureprotector-500518."
  }
}

variable "region" {
  type    = string
  default = "europe-southwest1"

  validation {
    condition     = var.region == "europe-southwest1"
    error_message = "The primary region must be europe-southwest1."
  }
}

variable "artifact_repository_id" {
  type    = string
  default = "np-releases"

  validation {
    condition     = var.artifact_repository_id == "np-releases"
    error_message = "The existing immutable repository is np-releases."
  }
}

variable "terraform_state_bucket_name" {
  type = string

  validation {
    condition     = var.terraform_state_bucket_name == "np-tfstate-migkxl-202606"
    error_message = "Unexpected Terraform state bucket."
  }
}

variable "g82_evidence_bucket_name" {
  type    = string
  default = "np-g82-evidence-22505444922"

  validation {
    condition     = var.g82_evidence_bucket_name == "np-g82-evidence-22505444922"
    error_message = "Unexpected G8.2 evidence bucket."
  }
}

variable "cloud_build_logs_bucket_name" {
  type    = string
  default = "np-cloudbuild-logs-22505444922"

  validation {
    condition     = var.cloud_build_logs_bucket_name == "np-cloudbuild-logs-22505444922"
    error_message = "Unexpected Cloud Build logs bucket."
  }
}

variable "staging_cluster_name" {
  type    = string
  default = "np-staging"

  validation {
    condition     = var.staging_cluster_name == "np-staging"
    error_message = "The staging cluster name must be np-staging."
  }
}

variable "staging_cloud_deploy_worker_pool" {
  type    = string
  default = "projects/natureprotector-500518/locations/europe-southwest1/workerPools/np-staging-deploy"

  validation {
    condition     = var.staging_cloud_deploy_worker_pool == "projects/natureprotector-500518/locations/europe-southwest1/workerPools/np-staging-deploy"
    error_message = "Unexpected staging Cloud Build worker pool."
  }
}

variable "staging_gke_node_service_account" {
  type    = string
  default = "np-staging-gke-nodes@natureprotector-500518.iam.gserviceaccount.com"

  validation {
    condition     = var.staging_gke_node_service_account == "np-staging-gke-nodes@natureprotector-500518.iam.gserviceaccount.com"
    error_message = "Unexpected staging GKE node service account."
  }
}

variable "deploy_service_account_email" {
  type    = string
  default = "np-cd-deploy@natureprotector-500518.iam.gserviceaccount.com"

  validation {
    condition     = var.deploy_service_account_email == "np-cd-deploy@natureprotector-500518.iam.gserviceaccount.com"
    error_message = "Unexpected deploy service account."
  }
}

variable "create_delivery_control_plane" {
  type    = bool
  default = false
}

variable "create_delivery_pipelines" {
  type    = bool
  default = false

  validation {
    condition = (
      !var.create_delivery_pipelines
      || var.create_delivery_control_plane
    )
    error_message = "Pipelines require the delivery control plane."
  }
}

variable "owner_creation_confirmation" {
  type      = string
  default   = ""
  sensitive = true

  validation {
    condition = (
      (
        !var.create_delivery_control_plane
        && !var.create_delivery_pipelines
      )
      || var.owner_creation_confirmation
      == "AUTHORIZE_EPHEMERAL_STAGING_APPLY_MAX_20_EUR_TTL_4H"
    )
    error_message = "The exact ephemeral staging authorization is required."
  }
}

variable "staging_run_deploy_parameters" {
  type    = map(string)
  default = {}
}

variable "staging_gke_deploy_parameters" {
  type    = map(string)
  default = {}
}

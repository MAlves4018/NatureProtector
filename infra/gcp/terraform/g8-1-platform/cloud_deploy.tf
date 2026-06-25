resource "google_service_account" "cloud_deploy_execution" {
  for_each     = var.create_delivery_control_plane ? toset(["staging", "production"]) : toset([])
  project      = var.platform_project_id
  account_id   = "np-deploy-${each.value}"
  display_name = "NatureProtector Cloud Deploy ${each.value} execution"
}



# Alternate execution identities are explicit and environment-specific. They
# need the Cloud Deploy runner role in the control-plane project. The workflow
# callers and the managed Cloud Deploy service agent receive only actAs on the
# matching identity; this avoids broad project-level Service Account User.
data "google_project" "platform" {
  project_id = var.platform_project_id
}

resource "google_project_iam_member" "cloud_deploy_execution_job_runner" {
  for_each = var.create_delivery_control_plane ? google_service_account.cloud_deploy_execution : {}
  project  = var.platform_project_id
  role     = "roles/clouddeploy.jobRunner"
  member   = "serviceAccount:${each.value.email}"
}

resource "google_service_account_iam_member" "workflow_uses_cloud_deploy_execution" {
  for_each = var.create_delivery_control_plane ? {
    staging    = google_service_account.workflow["staging"].email
    production = google_service_account.workflow["production"].email
  } : {}
  service_account_id = google_service_account.cloud_deploy_execution[each.key].name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${each.value}"
}

resource "google_service_account_iam_member" "cloud_deploy_service_agent_uses_execution" {
  for_each           = var.create_delivery_control_plane ? google_service_account.cloud_deploy_execution : {}
  service_account_id = each.value.name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:service-${data.google_project.platform.number}@gcp-sa-clouddeploy.iam.gserviceaccount.com"
}

resource "google_clouddeploy_target" "run_staging" {
  count            = var.create_delivery_pipelines ? 1 : 0
  project          = var.platform_project_id
  location         = var.region
  name             = "np-run-staging"
  require_approval = false
  run { location = "projects/${var.staging_project_id}/locations/${var.region}" }
  deploy_parameters = var.staging_run_deploy_parameters
  execution_configs {
    usages            = ["RENDER", "DEPLOY", "VERIFY"]
    execution_timeout = "3600s"
    private_pool {
      worker_pool      = var.staging_cloud_deploy_worker_pool
      service_account  = google_service_account.cloud_deploy_execution["staging"].email
      artifact_storage = "gs://${google_storage_bucket.evidence[0].name}/cloud-deploy/staging"
    }
  }
}

resource "google_clouddeploy_target" "run_production" {
  count            = var.create_delivery_pipelines ? 1 : 0
  project          = var.platform_project_id
  location         = var.region
  name             = "np-run-production"
  require_approval = true
  run { location = "projects/${var.production_project_id}/locations/${var.region}" }
  deploy_parameters = var.production_run_deploy_parameters
  execution_configs {
    usages            = ["RENDER", "DEPLOY", "VERIFY"]
    execution_timeout = "3600s"
    private_pool {
      worker_pool      = var.production_cloud_deploy_worker_pool
      service_account  = google_service_account.cloud_deploy_execution["production"].email
      artifact_storage = "gs://${google_storage_bucket.evidence[0].name}/cloud-deploy/production"
    }
  }
}

resource "google_clouddeploy_target" "gke_staging" {
  count            = var.create_delivery_pipelines ? 1 : 0
  project          = var.platform_project_id
  location         = var.region
  name             = "np-gke-staging"
  require_approval = false
  gke { cluster = "projects/${var.staging_project_id}/locations/${var.region}/clusters/${var.staging_cluster_name}" }
  deploy_parameters = var.staging_gke_deploy_parameters
  execution_configs {
    usages            = ["RENDER", "DEPLOY", "VERIFY"]
    execution_timeout = "3600s"
    private_pool {
      worker_pool      = var.staging_cloud_deploy_worker_pool
      service_account  = google_service_account.cloud_deploy_execution["staging"].email
      artifact_storage = "gs://${google_storage_bucket.evidence[0].name}/cloud-deploy/staging"
    }
  }
}

resource "google_clouddeploy_target" "gke_production" {
  count            = var.create_delivery_pipelines ? 1 : 0
  project          = var.platform_project_id
  location         = var.region
  name             = "np-gke-production"
  require_approval = true
  gke { cluster = "projects/${var.production_project_id}/locations/${var.region}/clusters/${var.production_cluster_name}" }
  deploy_parameters = var.production_gke_deploy_parameters
  execution_configs {
    usages            = ["RENDER", "DEPLOY", "VERIFY"]
    execution_timeout = "3600s"
    private_pool {
      worker_pool      = var.production_cloud_deploy_worker_pool
      service_account  = google_service_account.cloud_deploy_execution["production"].email
      artifact_storage = "gs://${google_storage_bucket.evidence[0].name}/cloud-deploy/production"
    }
  }
}

resource "google_clouddeploy_delivery_pipeline" "api" {
  count       = var.create_delivery_pipelines ? 1 : 0
  project     = var.platform_project_id
  location    = var.region
  name        = "natureprotector-api"
  description = "Backoffice API staging to production delivery"
  serial_pipeline {
    stages {
      target_id = google_clouddeploy_target.run_staging[0].name
      profiles  = ["staging"]
      strategy {
        standard {
          verify = true
        }
      }
    }
    stages {
      target_id = google_clouddeploy_target.run_production[0].name
      profiles  = ["production"]
      strategy {
        canary {
          canary_deployment {
            percentages = [5, 25, 50]
            verify      = true
          }
          runtime_config {
            cloud_run {
              automatic_traffic_control = true
            }
          }
        }
      }
    }
  }
}

resource "google_clouddeploy_delivery_pipeline" "frontend" {
  count       = var.create_delivery_pipelines ? 1 : 0
  project     = var.platform_project_id
  location    = var.region
  name        = "natureprotector-frontend"
  description = "Frontend staging to production delivery"
  serial_pipeline {
    stages {
      target_id = google_clouddeploy_target.run_staging[0].name
      profiles  = ["staging"]
      strategy {
        standard {
          verify = true
        }
      }
    }
    stages {
      target_id = google_clouddeploy_target.run_production[0].name
      profiles  = ["production"]
      strategy {
        canary {
          canary_deployment {
            percentages = [5, 25, 50]
            verify      = true
          }
          runtime_config {
            cloud_run {
              automatic_traffic_control = true
            }
          }
        }
      }
    }
  }
}

resource "google_clouddeploy_delivery_pipeline" "prevention" {
  count       = var.create_delivery_pipelines ? 1 : 0
  project     = var.platform_project_id
  location    = var.region
  name        = "natureprotector-prevention"
  description = "Prevention consumer staging to production delivery"
  serial_pipeline {
    stages {
      target_id = google_clouddeploy_target.gke_staging[0].name
      profiles  = ["staging"]
      strategy {
        standard {
          verify = true
        }
      }
    }
    stages {
      target_id = google_clouddeploy_target.gke_production[0].name
      profiles  = ["production"]
      # Prevention is a shared-queue consumer. Running stable and canary
      # consumers against the same queue would not provide deterministic
      # traffic partitioning and would complicate event-version guarantees.
      # Use a verified rolling deployment with KEDA, PDB and rollback instead.
      strategy {
        standard {
          verify = true
        }
      }
    }
  }
}

# Production requires a human approval at the target boundary. Once approved,
# successful verified canary phases are advanced automatically after a cooling
# period. Failed verification never advances the rollout.
resource "google_service_account" "cloud_deploy_automation" {
  count        = var.create_delivery_pipelines ? 1 : 0
  project      = var.platform_project_id
  account_id   = "np-deploy-automation"
  display_name = "NatureProtector verified canary automation"
}

resource "google_project_iam_member" "cloud_deploy_automation_operator" {
  count   = var.create_delivery_pipelines ? 1 : 0
  project = var.platform_project_id
  role    = "roles/clouddeploy.operator"
  member  = "serviceAccount:${google_service_account.cloud_deploy_automation[0].email}"
}

resource "google_service_account_iam_member" "automation_uses_production_execution" {
  count              = var.create_delivery_pipelines ? 1 : 0
  service_account_id = google_service_account.cloud_deploy_execution["production"].name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${google_service_account.cloud_deploy_automation[0].email}"
}

resource "google_clouddeploy_automation" "api_verified_canary" {
  count             = var.create_delivery_pipelines ? 1 : 0
  project           = var.platform_project_id
  location          = var.region
  name              = "np-api-verified-canary"
  delivery_pipeline = google_clouddeploy_delivery_pipeline.api[0].name
  service_account   = google_service_account.cloud_deploy_automation[0].email
  description       = "Advance only successful verified API canary phases"
  selector {
    targets {
      id = google_clouddeploy_target.run_production[0].name
    }
  }
  rules {
    advance_rollout_rule {
      id   = "advance-after-verification"
      wait = "300s"
    }
  }
}

resource "google_clouddeploy_automation" "frontend_verified_canary" {
  count             = var.create_delivery_pipelines ? 1 : 0
  project           = var.platform_project_id
  location          = var.region
  name              = "np-frontend-verified-canary"
  delivery_pipeline = google_clouddeploy_delivery_pipeline.frontend[0].name
  service_account   = google_service_account.cloud_deploy_automation[0].email
  description       = "Advance only successful verified frontend canary phases"
  selector {
    targets {
      id = google_clouddeploy_target.run_production[0].name
    }
  }
  rules {
    advance_rollout_rule {
      id   = "advance-after-verification"
      wait = "300s"
    }
  }
}

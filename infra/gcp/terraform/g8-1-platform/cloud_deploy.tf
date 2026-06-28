resource "google_clouddeploy_target" "run_staging" {
  count = var.create_delivery_pipelines ? 1 : 0

  project          = var.platform_project_id
  location         = var.region
  name             = "np-run-staging"
  require_approval = false

  run {
    location = "projects/${var.staging_project_id}/locations/${var.region}"
  }

  deploy_parameters = var.staging_run_deploy_parameters

  execution_configs {
    usages            = ["RENDER", "DEPLOY", "VERIFY"]
    execution_timeout = "3600s"
    worker_pool       = var.staging_cloud_deploy_worker_pool

    private_pool {
      worker_pool = var.staging_cloud_deploy_worker_pool

      service_account = google_service_account.cloud_deploy_execution[0].email
    }
  }

  depends_on = [
    google_project_service.platform,
    google_project_iam_member.cloud_deploy_execution_job_runner,
    google_project_iam_member.cloud_deploy_service_agent_worker_pool_user,
    google_service_account_iam_member.cloud_deploy_service_agent_uses_execution
  ]
}

resource "google_clouddeploy_target" "gke_staging" {
  count = var.create_delivery_pipelines ? 1 : 0

  project          = var.platform_project_id
  location         = var.region
  name             = "np-gke-staging"
  require_approval = false

  gke {
    cluster = "projects/${var.staging_project_id}/locations/${var.region}/clusters/${var.staging_cluster_name}"
  }

  deploy_parameters = var.staging_gke_deploy_parameters

  execution_configs {
    usages            = ["RENDER", "DEPLOY", "VERIFY"]
    execution_timeout = "3600s"
    worker_pool       = var.staging_cloud_deploy_worker_pool

    private_pool {
      worker_pool = var.staging_cloud_deploy_worker_pool

      service_account = google_service_account.cloud_deploy_execution[0].email
    }
  }

  depends_on = [
    google_project_service.platform,
    google_project_iam_member.cloud_deploy_execution_job_runner,
    google_project_iam_member.cloud_deploy_service_agent_worker_pool_user,
    google_service_account_iam_member.cloud_deploy_service_agent_uses_execution
  ]
}

resource "google_clouddeploy_delivery_pipeline" "api" {
  count = var.create_delivery_pipelines ? 1 : 0

  project     = var.platform_project_id
  location    = var.region
  name        = "natureprotector-api"
  description = "NatureProtector API ephemeral staging delivery"

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
  }
}

resource "google_clouddeploy_delivery_pipeline" "frontend" {
  count = var.create_delivery_pipelines ? 1 : 0

  project     = var.platform_project_id
  location    = var.region
  name        = "natureprotector-frontend"
  description = "NatureProtector frontend ephemeral staging delivery"

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
  }
}

resource "google_clouddeploy_delivery_pipeline" "prevention" {
  count = var.create_delivery_pipelines ? 1 : 0

  project     = var.platform_project_id
  location    = var.region
  name        = "natureprotector-prevention"
  description = "NatureProtector prevention ephemeral staging delivery"

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
  }
}

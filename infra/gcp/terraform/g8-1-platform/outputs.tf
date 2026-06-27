output "delivery_control_plane_mode" {
  value = "single-project-staging-only"
}

output "artifact_repository" {
  value = "${var.region}-docker.pkg.dev/${var.platform_project_id}/${var.artifact_repository_id}"
}

output "terraform_state_bucket" {
  value = var.terraform_state_bucket_name
}

output "g82_evidence_bucket" {
  value = (
    var.create_delivery_control_plane
    ? "gs://${google_storage_bucket.g82_evidence[0].name}"
    : null
  )
}

output "cloud_deploy_execution_service_account" {
  value = var.create_delivery_control_plane ? google_service_account.cloud_deploy_execution[0].email : null
}

output "delivery_targets" {
  value = (
    var.create_delivery_pipelines
    ? {
      run = google_clouddeploy_target.run_staging[0].name
      gke = google_clouddeploy_target.gke_staging[0].name
    }
    : null
  )
}

output "delivery_pipelines" {
  value = (
    var.create_delivery_pipelines
    ? {
      api        = google_clouddeploy_delivery_pipeline.api[0].name
      frontend   = google_clouddeploy_delivery_pipeline.frontend[0].name
      prevention = google_clouddeploy_delivery_pipeline.prevention[0].name
    }
    : null
  )
}

resource "google_artifact_registry_repository" "images" {
  count         = var.create_delivery_control_plane ? 1 : 0
  project       = var.platform_project_id
  location      = var.region
  repository_id = var.artifact_repository_id
  format        = "DOCKER"
  description   = "Immutable NatureProtector release images"

  docker_config {
    immutable_tags = true
  }

  cleanup_policy_dry_run = true

  depends_on = [google_project_service.platform]
}


data "google_project" "staging" {
  count      = var.create_delivery_pipelines ? 1 : 0
  project_id = var.staging_project_id
}

data "google_project" "production" {
  count      = var.create_delivery_pipelines ? 1 : 0
  project_id = var.production_project_id
}

locals {
  artifact_registry_readers = var.create_delivery_pipelines ? {
    staging_cloud_run_service_agent    = "serviceAccount:service-${data.google_project.staging[0].number}@serverless-robot-prod.iam.gserviceaccount.com"
    production_cloud_run_service_agent = "serviceAccount:service-${data.google_project.production[0].number}@serverless-robot-prod.iam.gserviceaccount.com"
    staging_gke_nodes                  = "serviceAccount:${var.staging_gke_node_service_account}"
    production_gke_nodes               = "serviceAccount:${var.production_gke_node_service_account}"
    staging_cloud_deploy_execution     = "serviceAccount:${google_service_account.cloud_deploy_execution["staging"].email}"
    production_cloud_deploy_execution  = "serviceAccount:${google_service_account.cloud_deploy_execution["production"].email}"
  } : {}
}

# Runtime projects pull immutable images from the central platform registry.
# Scope reader access to this repository instead of the whole platform project.
resource "google_artifact_registry_repository_iam_member" "runtime_readers" {
  for_each   = local.artifact_registry_readers
  project    = var.platform_project_id
  location   = var.region
  repository = google_artifact_registry_repository.images[0].repository_id
  role       = "roles/artifactregistry.reader"
  member     = each.value
}

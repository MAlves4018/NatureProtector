# The immutable np-releases repository already exists and is intentionally
# reused. This root must never attempt to recreate or replace it.

data "google_artifact_registry_repository" "images" {
  count = var.create_delivery_control_plane ? 1 : 0

  project       = var.platform_project_id
  location      = var.region
  repository_id = var.artifact_repository_id

  depends_on = [
    google_project_service.platform
  ]
}

locals {
  artifact_registry_readers = (
    var.create_delivery_pipelines
    ? {
      staging_cloud_run_service_agent = "serviceAccount:service-${data.google_project.current.number}@serverless-robot-prod.iam.gserviceaccount.com"

      staging_gke_nodes = "serviceAccount:${var.staging_gke_node_service_account}"

      staging_cloud_deploy_execution = "serviceAccount:${google_service_account.cloud_deploy_execution[0].email}"
    }
    : {}
  )
}

resource "google_artifact_registry_repository_iam_member" "runtime_readers" {
  for_each = local.artifact_registry_readers

  project    = var.platform_project_id
  location   = var.region
  repository = data.google_artifact_registry_repository.images[0].repository_id
  role       = "roles/artifactregistry.reader"
  member     = each.value
}

data "google_project" "current" {
  project_id = var.platform_project_id
}

locals {
  # These roles are required for the staging environment lifecycle and the
  # staging deployment workflow. Project-level roles for np-cd-deploy are
  # owned only by this platform state.
  deploy_foundation_roles = toset([
    "roles/cloudbuild.workerPoolOwner",
    "roles/cloudsql.admin",
    "roles/compute.networkAdmin",
    "roles/container.admin",
    "roles/dns.admin",
    "roles/iam.serviceAccountAdmin",
    "roles/logging.viewer",
    "roles/monitoring.editor",
    "roles/resourcemanager.projectIamAdmin",
    "roles/run.admin",
    "roles/secretmanager.admin",
    "roles/servicenetworking.networksAdmin",
    "roles/serviceusage.serviceUsageAdmin"
  ])

  # These permissions are not required by the foundation/environment stage.
  # They are granted only when Cloud Deploy pipelines are explicitly enabled.
  deploy_pipeline_roles = toset([
    "roles/artifactregistry.admin",
    "roles/clouddeploy.admin"
  ])
}

resource "google_project_iam_member" "deploy_foundation_roles" {
  for_each = (
    var.create_delivery_control_plane
    ? local.deploy_foundation_roles
    : toset([])
  )

  project = var.platform_project_id
  role    = each.value
  member  = "serviceAccount:${var.deploy_service_account_email}"

  depends_on = [
    google_project_service.platform
  ]
}

resource "google_project_iam_member" "deploy_pipeline_roles" {
  for_each = (
    var.create_delivery_pipelines
    ? local.deploy_pipeline_roles
    : toset([])
  )

  project = var.platform_project_id
  role    = each.value
  member  = "serviceAccount:${var.deploy_service_account_email}"

  depends_on = [
    google_project_service.platform
  ]
}

resource "google_project_iam_custom_role" "cloud_deploy_source_bucket_lister" {
  count = var.create_delivery_pipelines ? 1 : 0

  project     = var.platform_project_id
  role_id     = "npCloudDeploySourceBucketLister"
  title       = "NatureProtector Cloud Deploy source bucket lister"
  description = "Allows the workflow deployer to satisfy Cloud Deploy release source bucket discovery without project-wide Storage Admin."
  permissions = [
    "storage.buckets.get",
    "storage.buckets.list",
  ]
}

resource "google_project_iam_member" "cloud_deploy_source_bucket_lister" {
  count = var.create_delivery_pipelines ? 1 : 0

  project = var.platform_project_id
  role    = google_project_iam_custom_role.cloud_deploy_source_bucket_lister[0].id
  member  = "serviceAccount:${var.deploy_service_account_email}"
}

resource "google_service_account" "cloud_deploy_execution" {
  count = var.create_delivery_control_plane ? 1 : 0

  project      = var.platform_project_id
  account_id   = "np-deploy-staging"
  display_name = "NatureProtector staging Cloud Deploy execution"

  depends_on = [
    google_project_service.platform
  ]
}

resource "google_project_iam_member" "cloud_deploy_execution_job_runner" {
  count = var.create_delivery_control_plane ? 1 : 0

  project = var.platform_project_id
  role    = "roles/clouddeploy.jobRunner"
  member  = "serviceAccount:${google_service_account.cloud_deploy_execution[0].email}"
}

resource "google_service_account_iam_member" "deploy_uses_cloud_deploy_execution" {
  count = var.create_delivery_control_plane ? 1 : 0

  service_account_id = google_service_account.cloud_deploy_execution[0].name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${var.deploy_service_account_email}"
}

resource "google_service_account_iam_member" "cloud_deploy_service_agent_uses_execution" {
  count = var.create_delivery_pipelines ? 1 : 0

  service_account_id = google_service_account.cloud_deploy_execution[0].name
  role               = "roles/iam.serviceAccountUser"

  member = "serviceAccount:service-${data.google_project.current.number}@gcp-sa-clouddeploy.iam.gserviceaccount.com"

  depends_on = [
    google_project_service.platform
  ]
}

resource "google_project_iam_member" "cloud_deploy_service_agent_worker_pool_user" {
  count = var.create_delivery_pipelines ? 1 : 0

  project = var.platform_project_id
  role    = "roles/cloudbuild.workerPoolUser"

  member = "serviceAccount:service-${data.google_project.current.number}@gcp-sa-clouddeploy.iam.gserviceaccount.com"

  depends_on = [
    google_project_service.platform
  ]
}

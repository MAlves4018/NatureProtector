locals {
  namespace = var.runtime_namespace != "" ? var.runtime_namespace : "natureprotector-${var.environment}"
  runtime_accounts = {
    api         = "np-${var.environment}-api"
    frontend    = "np-${var.environment}-frontend"
    simulator   = "np-${var.environment}-simulator"
    smoke       = "np-${var.environment}-smoke"
    prevention  = "np-${var.environment}-prevention"
    otel        = "np-${var.environment}-otel"
    secret_sync = "np-${var.environment}-secret-sync"
    migrations  = "np-${var.environment}-migrations"
    bootstrap   = "np-${var.environment}-bootstrap"
  }
}


resource "google_service_account" "gke_nodes" {
  count        = var.create_data_plane ? 1 : 0
  project      = var.project_id
  account_id   = "np-${var.environment}-gke-nodes"
  display_name = "NatureProtector ${var.environment} GKE node identity"
}

# Autopilot nodes use this dedicated identity instead of the broad Compute
# Engine default service account. The built-in role contains the minimum
# permissions expected by GKE nodes; cross-project image pull is granted on
# the platform Artifact Registry repository by the platform root.
resource "google_project_iam_member" "gke_node_baseline" {
  count   = var.create_data_plane ? 1 : 0
  project = var.project_id
  role    = "roles/container.defaultNodeServiceAccount"
  member  = "serviceAccount:${google_service_account.gke_nodes[0].email}"
}

resource "google_service_account" "runtime" {
  for_each     = var.create_data_plane ? local.runtime_accounts : {}
  project      = var.project_id
  account_id   = each.value
  display_name = "NatureProtector ${var.environment} ${each.key}"
}

resource "google_project_iam_member" "runtime_secret_access" {
  for_each = var.create_data_plane ? toset(["api", "simulator", "smoke", "prevention", "secret_sync", "migrations", "bootstrap"]) : toset([])
  project  = var.project_id
  role     = "roles/secretmanager.secretAccessor"
  member   = "serviceAccount:${google_service_account.runtime[each.value].email}"
}

resource "google_project_iam_member" "api_runs_simulator_with_overrides" {
  count   = var.create_data_plane ? 1 : 0
  project = var.project_id
  role    = "roles/run.jobsExecutorWithOverrides"
  member  = "serviceAccount:${google_service_account.runtime["api"].email}"
}

resource "google_project_iam_member" "otel_telemetry" {
  for_each = var.create_data_plane ? toset([
    "roles/logging.logWriter",
    "roles/monitoring.metricWriter",
    "roles/cloudtrace.agent"
  ]) : toset([])
  project = var.project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.runtime["otel"].email}"
}

resource "google_service_account_iam_member" "gke_workload_identity" {
  for_each = var.create_data_plane ? {
    prevention  = "natureprotector-prevention"
    otel        = "natureprotector-otel"
    secret_sync = "natureprotector-secret-sync"
  } : {}
  service_account_id = google_service_account.runtime[each.key].name
  role               = "roles/iam.workloadIdentityUser"
  member             = "serviceAccount:${var.project_id}.svc.id.goog[${local.namespace}/${each.value}]"
}

resource "google_project_iam_member" "workflow_environment_roles" {
  for_each = var.create_data_plane ? toset([
    "roles/run.admin",
    "roles/container.admin",
    "roles/cloudsql.viewer",
    "roles/secretmanager.viewer",
    "roles/monitoring.viewer",
    "roles/logging.viewer"
  ]) : toset([])
  project = var.project_id
  role    = each.value
  member  = "serviceAccount:${var.workflow_deployer_service_account}"
}

resource "google_project_iam_member" "cloud_deploy_environment_roles" {
  for_each = var.create_data_plane ? toset([
    "roles/run.developer",
    "roles/container.developer",
    "roles/monitoring.viewer",
    "roles/logging.viewer"
  ]) : toset([])
  project = var.project_id
  role    = each.value
  member  = "serviceAccount:${var.cloud_deploy_execution_service_account}"
}

resource "google_service_account_iam_member" "workflow_uses_runtime_accounts" {
  for_each           = var.create_data_plane ? local.runtime_accounts : {}
  service_account_id = google_service_account.runtime[each.key].name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${var.workflow_deployer_service_account}"
}

resource "google_service_account_iam_member" "cloud_deploy_uses_runtime_accounts" {
  for_each           = var.create_data_plane ? local.runtime_accounts : {}
  service_account_id = google_service_account.runtime[each.key].name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${var.cloud_deploy_execution_service_account}"
}

# Cloud Deploy is hosted in the platform project while the private execution
# pool and runtime targets live in this environment project. Both the managed
# Cloud Deploy service agent and the explicit execution identity need narrowly
# scoped access across that project boundary.
data "google_project" "platform" {
  project_id = var.platform_project_id
}

resource "google_project_iam_member" "cloud_deploy_service_agent_uses_private_pool" {
  count   = var.create_data_plane ? 1 : 0
  project = var.project_id
  role    = "roles/cloudbuild.workerPoolUser"
  member  = "serviceAccount:service-${data.google_project.platform.number}@gcp-sa-clouddeploy.iam.gserviceaccount.com"
}

resource "google_project_iam_member" "cloud_deploy_execution_uses_private_pool" {
  count   = var.create_data_plane ? 1 : 0
  project = var.project_id
  role    = "roles/cloudbuild.workerPoolUser"
  member  = "serviceAccount:${var.cloud_deploy_execution_service_account}"
}


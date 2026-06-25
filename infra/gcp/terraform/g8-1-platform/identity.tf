locals {
  workflow_identities = {
    release = {
      account_id  = "gha-np-release"
      workflow    = "gcp-g8-1-release.yml"
      environment = ""
    }
    staging = {
      account_id  = "gha-np-staging"
      workflow    = "gcp-g8-1-deploy-staging.yml"
      environment = "staging"
    }
    production = {
      account_id  = "gha-np-production"
      workflow    = "gcp-g8-1-promote-production.yml"
      environment = "production"
    }
    operations = {
      account_id  = "gha-np-operations"
      workflow    = "gcp-g8-1-teardown.yml"
      environment = "production-operations"
    }
    g82-probe = {
      account_id  = "gha-np-g82-probe"
      workflow    = "gcp-g8-2-runtime-probe.yml"
      environment = "staging"
    }
    g82-qualification = {
      account_id  = "gha-np-g82-qualify"
      workflow    = "gcp-g8-2-runtime-qualification.yml"
      environment = "staging"
    }
  }
}

resource "google_iam_workload_identity_pool" "github" {
  count                     = var.create_delivery_control_plane ? 1 : 0
  project                   = var.platform_project_id
  workload_identity_pool_id = "natureprotector-github"
  display_name              = "NatureProtector GitHub Actions"
  depends_on                = [google_project_service.platform]
}

resource "google_iam_workload_identity_pool_provider" "github" {
  for_each                           = var.create_delivery_control_plane ? local.workflow_identities : {}
  project                            = var.platform_project_id
  workload_identity_pool_id          = google_iam_workload_identity_pool.github[0].workload_identity_pool_id
  workload_identity_pool_provider_id = "np-${each.key}"
  display_name                       = length("NatureProtector ${each.key} workflow") <= 32 ? "NatureProtector ${each.key} workflow" : "NP ${each.key} workflow"

  attribute_mapping = merge(
    {
      "google.subject"          = "assertion.sub"
      "attribute.repository"    = "assertion.repository"
      "attribute.repository_id" = "assertion.repository_id"
      "attribute.owner_id"      = "assertion.repository_owner_id"
      "attribute.ref"           = "assertion.ref"
      "attribute.workflow_ref"  = "assertion.workflow_ref"
      # Each provider maps a short, URI-safe identity used by the IAM
      # principalSet. The provider condition below still validates the exact
      # GitHub workflow_ref and branch before issuing the identity.
      "attribute.workflow_id" = "'${each.key}'"
    },
    each.value.environment == "" ? {} : {
      "attribute.environment" = "assertion.environment"
    }
  )

  attribute_condition = each.value.environment == "" ? (
    "assertion.repository_id == '${var.repository_id}' && assertion.repository_owner_id == '${var.repository_owner_id}' && assertion.ref == 'refs/heads/${var.default_branch}' && assertion.workflow_ref == '${var.repository}/.github/workflows/${each.value.workflow}@refs/heads/${var.default_branch}'"
    ) : (
    "assertion.repository_id == '${var.repository_id}' && assertion.repository_owner_id == '${var.repository_owner_id}' && assertion.ref == 'refs/heads/${var.default_branch}' && assertion.environment == '${each.value.environment}' && assertion.workflow_ref == '${var.repository}/.github/workflows/${each.value.workflow}@refs/heads/${var.default_branch}'"
  )

  oidc { issuer_uri = "https://token.actions.githubusercontent.com" }
}

resource "google_service_account" "workflow" {
  for_each     = var.create_delivery_control_plane ? local.workflow_identities : {}
  project      = var.platform_project_id
  account_id   = each.value.account_id
  display_name = "NatureProtector ${each.key} workflow"
}

resource "google_service_account_iam_member" "workflow_federation" {
  for_each           = var.create_delivery_control_plane ? local.workflow_identities : {}
  service_account_id = google_service_account.workflow[each.key].name
  role               = "roles/iam.workloadIdentityUser"

  # Bind through a short provider-scoped attribute. The provider itself only
  # accepts the exact workflow_ref, branch, repository IDs and (where used)
  # GitHub Environment. This avoids putting slash-rich workflow_ref values in
  # the principalSet URI while preserving one workflow per service account.
  member = "principalSet://iam.googleapis.com/${google_iam_workload_identity_pool.github[0].name}/attribute.workflow_id/${each.key}"
}

resource "google_project_iam_member" "release_artifact_writer" {
  count   = var.create_delivery_control_plane ? 1 : 0
  project = var.platform_project_id
  role    = "roles/artifactregistry.writer"
  member  = "serviceAccount:${google_service_account.workflow["release"].email}"
}

resource "google_project_iam_member" "release_attestation" {
  for_each = var.create_delivery_control_plane ? toset([
    "roles/containeranalysis.notes.attacher",
    "roles/clouddeploy.releaser"
  ]) : toset([])
  project = var.platform_project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.workflow["release"].email}"
}

resource "google_project_iam_member" "staging_deployer" {
  for_each = var.create_delivery_control_plane ? toset([
    "roles/clouddeploy.releaser",
    "roles/clouddeploy.jobRunner"
  ]) : toset([])
  project = var.platform_project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.workflow["staging"].email}"
}

resource "google_project_iam_member" "production_deployer" {
  for_each = var.create_delivery_control_plane ? toset([
    "roles/clouddeploy.releaser",
    "roles/clouddeploy.approver",
    "roles/clouddeploy.jobRunner"
  ]) : toset([])
  project = var.platform_project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.workflow["production"].email}"
}

resource "google_project_iam_member" "operations_viewer" {
  count   = var.create_delivery_control_plane ? 1 : 0
  project = var.platform_project_id
  role    = "roles/viewer"
  member  = "serviceAccount:${google_service_account.workflow["operations"].email}"
}


# The operations workflow must be able to read/update remote environment state
# and remove the runtime resources after evidence export. These explicit roles
# replace Owner/Editor and remain managed by the platform foundation so they do
# not disappear halfway through environment destruction.
resource "google_storage_bucket_iam_member" "operations_state" {
  count  = var.create_delivery_control_plane ? 1 : 0
  bucket = var.terraform_state_bucket_name
  role   = "roles/storage.objectAdmin"
  member = "serviceAccount:${google_service_account.workflow["operations"].email}"
}

locals {
  operations_environment_roles = toset([
    "roles/cloudasset.viewer",
    "roles/cloudbuild.builds.editor",
    "roles/cloudbuild.workerPoolOwner",
    "roles/cloudsql.admin",
    "roles/compute.loadBalancerAdmin",
    "roles/compute.networkAdmin",
    "roles/compute.securityAdmin",
    "roles/container.admin",
    "roles/dns.admin",
    "roles/iam.serviceAccountAdmin",
    "roles/monitoring.admin",
    "roles/resourcemanager.projectIamAdmin",
    "roles/run.admin",
    "roles/secretmanager.admin",
    "roles/servicenetworking.networksAdmin",
    "roles/serviceusage.serviceUsageAdmin"
  ])
  operations_environment_grants = var.create_delivery_control_plane ? merge(
    { for role in local.operations_environment_roles : "staging:${role}" => { project = var.staging_project_id, role = role } },
    { for role in local.operations_environment_roles : "production:${role}" => { project = var.production_project_id, role = role } }
  ) : {}
}

resource "google_project_iam_member" "operations_environment" {
  for_each = local.operations_environment_grants
  project  = each.value.project
  role     = each.value.role
  member   = "serviceAccount:${google_service_account.workflow["operations"].email}"
}


# G8.2 probe identity may execute bounded qualification drills and read the
# resulting operational state. It remains separate from release, deployment,
# finalization and authorization identities.
locals {
  g82_probe_environment_roles = toset([
    "roles/cloudasset.viewer",
    "roles/cloudsql.admin",
    "roles/container.developer",
    "roles/logging.viewer",
    "roles/monitoring.viewer",
    "roles/run.developer",
    "roles/secretmanager.viewer"
  ])
  g82_probe_environment_grants = var.create_delivery_control_plane ? merge(
    { for role in local.g82_probe_environment_roles : "staging:${role}" => { project = var.staging_project_id, role = role } },
    { for role in local.g82_probe_environment_roles : "production:${role}" => { project = var.production_project_id, role = role } }
  ) : {}
}

resource "google_project_iam_member" "g82_probe_environment" {
  for_each = local.g82_probe_environment_grants
  project  = each.value.project
  role     = each.value.role
  member   = "serviceAccount:${google_service_account.workflow["g82-probe"].email}"
}

resource "google_storage_bucket_iam_member" "g82_qualification_archive" {
  count  = var.create_delivery_control_plane ? 1 : 0
  bucket = var.evidence_bucket_name
  role   = "roles/storage.objectAdmin"
  member = "serviceAccount:${google_service_account.workflow["g82-qualification"].email}"
}

resource "google_project_iam_member" "g82_qualification_bucket_viewer" {
  count   = var.create_delivery_control_plane ? 1 : 0
  project = var.platform_project_id
  role    = "roles/storage.objectViewer"
  member  = "serviceAccount:${google_service_account.workflow["g82-qualification"].email}"
}

# Archive finalization verifies bucket versioning, retention and public-access
# prevention before it can emit a final qualification verdict. Object write
# access alone does not grant bucket metadata read access.
resource "google_storage_bucket_iam_member" "g82_qualification_bucket_metadata" {
  count  = var.create_delivery_control_plane ? 1 : 0
  bucket = var.evidence_bucket_name
  role   = "roles/storage.legacyBucketReader"
  member = "serviceAccount:${google_service_account.workflow["g82-qualification"].email}"
}

locals {
  platform_services = toset([
    "artifactregistry.googleapis.com",
    "binaryauthorization.googleapis.com",
    "cloudbuild.googleapis.com",
    "clouddeploy.googleapis.com",
    "cloudtrace.googleapis.com",
    "compute.googleapis.com",
    "container.googleapis.com",
    "containeranalysis.googleapis.com",
    "dns.googleapis.com",
    "iam.googleapis.com",
    "iamcredentials.googleapis.com",
    "logging.googleapis.com",
    "monitoring.googleapis.com",
    "run.googleapis.com",
    "secretmanager.googleapis.com",
    "servicenetworking.googleapis.com",
    "serviceusage.googleapis.com",
    "sqladmin.googleapis.com",
    "sts.googleapis.com"
  ])

  enabled_platform_services = (
    var.create_delivery_control_plane
    ? local.platform_services
    : toset([])
  )
}

resource "google_project_service" "platform" {
  for_each = local.enabled_platform_services

  project            = var.platform_project_id
  service            = each.value
  disable_on_destroy = false
}

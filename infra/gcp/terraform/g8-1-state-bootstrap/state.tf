resource "google_storage_bucket" "terraform_state" {
  count                       = var.create_state_foundation ? 1 : 0
  project                     = var.platform_project_id
  name                        = var.state_bucket_name
  location                    = var.region
  uniform_bucket_level_access = true
  public_access_prevention    = "enforced"
  force_destroy               = false

  versioning { enabled = true }
  retention_policy {
    retention_period = var.state_retention_days * 86400
  }
  lifecycle { prevent_destroy = true }
  depends_on = [google_project_service.storage]
}

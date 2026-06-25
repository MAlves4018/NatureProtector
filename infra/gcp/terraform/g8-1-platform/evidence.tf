resource "google_storage_bucket" "evidence" {
  count                       = (var.create_delivery_control_plane || var.create_evidence_storage) ? 1 : 0
  project                     = var.platform_project_id
  name                        = var.evidence_bucket_name
  location                    = var.region
  uniform_bucket_level_access = true
  public_access_prevention    = "enforced"
  force_destroy               = false

  versioning { enabled = true }

  retention_policy {
    retention_period = 31536000
    is_locked        = false
  }

  lifecycle_rule {
    condition { num_newer_versions = 10 }
    action { type = "Delete" }
  }

  depends_on = [google_project_service.platform]
}

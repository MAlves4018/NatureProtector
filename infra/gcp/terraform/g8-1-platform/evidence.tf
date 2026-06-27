# G8.2 qualification evidence is intentionally isolated from Terraform state.
# The bucket is persistent platform infrastructure even though staging runtime
# resources are ephemeral.
resource "google_storage_bucket" "g82_evidence" {
  count = var.create_delivery_control_plane ? 1 : 0

  project  = var.platform_project_id
  name     = var.g82_evidence_bucket_name
  location = var.region

  storage_class               = "STANDARD"
  uniform_bucket_level_access = true
  public_access_prevention    = "enforced"
  force_destroy               = false

  labels = {
    system      = "natureprotector"
    environment = "staging"
    purpose     = "g82-evidence"
    managed_by  = "terraform"
    phase       = "g8-2"
  }

  versioning {
    enabled = true
  }

  retention_policy {
    retention_period = 31536000
    is_locked        = false
  }

  lifecycle_rule {
    condition {
      num_newer_versions = 10
    }

    action {
      type = "Delete"
    }
  }

  depends_on = [
    google_project_service.platform
  ]
}

resource "google_storage_bucket_iam_member" "g82_evidence_objects" {
  count = var.create_delivery_control_plane ? 1 : 0

  bucket = google_storage_bucket.g82_evidence[0].name
  role   = "roles/storage.objectAdmin"
  member = "serviceAccount:${var.deploy_service_account_email}"
}

resource "google_storage_bucket_iam_member" "g82_evidence_metadata" {
  count = var.create_delivery_control_plane ? 1 : 0

  bucket = google_storage_bucket.g82_evidence[0].name
  role   = "roles/storage.bucketViewer"
  member = "serviceAccount:${var.deploy_service_account_email}"
}

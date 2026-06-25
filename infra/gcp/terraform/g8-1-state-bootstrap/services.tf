resource "google_project_service" "storage" {
  count              = var.create_state_foundation ? 1 : 0
  project            = var.platform_project_id
  service            = "storage.googleapis.com"
  disable_on_destroy = false
}

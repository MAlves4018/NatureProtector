# Cloud Deploy uses a private Cloud Build pool connected to the environment
# VPC. This is required for deployment/verification against private GKE and
# Cloud Run services whose direct public ingress is disabled.
resource "google_cloudbuild_worker_pool" "cloud_deploy" {
  count    = var.create_data_plane ? 1 : 0
  project  = var.project_id
  location = var.region
  name     = "np-${var.environment}-deploy"

  worker_config {
    disk_size_gb   = var.cloud_deploy_worker_disk_size_gb
    machine_type   = var.cloud_deploy_worker_machine_type
    no_external_ip = true
  }

  network_config {
    peered_network          = google_compute_network.main[0].id
    peered_network_ip_range = "/28"
  }

  depends_on = [
    google_service_networking_connection.private_service_access
  ]
}

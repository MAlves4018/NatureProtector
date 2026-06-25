# Cloud Run services and jobs cannot resolve Kubernetes service names. Stable
# private DNS records bind the environment-local names used in TLS certificates
# to the reserved internal load-balancer addresses in the same VPC.
resource "google_dns_managed_zone" "runtime" {
  count       = var.create_data_plane ? 1 : 0
  project     = var.project_id
  name        = "np-${var.environment}-runtime"
  dns_name    = "${var.environment}.natureprotector.internal."
  description = "NatureProtector ${var.environment} private runtime services"
  visibility  = "private"

  private_visibility_config {
    networks {
      network_url = google_compute_network.main[0].id
    }
  }
}

resource "google_dns_record_set" "rabbitmq" {
  count        = var.create_data_plane ? 1 : 0
  project      = var.project_id
  managed_zone = google_dns_managed_zone.runtime[0].name
  name         = "rabbitmq.${google_dns_managed_zone.runtime[0].dns_name}"
  type         = "A"
  ttl          = 30
  rrdatas      = [google_compute_address.gke_internal_service["rabbitmq"].address]
}

resource "google_dns_record_set" "otel" {
  count        = var.create_data_plane ? 1 : 0
  project      = var.project_id
  managed_zone = google_dns_managed_zone.runtime[0].name
  name         = "otel.${google_dns_managed_zone.runtime[0].dns_name}"
  type         = "A"
  ttl          = 30
  rrdatas      = [google_compute_address.gke_internal_service["otel"].address]
}

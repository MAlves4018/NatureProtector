resource "google_compute_network" "main" {
  count                   = var.create_data_plane ? 1 : 0
  project                 = var.project_id
  name                    = "np-${var.environment}"
  auto_create_subnetworks = false
  routing_mode            = "REGIONAL"
}

resource "google_compute_subnetwork" "main" {
  count                    = var.create_data_plane ? 1 : 0
  project                  = var.project_id
  region                   = var.region
  name                     = "np-${var.environment}-${var.region}"
  network                  = google_compute_network.main[0].id
  ip_cidr_range            = var.network_cidr
  private_ip_google_access = true

  secondary_ip_range {
    range_name    = "pods"
    ip_cidr_range = var.pods_cidr
  }
  secondary_ip_range {
    range_name    = "services"
    ip_cidr_range = var.services_cidr
  }

  log_config {
    aggregation_interval = "INTERVAL_5_SEC"
    flow_sampling        = 0.5
    metadata             = "INCLUDE_ALL_METADATA"
  }
}

resource "google_compute_global_address" "private_service_access" {
  count         = var.create_data_plane ? 1 : 0
  project       = var.project_id
  name          = "np-${var.environment}-private-services"
  purpose       = "VPC_PEERING"
  address_type  = "INTERNAL"
  prefix_length = var.private_service_access_prefix_length
  network       = google_compute_network.main[0].id
}

resource "google_service_networking_connection" "private_service_access" {
  count                   = var.create_data_plane ? 1 : 0
  network                 = google_compute_network.main[0].id
  service                 = "servicenetworking.googleapis.com"
  reserved_peering_ranges = [google_compute_global_address.private_service_access[0].name]
}

# Stable private entry points used by Cloud Run services/jobs to reach the GKE
# data plane. These addresses remain environment-local and are never public.
resource "google_compute_address" "gke_internal_service" {
  for_each     = var.create_data_plane ? toset(["rabbitmq", "otel"]) : toset([])
  project      = var.project_id
  region       = var.region
  name         = "np-${var.environment}-${each.value}-ilb"
  address_type = "INTERNAL"
  subnetwork   = google_compute_subnetwork.main[0].id
}

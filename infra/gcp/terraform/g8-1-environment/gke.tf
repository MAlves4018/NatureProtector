resource "google_container_cluster" "main" {
  count               = var.create_data_plane ? 1 : 0
  project             = var.project_id
  name                = var.cluster_name
  location            = var.region
  enable_autopilot    = true
  network             = google_compute_network.main[0].id
  subnetwork          = google_compute_subnetwork.main[0].id
  deletion_protection = var.deletion_protection

  release_channel { channel = "REGULAR" }
  workload_identity_config { workload_pool = "${var.project_id}.svc.id.goog" }
  private_cluster_config { enable_private_nodes = true }
  control_plane_endpoints_config {
    dns_endpoint_config {
      allow_external_traffic    = true
      enable_k8s_tokens_via_dns = false
      enable_k8s_certs_via_dns  = false
    }
    ip_endpoints_config { enabled = false }
  }
  ip_allocation_policy {
    cluster_secondary_range_name  = "pods"
    services_secondary_range_name = "services"
  }
  secret_manager_config {
    enabled = true
    rotation_config {
      enabled           = true
      rotation_interval = "300s"
    }
  }
  secret_sync_config {
    enabled = true
    rotation_config {
      enabled           = true
      rotation_interval = "300s"
    }
  }
  binary_authorization { evaluation_mode = "PROJECT_SINGLETON_POLICY_ENFORCE" }
  security_posture_config {
    mode               = "BASIC"
    vulnerability_mode = "VULNERABILITY_DISABLED"
  }
  cost_management_config { enabled = true }

  cluster_autoscaling {
    auto_provisioning_defaults {
      service_account = google_service_account.gke_nodes[0].email
    }
  }

  depends_on = [
    google_project_iam_member.gke_node_baseline
  ]
}

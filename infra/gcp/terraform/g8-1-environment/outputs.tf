output "network_id" { value = var.create_data_plane ? google_compute_network.main[0].id : null }
output "cluster_id" { value = var.create_data_plane ? google_container_cluster.main[0].id : null }
output "cloud_sql_connection_name" { value = var.create_data_plane ? google_sql_database_instance.primary[0].connection_name : null }
output "edge_ip" { value = var.create_data_plane && var.create_edge && length(var.managed_certificate_domains) > 0 ? google_compute_global_address.https[0].address : null }
output "secret_ids" { value = { for key, value in google_secret_manager_secret.runtime : key => value.id } }
output "runtime_service_accounts" { value = { for key, value in google_service_account.runtime : key => value.email } }
output "network_name" { value = var.create_data_plane ? google_compute_network.main[0].name : null }
output "subnetwork_name" { value = var.create_data_plane ? google_compute_subnetwork.main[0].name : null }
output "cloud_sql_private_ip" { value = var.create_data_plane ? google_sql_database_instance.primary[0].private_ip_address : null }
output "rabbitmq_internal_ip" { value = var.create_data_plane ? google_compute_address.gke_internal_service["rabbitmq"].address : null }
output "otel_internal_ip" { value = var.create_data_plane ? google_compute_address.gke_internal_service["otel"].address : null }
output "runtime_subnet_cidr" { value = var.create_data_plane ? google_compute_subnetwork.main[0].ip_cidr_range : null }
output "cloud_deploy_worker_pool" { value = var.create_data_plane ? google_cloudbuild_worker_pool.cloud_deploy[0].id : null }

output "rabbitmq_private_dns_name" {
  value = var.create_data_plane ? trimsuffix(google_dns_record_set.rabbitmq[0].name, ".") : null
}
output "otel_private_dns_name" {
  value = var.create_data_plane ? trimsuffix(google_dns_record_set.otel[0].name, ".") : null
}

output "gke_node_service_account" { value = var.create_data_plane ? google_service_account.gke_nodes[0].email : null }

output "generated_secret_versions" {
  value = { for key, value in google_secret_manager_secret_version.generated : key => value.version }
}

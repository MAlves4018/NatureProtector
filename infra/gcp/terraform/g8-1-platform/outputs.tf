output "artifact_repository" {
  value = var.create_delivery_control_plane ? google_artifact_registry_repository.images[0].name : null
}
output "evidence_bucket" {
  value = (var.create_delivery_control_plane || var.create_evidence_storage) ? google_storage_bucket.evidence[0].name : null
}
output "wif_providers" {
  value = { for key, value in google_iam_workload_identity_pool_provider.github : key => value.name }
}
output "workflow_service_accounts" {
  value = { for key, value in google_service_account.workflow : key => value.email }
}
output "delivery_pipelines" {
  value = var.create_delivery_pipelines ? {
    api        = google_clouddeploy_delivery_pipeline.api[0].name
    frontend   = google_clouddeploy_delivery_pipeline.frontend[0].name
    prevention = google_clouddeploy_delivery_pipeline.prevention[0].name
  } : null
}

output "cloud_deploy_execution_service_accounts" {
  value = { for key, value in google_service_account.cloud_deploy_execution : key => value.email }
}

# Persistent staging delivery foundation.
# Authorization is supplied only through TF_VAR_owner_creation_confirmation.

platform_project_id = "natureprotector-500518"
staging_project_id  = "natureprotector-500518"
region              = "europe-southwest1"

artifact_repository_id      = "np-releases"
terraform_state_bucket_name = "np-tfstate-migkxl-202606"
g82_evidence_bucket_name    = "np-g82-evidence-22505444922"

staging_cluster_name = "np-staging"

staging_cloud_deploy_worker_pool = "projects/natureprotector-500518/locations/europe-southwest1/workerPools/np-staging-deploy"

staging_gke_node_service_account = "np-staging-gke-nodes@natureprotector-500518.iam.gserviceaccount.com"

deploy_service_account_email = "np-cd-deploy@natureprotector-500518.iam.gserviceaccount.com"

create_delivery_control_plane = true
create_delivery_pipelines     = false

staging_run_deploy_parameters = {
  environment = "staging"
}

staging_gke_deploy_parameters = {
  environment = "staging"
  namespace   = "natureprotector-staging"
}

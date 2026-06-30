# Persistent staging delivery foundation.
# Authorization is supplied only through TF_VAR_owner_creation_confirmation.
#
# Stage 2 is active: the environment, private worker pool, Cloud SQL, GKE,
# runtime identities and owner-managed TLS versions have been proved.
# Values below are non-secret resource references resolved from the canonical
# environment Terraform state and the enabled Secret Manager version contract.

platform_project_id = "natureprotector-500518"
staging_project_id  = "natureprotector-500518"
region              = "europe-southwest1"

artifact_repository_id                     = "np-releases"
terraform_state_bucket_name                = "np-tfstate-migkxl-202606"
g82_evidence_bucket_name                   = "np-g82-evidence-22505444922"
cloud_build_logs_bucket_name               = "np-cloudbuild-logs-22505444922"
cloud_deploy_source_bucket_name            = "d09bb0b9ead342f0a6b38ecd9db4c11a_clouddeploy"
cloud_deploy_frontend_source_bucket_name   = "0055c8c327b743efbfa1809f2a4363ef_clouddeploy"
cloud_deploy_prevention_source_bucket_name = "c31effabdcbc4c0895cf09390ae59db0_clouddeploy"

staging_cluster_name = "np-staging"

staging_cloud_deploy_worker_pool = "projects/natureprotector-500518/locations/europe-southwest1/workerPools/np-staging-deploy"

staging_gke_node_service_account = "np-staging-gke-nodes@natureprotector-500518.iam.gserviceaccount.com"

deploy_service_account_email = "np-cd-deploy@natureprotector-500518.iam.gserviceaccount.com"

create_delivery_control_plane = true
create_delivery_pipelines     = true

staging_run_deploy_parameters = {
  api_internal_origin            = "https://natureprotector-api-22505444922.europe-southwest1.run.app"
  api_max_scale                  = "2"
  api_min_scale                  = "0"
  api_service_account_email      = "np-staging-api@natureprotector-500518.iam.gserviceaccount.com"
  cloud_sql_ca_secret            = "np-staging-cloud-sql-server-ca"
  cloud_sql_ca_version           = "1"
  cloud_sql_private_ip           = "10.196.1.3"
  frontend_max_scale             = "2"
  frontend_min_scale             = "0"
  frontend_service_account_email = "np-staging-frontend@natureprotector-500518.iam.gserviceaccount.com"
  jwt_signing_key_secret         = "np-staging-jwt-signing-key"
  jwt_signing_key_version        = "1"
  otel_endpoint                  = "http://otel.staging.natureprotector.internal:4317"
  postgres_app_password_secret   = "np-staging-postgres-app-password"
  postgres_app_password_version  = "1"
  rabbitmq_app_password_secret   = "np-staging-rabbitmq-app-password"
  rabbitmq_app_password_version  = "1"
  rabbitmq_app_username_secret   = "np-staging-rabbitmq-app-username"
  rabbitmq_app_username_version  = "1"
  rabbitmq_ca_secret             = "np-staging-rabbitmq-ca-certificate"
  rabbitmq_ca_version            = "1"
  rabbitmq_private_host          = "rabbitmq.staging.natureprotector.internal"
  rabbitmq_tls_server_name       = "rabbitmq.staging.natureprotector.internal"
  runtime_project_id             = "natureprotector-500518"
  runtime_region                 = "europe-southwest1"
}

staging_gke_deploy_parameters = {
  cloud_sql_private_cidr    = "10.196.1.3/32"
  cloud_sql_private_ip      = "10.196.1.3"
  otel_gsa                  = "np-staging-otel@natureprotector-500518.iam.gserviceaccount.com"
  otel_load_balancer_ip     = "10.20.0.2"
  prevention_gsa            = "np-staging-prevention@natureprotector-500518.iam.gserviceaccount.com"
  rabbitmq_load_balancer_ip = "10.20.0.3"

  rabbitmq_tls_server_name = "rabbitmq.staging.natureprotector.internal"

  runtime_subnet_cidr = "10.20.0.0/24"
  secret_sync_gsa     = "np-staging-secret-sync@natureprotector-500518.iam.gserviceaccount.com"
}

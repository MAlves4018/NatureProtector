# NatureProtector ephemeral staging runtime.
# This profile is staging-only and must never be reused for production.

project_id          = "natureprotector-500518"
platform_project_id = "natureprotector-500518"
environment         = "staging"
region              = "europe-southwest1"

network_cidr  = "10.20.0.0/24"
pods_cidr     = "10.21.0.0/16"
services_cidr = "10.22.0.0/20"

cluster_name           = "np-staging"
runtime_namespace      = "natureprotector-staging"
database_instance_name = "np-staging-postgres"

database_tier = "db-f1-micro"

database_edition           = "ENTERPRISE"
database_disk_size_gb      = 10
database_availability_type = "ZONAL"
database_disk_type         = "PD_HDD"
database_backup_enabled    = false
database_pitr_enabled      = false
database_retained_backups  = 1
deletion_protection        = false

cloud_deploy_worker_machine_type = "e2-standard-4"
cloud_deploy_worker_disk_size_gb = 100

create_data_plane             = true
create_edge                   = true
materialize_generated_secrets = true
secret_generation             = 1

managed_certificate_domains      = ["136-68-225-29.sslip.io"]
monitoring_notification_channels = []

workflow_deployer_service_account = "np-cd-deploy@natureprotector-500518.iam.gserviceaccount.com"

cloud_deploy_execution_service_account = "np-deploy-staging@natureprotector-500518.iam.gserviceaccount.com"

output "state_bucket_name" {
  value = var.create_state_foundation ? google_storage_bucket.terraform_state[0].name : null
}

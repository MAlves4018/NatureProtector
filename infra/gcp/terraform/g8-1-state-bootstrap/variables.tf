variable "platform_project_id" { type = string }
variable "region" {
  type    = string
  default = "europe-southwest1"
  validation {
    condition     = var.region == "europe-southwest1"
    error_message = "The primary NatureProtector region is Madrid (europe-southwest1)."
  }
}
variable "state_bucket_name" { type = string }
variable "state_retention_days" {
  type    = number
  default = 30
  validation {
    condition     = var.state_retention_days >= 7
    error_message = "Terraform state retention must be at least seven days."
  }
}
variable "create_state_foundation" {
  type    = bool
  default = false
}
variable "owner_creation_confirmation" {
  type      = string
  default   = ""
  sensitive = true
  validation {
    condition     = !var.create_state_foundation || var.owner_creation_confirmation == "OWNER_APPROVES_NEW_NON_CN_GCP_PROJECTS_AFTER_G10"
    error_message = "The state foundation may only be created in a new non-CN platform project after G10 and explicit owner approval."
  }
}

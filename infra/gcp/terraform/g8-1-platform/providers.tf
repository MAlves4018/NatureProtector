provider "google" {
  project        = var.platform_project_id
  region         = var.region
  default_labels = local.labels
}

provider "google-beta" {
  project = var.platform_project_id
  region  = var.region
}


locals {
  labels = {
    system      = "natureprotector"
    managed_by  = "terraform"
    phase       = "g8-1"
    environment = "platform"
  }
}

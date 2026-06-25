terraform {
  backend "gcs" {}
  required_version = "~> 1.15.5"
  required_providers {
    google = { source = "hashicorp/google", version = "= 7.36.0" }
  }
}

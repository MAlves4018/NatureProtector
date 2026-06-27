# Project-level Google Cloud API enablement is owned exclusively by the
# g8-1-platform Terraform root. Keeping google_project_service resources out
# of this root prevents the same remote API registration from being managed
# by both the platform and environment states.

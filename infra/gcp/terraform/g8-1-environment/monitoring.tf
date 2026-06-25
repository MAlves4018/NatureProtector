resource "google_monitoring_service" "api" {
  count        = var.create_data_plane ? 1 : 0
  project      = var.project_id
  service_id   = "natureprotector-api-${var.environment}"
  display_name = "NatureProtector API ${var.environment}"
  basic_service {
    service_type = "CLOUD_RUN"
    service_labels = {
      service_name = var.api_service_name
      location     = var.region
    }
  }
}

resource "google_monitoring_slo" "api_availability" {
  count               = var.create_data_plane ? 1 : 0
  project             = var.project_id
  service             = google_monitoring_service.api[0].service_id
  slo_id              = "availability-7d"
  display_name        = "API availability over seven rolling days"
  goal                = var.api_availability_goal
  rolling_period_days = 7

  request_based_sli {
    good_total_ratio {
      good_service_filter = join(" AND ", [
        "metric.type=\"run.googleapis.com/request_count\"",
        "resource.type=\"cloud_run_revision\"",
        "resource.label.service_name=\"${var.api_service_name}\"",
        "resource.label.location=\"${var.region}\"",
        "metric.label.response_code_class!=\"5xx\""
      ])
      total_service_filter = join(" AND ", [
        "metric.type=\"run.googleapis.com/request_count\"",
        "resource.type=\"cloud_run_revision\"",
        "resource.label.service_name=\"${var.api_service_name}\"",
        "resource.label.location=\"${var.region}\""
      ])
    }
  }
}

resource "google_monitoring_slo" "api_latency" {
  count               = var.create_data_plane ? 1 : 0
  project             = var.project_id
  service             = google_monitoring_service.api[0].service_id
  slo_id              = "latency-7d"
  display_name        = "API p99-oriented request latency over seven rolling days"
  goal                = 0.99
  rolling_period_days = 7

  request_based_sli {
    distribution_cut {
      distribution_filter = join(" AND ", [
        "metric.type=\"run.googleapis.com/request_latencies\"",
        "resource.type=\"cloud_run_revision\"",
        "resource.label.service_name=\"${var.api_service_name}\"",
        "resource.label.location=\"${var.region}\""
      ])
      range { max = 30000 }
    }
  }
}

resource "google_monitoring_alert_policy" "api_fast_burn" {
  count                 = var.create_data_plane ? 1 : 0
  project               = var.project_id
  display_name          = "NatureProtector API fast error-budget burn (${var.environment})"
  combiner              = "OR"
  enabled               = true
  notification_channels = var.monitoring_notification_channels
  conditions {
    display_name = "Availability budget burns at 14.4x over one hour"
    condition_threshold {
      filter          = "select_slo_burn_rate(\"${google_monitoring_slo.api_availability[0].name}\", \"3600s\")"
      comparison      = "COMPARISON_GT"
      threshold_value = 14.4
      duration        = "0s"
      trigger { count = 1 }
    }
  }
  alert_strategy { auto_close = "1800s" }
  documentation {
    content   = "Fast error-budget burn. Freeze promotion, inspect the current rollout and execute the rollback runbook when release-related."
    mime_type = "text/markdown"
  }
}

resource "google_monitoring_alert_policy" "api_slow_burn" {
  count                 = var.create_data_plane ? 1 : 0
  project               = var.project_id
  display_name          = "NatureProtector API slow error-budget burn (${var.environment})"
  combiner              = "OR"
  enabled               = true
  notification_channels = var.monitoring_notification_channels
  conditions {
    display_name = "Availability budget burns at 3x over six hours"
    condition_threshold {
      filter          = "select_slo_burn_rate(\"${google_monitoring_slo.api_availability[0].name}\", \"21600s\")"
      comparison      = "COMPARISON_GT"
      threshold_value = 3
      duration        = "0s"
      trigger { count = 1 }
    }
  }
  alert_strategy { auto_close = "3600s" }
  documentation {
    content   = "Sustained error-budget consumption. Investigate latency, saturation, database connections and backlog before the next release."
    mime_type = "text/markdown"
  }
}

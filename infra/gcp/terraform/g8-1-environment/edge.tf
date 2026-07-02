resource "google_compute_security_policy" "edge" {
  count       = var.create_data_plane && var.create_edge ? 1 : 0
  project     = var.project_id
  name        = "np-${var.environment}-edge"
  description = "NatureProtector Cloud Armor WAF and rate limiting"
  type        = "CLOUD_ARMOR"

  adaptive_protection_config {
    layer_7_ddos_defense_config {
      enable = true
    }
  }
}

resource "google_compute_security_policy_rule" "login_rate_limit" {
  count           = var.create_data_plane && var.create_edge ? 1 : 0
  project         = var.project_id
  security_policy = google_compute_security_policy.edge[0].name
  priority        = 1000
  action          = "rate_based_ban"
  description     = "Protect authentication from brute force and resource abuse"
  match {
    expr {
      expression = "request.path.matches('/api/users-roles/login')"
    }
  }
  rate_limit_options {
    conform_action = "allow"
    exceed_action  = "deny(429)"
    enforce_on_key = "IP"
    rate_limit_threshold {
      count        = 20
      interval_sec = 60
    }
    ban_duration_sec = 900
    ban_threshold {
      count        = 60
      interval_sec = 300
    }
  }
}

resource "google_compute_security_policy_rule" "simulation_rate_limit" {
  count           = var.create_data_plane && var.create_edge ? 1 : 0
  project         = var.project_id
  security_policy = google_compute_security_policy.edge[0].name
  priority        = 1100
  action          = "throttle"
  description     = "Limit expensive simulation launch requests at the edge"
  match {
    expr {
      expression = "request.path.matches('/api/control/runtime/runs') && request.method == 'POST'"
    }
  }
  rate_limit_options {
    conform_action = "allow"
    exceed_action  = "deny(429)"
    enforce_on_key = "IP"
    rate_limit_threshold {
      count        = 12
      interval_sec = 300
    }
  }
}

resource "google_compute_security_policy_rule" "api_rate_limit" {
  count           = var.create_data_plane && var.create_edge ? 1 : 0
  project         = var.project_id
  security_policy = google_compute_security_policy.edge[0].name
  priority        = 1200
  action          = "throttle"
  description     = "General per-client API protection"
  match {
    expr {
      expression = "request.path.startsWith('/api/')"
    }
  }
  rate_limit_options {
    conform_action = "allow"
    exceed_action  = "deny(429)"
    enforce_on_key = "IP"
    rate_limit_threshold {
      count        = 600
      interval_sec = 60
    }
  }
}

resource "google_compute_security_policy_rule" "owasp_sqli_user_create" {
  count           = var.create_data_plane && var.create_edge ? 1 : 0
  project         = var.project_id
  security_policy = google_compute_security_policy.edge[0].name
  priority        = 95
  action          = "deny(403)"
  description     = "OWASP SQL injection protection for user creation with scoped exclusions"
  match {
    expr {
      expression = <<-EOT
        request.method == 'POST' &&
        request.path == '/api/users-roles/users' &&
        evaluatePreconfiguredWaf(
          'sqli-v33-stable',
          {
            'sensitivity': 1
          }
        )
      EOT
    }
  }
}

resource "google_compute_security_policy_rule" "owasp_sqli" {
  count           = var.create_data_plane && var.create_edge ? 1 : 0
  project         = var.project_id
  security_policy = google_compute_security_policy.edge[0].name
  priority        = 100
  action          = "deny(403)"
  description     = "OWASP SQL injection protection"
  match {
    expr {
      expression = <<-EOT
        request.path != '/api/users-roles/login' &&
        !(
          request.method == 'POST' &&
          request.path == '/api/users-roles/users'
        ) &&
        evaluatePreconfiguredWaf(
          'sqli-v33-stable',
          {
            'sensitivity': 4,
            'opt_out_rule_ids': [
              'owasp-crs-v030301-id942200-sqli',
              'owasp-crs-v030301-id942432-sqli'
            ]
          }
        )
      EOT
    }
  }
}

resource "google_compute_security_policy_rule" "owasp_xss" {
  count           = var.create_data_plane && var.create_edge ? 1 : 0
  project         = var.project_id
  security_policy = google_compute_security_policy.edge[0].name
  priority        = 110
  action          = "deny(403)"
  description     = "OWASP cross-site scripting protection"
  match {
    expr {
      expression = "evaluatePreconfiguredWaf('xss-v33-stable')"
    }
  }
}

resource "google_compute_security_policy_rule" "default" {
  count           = var.create_data_plane && var.create_edge ? 1 : 0
  project         = var.project_id
  security_policy = google_compute_security_policy.edge[0].name
  priority        = 2147483647
  action          = "allow"
  description     = "Default allow after explicit WAF and rate-limit rules"
  match {
    versioned_expr = "SRC_IPS_V1"
    config {
      src_ip_ranges = ["*"]
    }
  }
}

resource "google_compute_region_network_endpoint_group" "api" {
  count                 = var.create_data_plane && var.create_edge ? 1 : 0
  project               = var.project_id
  region                = var.region
  name                  = "np-${var.environment}-api"
  network_endpoint_type = "SERVERLESS"
  cloud_run {
    service = var.api_service_name
  }
}
resource "google_compute_region_network_endpoint_group" "frontend" {
  count                 = var.create_data_plane && var.create_edge ? 1 : 0
  project               = var.project_id
  region                = var.region
  name                  = "np-${var.environment}-frontend"
  network_endpoint_type = "SERVERLESS"
  cloud_run {
    service = var.frontend_service_name
  }
}

resource "google_compute_backend_service" "api" {
  count                  = var.create_data_plane && var.create_edge ? 1 : 0
  project                = var.project_id
  name                   = "np-${var.environment}-api"
  protocol               = "HTTPS"
  load_balancing_scheme  = "EXTERNAL_MANAGED"
  security_policy        = google_compute_security_policy.edge[0].id
  custom_request_headers = ["X-Forwarded-For:{client_ip_address},{server_ip_address}"]
  backend {
    group = google_compute_region_network_endpoint_group.api[0].id
  }
  log_config {
    enable      = true
    sample_rate = 1.0
  }
}
resource "google_compute_backend_service" "frontend" {
  count                  = var.create_data_plane && var.create_edge ? 1 : 0
  project                = var.project_id
  name                   = "np-${var.environment}-frontend"
  protocol               = "HTTPS"
  load_balancing_scheme  = "EXTERNAL_MANAGED"
  security_policy        = google_compute_security_policy.edge[0].id
  custom_request_headers = ["X-Forwarded-For:{client_ip_address},{server_ip_address}"]
  backend {
    group = google_compute_region_network_endpoint_group.frontend[0].id
  }
  log_config {
    enable      = true
    sample_rate = 1.0
  }
}

resource "google_compute_url_map" "https" {
  count           = var.create_data_plane && var.create_edge ? 1 : 0
  project         = var.project_id
  name            = "np-${var.environment}"
  default_service = google_compute_backend_service.frontend[0].id
  host_rule {
    hosts        = ["*"]
    path_matcher = "natureprotector"
  }
  path_matcher {
    name            = "natureprotector"
    default_service = google_compute_backend_service.frontend[0].id
    path_rule {
      paths   = ["/api", "/api/*"]
      service = google_compute_backend_service.api[0].id
    }
  }
}

resource "google_compute_managed_ssl_certificate" "https" {
  count   = var.create_data_plane && var.create_edge && length(var.managed_certificate_domains) > 0 ? 1 : 0
  project = var.project_id
  name    = "np-${var.environment}"
  managed {
    domains = var.managed_certificate_domains
  }
}

resource "google_compute_global_address" "https" {
  count   = var.create_data_plane && var.create_edge && length(var.managed_certificate_domains) > 0 ? 1 : 0
  project = var.project_id
  name    = "np-${var.environment}-https"
}

resource "google_compute_target_https_proxy" "https" {
  count            = var.create_data_plane && var.create_edge && length(var.managed_certificate_domains) > 0 ? 1 : 0
  project          = var.project_id
  name             = "np-${var.environment}"
  url_map          = google_compute_url_map.https[0].id
  ssl_certificates = [google_compute_managed_ssl_certificate.https[0].id]
}

resource "google_compute_global_forwarding_rule" "https" {
  count                 = var.create_data_plane && var.create_edge && length(var.managed_certificate_domains) > 0 ? 1 : 0
  project               = var.project_id
  name                  = "np-${var.environment}-https"
  ip_address            = google_compute_global_address.https[0].id
  port_range            = "443"
  target                = google_compute_target_https_proxy.https[0].id
  load_balancing_scheme = "EXTERNAL_MANAGED"
}

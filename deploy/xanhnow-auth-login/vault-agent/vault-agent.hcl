pid_file = "/srv/xanhnow/s101/secrets/auth-login/vault-agent.pid"

vault {
  address = "https://192.168.2.81:8200"
  ca_cert = "/etc/xanhnow/s101/auth-login/trust/vault-ca.crt"
}

auto_auth {
  method "approle" {
    mount_path = "auth/approle"
    config = {
      role_id_file_path = "/etc/xanhnow/s101/auth-login/vault/role_id"
      secret_id_file_path = "/etc/xanhnow/s101/auth-login/vault/secret_id"
      remove_secret_id_file_after_reading = false
    }
  }

  sink "file" {
    config = {
      path = "/srv/xanhnow/s101/secrets/auth-login/.vault-token"
      mode = 0600
    }
  }
}

template {
  source = "/etc/xanhnow/s101/auth-login/templates/postgres-connection-string.ctmpl"
  destination = "/srv/xanhnow/s101/secrets/auth-login/postgres-connection-string"
  perms = "0600"
}

template {
  source = "/etc/xanhnow/s101/auth-login/templates/redis-password.ctmpl"
  destination = "/srv/xanhnow/s101/secrets/auth-login/redis-password"
  perms = "0600"
}

template {
  source = "/etc/xanhnow/s101/auth-login/templates/redis-tls-enabled.ctmpl"
  destination = "/srv/xanhnow/s101/secrets/auth-login/redis-tls-enabled"
  perms = "0600"
}

template {
  source = "/etc/xanhnow/s101/auth-login/templates/password-pepper.ctmpl"
  destination = "/srv/xanhnow/s101/secrets/auth-login/password-pepper"
  perms = "0600"
}

template {
  source = "/etc/xanhnow/s101/auth-login/templates/password-pepper-version.ctmpl"
  destination = "/srv/xanhnow/s101/secrets/auth-login/password-pepper-version"
  perms = "0600"
}

template {
  source = "/etc/xanhnow/s101/auth-login/templates/password-algorithm.ctmpl"
  destination = "/srv/xanhnow/s101/secrets/auth-login/password-algorithm"
  perms = "0600"
}

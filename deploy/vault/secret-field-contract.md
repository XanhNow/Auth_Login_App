# Auth Login KV v2 field contract

No real values belong in this repository.

## `kv/xanhnow/auth-login/postgres/runtime`

Preferred field:

- `connection_string`

Expected runtime endpoint: `192.168.2.80:5432` through PgBouncer transaction pooling.

Runtime username: `xanhnow_auth`.

## `kv/xanhnow/auth-login/postgres/migration`

Preferred field:

- `connection_string`

Expected migration endpoint: `192.168.2.80:15432` through admin/primary path.

Migration username: `xanhnow_auth_migrator`.

## `kv/xanhnow/auth-login/redis`

- `password`
- `tls_enabled`

Redis endpoints remain in appsettings unless overridden:

```text
192.168.2.16:6379,192.168.2.33:6379,192.168.2.53:6379
```

## `kv/xanhnow/auth-login/kafka`

- `security_protocol`
- optional future fields: `sasl_mechanism`, `username`, `password`, `ssl_ca_location`

Kafka bootstrap servers and topic remain in appsettings unless overridden:

```text
192.168.2.14:9092,192.168.2.31:9092,192.168.2.51:9092
xanhnow.auth.login.events.v1
```

## `kv/xanhnow/auth-login/password-hashing`

Fields are consumed by the password hasher implementation. Do not print or commit values.

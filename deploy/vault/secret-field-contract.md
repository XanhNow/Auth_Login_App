# Auth Login KV v2 field contract

No real values belong in this repository.

## `kv/xanhnow/s101/auth-login/postgres/runtime`

Accepted formats:

- `connection_string`
- or split fields: `host`, `port`, `database`, `username`, `password`, optional `ssl_mode`

Expected runtime endpoint: `192.168.2.80:5432` through PgBouncer transaction pooling.

Runtime username: `xanhnow_auth`.

## `kv/xanhnow/s101/auth-login/postgres/migration`

Preferred field:

- `connection_string`

Expected migration endpoint: `192.168.2.80:15432` through admin/primary path.

Migration username: `xanhnow_auth_migrator`.

## `kv/xanhnow/s101/auth-login/redis`

- `password`
- `tls_enabled`

Redis endpoints remain in appsettings unless overridden:

```text
192.168.2.16:6379,192.168.2.33:6379,192.168.2.53:6379
```

## `kv/xanhnow/s101/auth-login/password-hashing`

Fields are consumed by the password hasher implementation. Do not print or commit values.

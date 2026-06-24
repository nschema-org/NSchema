-- NSchema project configuration. These blocks tell NSchema which database to
-- connect to and where to keep state. Config blocks may live in any .sql file.

PROVIDER sqlserver (
  -- Prefer the NSCHEMA_SQLSERVER_CONNECTION_STRING environment variable, which
  -- overrides the value below.
  connection_string = ''
  -- Credentials may be supplied separately from the connection string (e.g. from
  -- a secret store) via NSCHEMA_SQLSERVER_USERNAME / NSCHEMA_SQLSERVER_PASSWORD.
  -- They override any user/password embedded in connection_string.
);

BACKEND s3 (
  -- Credentials come from the standard AWS chain (environment, shared profile, or
  -- instance role), not from this block.
  bucket = 'my-nschema-state',
  key    = 'nschema.state.json'
);

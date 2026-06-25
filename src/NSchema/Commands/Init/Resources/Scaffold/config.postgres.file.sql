-- NSchema project configuration. These blocks tell NSchema which database to
-- connect to and where to keep state. Config blocks may live in any .sql file.

PROVIDER postgres (
  -- Prefer the NSCHEMA_POSTGRES_CONNECTION_STRING environment variable, which
  -- overrides the value below.
  connection_string = ''
  -- Credentials may be supplied separately from the connection string (e.g. from
  -- a secret store) via NSCHEMA_POSTGRES_USERNAME / NSCHEMA_POSTGRES_PASSWORD.
  -- They override any user/password embedded in connection_string.
);

BACKEND file (
  path = './nschema.state.json'
);

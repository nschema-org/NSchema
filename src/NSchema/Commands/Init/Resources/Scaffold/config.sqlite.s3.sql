-- NSchema project configuration. These blocks tell NSchema which database to
-- connect to and where to keep state. Config blocks may live in any .sql file.

PROVIDER sqlite (
  version           = '4.0.0-alpha.1',
  -- A local SQLite database file. The NSCHEMA_SQLITE_CONNECTION_STRING environment
  -- variable overrides the value below.
  connection_string = 'Data Source=app.db'
);

BACKEND s3 (
  version = '4.0.0-alpha.1',
  -- Credentials come from the standard AWS chain (environment, shared profile, or
  -- instance role), not from this block.
  bucket  = 'my-nschema-state',
  key     = 'nschema.state.json'
);

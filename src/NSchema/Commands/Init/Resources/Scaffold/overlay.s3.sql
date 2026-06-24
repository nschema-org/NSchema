-- Overlay for the 'prod' environment. Select it with:
--   nschema plan --environment prod
-- Any base block you don't override here still applies.

BACKEND s3 (
  bucket = 'my-nschema-state',
  key    = 'prod/nschema.state.json'
);

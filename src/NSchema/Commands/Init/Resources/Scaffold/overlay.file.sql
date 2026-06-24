-- Overlay for the 'prod' environment. Select it with:
--   nschema plan --environment prod
-- Any base block you don't override here still applies.

BACKEND file (
  path = './nschema.prod.state.json'
);

-- SQLite surfaces every object under the single 'main' schema, so declare tables
-- there and omit CREATE SCHEMA ('main' always exists).
CREATE TABLE main.widgets (
  id   bigint NOT NULL,
  name text,
  CONSTRAINT widgets_pkey PRIMARY KEY (id)
);

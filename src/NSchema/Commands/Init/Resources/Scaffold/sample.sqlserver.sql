CREATE SCHEMA app;

CREATE TABLE app.widgets (
  id   int NOT NULL,
  name varchar(100),
  CONSTRAINT widgets_pkey PRIMARY KEY (id)
);

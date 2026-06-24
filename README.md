# ![NSchema](https://raw.githubusercontent.com/nschema-org/NSchema.Docs/main/assets/nschema-logo-horizontal.png)

[![NSchema](https://github.com/nschema-org/NSchema/actions/workflows/cicd.yml/badge.svg)](https://github.com/nschema-org/NSchema/actions/workflows/cicd.yml)

# NSchema

NSchema is a declarative database schema migration tool. Write the schema you want in plain SQL, point NSchema at your database, and it will compute and apply the migration to get there.

It borrows the same `plan`, `apply` pattern from Terraform, and includes a lot of similar features like backend state, providers, and saved plan files.

Full documentation and provider support is available at **[nschema.dev](https://nschema.dev)**.

## Installation

```sh
dotnet tool install --global nschema
```

This installs the `nschema` command.

## Quickstart

```sh
nschema init     # scaffold a project (config + sample schema)
nschema plan     # preview the migration
nschema apply    # apply it
```

## Documentation

Full documentation lives at **[nschema.dev](https://nschema.dev)**:

- [Quickstart](https://nschema.dev/start/quickstart/) — from empty directory to applied schema
- [CLI reference](https://nschema.dev/cli/) — every command, flag, and exit code
- [DDL language](https://nschema.dev/ddl/defining-schemas/) — how to declare schemas
- [Configuration](https://nschema.dev/cli/configuration/) — providers, backends, and environment variables

## License

See [LICENSE](LICENSE).

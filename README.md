# ![NSchema](https://raw.githubusercontent.com/nschema-org/NSchema.Docs/main/assets/nschema-logo-horizontal.png)

[![NSchema](https://github.com/nschema-org/NSchema/actions/workflows/cicd.yml/badge.svg)](https://github.com/nschema-org/NSchema/actions/workflows/cicd.yml)

> [!WARNING]
> This project is still very new, and although I'm making great headway, there's no stable release just yet.
> Feel free to follow my progress though, or ask questions if you're interested. Just know it's not production ready just yet.

# NSchema

A declarative database schema migration tool. You describe the schema you want, and NSchema computes and applies the migration to get there — think *Terraform for your database schema*.

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

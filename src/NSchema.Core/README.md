# ![NSchema](https://raw.githubusercontent.com/nschema-org/NSchema.Docs/main/assets/nschema-logo-horizontal.png)

[![NSchema.Core](https://github.com/nschema-org/NSchema.Core/actions/workflows/cicd.yml/badge.svg)](https://github.com/nschema-org/NSchema.Core/actions/workflows/cicd.yml)

NSchema.Core is the engine behind the [NSchema CLI](https://github.com/nschema-org/NSchema), a declarative database schema migration tool. You describe the schema you want using familiar SQL syntax; the library compares it against the current state of your database and runs the SQL to bring it in line.

While this library can be consumed directly, I'd recommend using the [CLI tool](https://github.com/nschema-org/NSchema) unless you have a specific reason to build your own harness around the Core package.

## Installation

```sh
dotnet add package NSchema.Core
dotnet add package NSchema.Postgres   # or another provider
```

## Documentation

Full documentation lives at **[nschema.dev](https://nschema.dev)**. For embedding the engine directly in a .NET application:

- [Embedding the engine](https://nschema.dev/library/embedding/) — a getting-started walkthrough
- [Concepts & pipeline](https://nschema.dev/library/concepts/) — the domain model and how a run flows
- [Configuration (C#)](https://nschema.dev/library/configuration/) — building, running, operations, policies
- [Extension points](https://nschema.dev/library/extension-points/) — every interface you can swap or extend
- [DDL language](https://nschema.dev/ddl/defining-schemas/) and [grammar reference](https://nschema.dev/ddl/grammar/)

## License

See [LICENSE](LICENSE).

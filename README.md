# NSchema.Cli

[TODO]

## Configuration

### Connection string

The database connection string is a secret. Prefer supplying it through the environment:

```sh
export NSCHEMA_CONNECTION_STRING="..."
```

It can also be passed with `--connection-string` or set in `nschema.json`, but _please_ don't commit secrets to source control.

## License

See [LICENSE](LICENSE).

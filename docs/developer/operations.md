# Platform Operations

This guide covers running and managing a local Spring Voyage deployment.

## Running Locally

### Local Development

API Host in single-tenant mode + Dapr sidecar + Podman containers. Single machine.

```
dapr run --app-id spring-api --app-port 5000 -- dotnet run --project src/Cvoya.Spring.Host.Api -- --local
```

See [setup.md](setup.md) for full setup instructions.

## Health Checks

```
curl http://localhost:5001/health
```

Checks:
- API Host and Worker Host liveness
- Dapr sidecar connectivity
- State store connectivity
- Pub/sub broker connectivity
- Actor runtime health

## Database Migrations

Schema changes use EF Core migrations. `SpringDbContext` lives in the
`Cvoya.Spring.Dapr` project, and migrations target the Npgsql
(PostgreSQL) provider.

### Auto-migrate on startup (default)

The API and Worker hosts run a hosted service
(`Cvoya.Spring.Dapr.Data.DatabaseMigrator`) on startup that applies any
pending EF Core migrations. This is on by default so fresh
deployments and local dev databases come up with an up-to-date schema
without operator intervention.

You can disable it if you run migrations out-of-band (CI/CD pipeline,
scripted SQL deployment, or manual `dotnet ef database update`) by
setting the following in `appsettings.json` or an equivalent
environment variable:

```json
{
  "Database": {
    "AutoMigrate": false
  }
}
```

With the flag disabled, the host will log that it is skipping
migrations and assume the schema is already up to date.

### Adding a new migration

Run from the repository root after making model changes:

```
dotnet tool restore
dotnet ef migrations add <MigrationName> \
  --project src/Cvoya.Spring.Dapr \
  --output-dir Data/Migrations
```

Commit the generated files under `src/Cvoya.Spring.Dapr/Data/Migrations/`.

### Applying migrations externally

```
dotnet ef database update \
  --project src/Cvoya.Spring.Dapr \
  --connection "Host=...;Database=...;Username=...;Password=..."
```

To emit an idempotent SQL script (for a DBA-managed deployment):

```
dotnet ef migrations script \
  --idempotent \
  --project src/Cvoya.Spring.Dapr \
  --output spring-migrations.sql
```

### Design-time connection

`dotnet ef` uses `Cvoya.Spring.Dapr.Data.SpringDbContextDesignTimeFactory`
to build the context at design time. It uses a placeholder connection
string (never opened) to pin the Npgsql provider so generated
migrations target PostgreSQL. Pass `--connection` at `database update`
time to hit a real database.

### Test databases

Unit and integration tests run against the EF Core in-memory provider,
which does not support migrations. Test harnesses continue to rely on
the implicit schema that the in-memory provider materializes from the
model; `DatabaseMigrator` is a no-op against non-relational providers.

## Troubleshooting

### Agent Not Responding

1. Check agent status: `spring agent status <agent>`
2. Check the activity stream: `spring activity stream --agent <agent>`
3. Check for errors: `spring activity history --agent <agent> --type error`
4. Check the Dapr sidecar logs for actor activation issues

### Workflow Stuck

1. Check workflow status: `spring workflow status <id>`
2. Look for pending human-in-the-loop steps
3. Check the workflow container logs
4. Check for dead-lettered pub/sub messages

## Backup and Recovery

### Database

PostgreSQL backups cover all platform data:
- Agent definitions, activity history
- Actor runtime state (stored in PostgreSQL via Dapr state store)

Use standard PostgreSQL backup tools: `pg_dump`, continuous archiving, or managed database backups.

### Secret Rotation

Dapr Secrets building block supports rotation. Connectors re-authenticate when secrets change via Dapr Configuration change subscriptions.

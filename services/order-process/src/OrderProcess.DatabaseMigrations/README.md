# OrderProcess.DatabaseMigrations

Runnable console app that applies **database-first** migrations using **FluentMigrator**.

## Why
- Full control of **DDL/DML** using T-SQL scripts
- Version tracking in DB via `dbo.SchemaVersion`
- Designed to be a pipeline step in Azure DevOps

## Run

### Apply all migrations

```bash
dotnet run --project src/OrderProcess.DatabaseMigrations -- --connection "Server=localhost,1433;Database=contoso;User ID=sa;Password=Your_strong_Password123!;TrustServerCertificate=True;Encrypt=False"
```

### Apply up to a specific version

```bash
dotnet run --project src/OrderProcess.DatabaseMigrations -- --connection "<conn>" --to 1
```

## Inputs

- `--connection`: SQL Server / Azure SQL connection string
- `--to`: optional target version (defaults to latest)

## Scripts

- `Sql/0001_InitSchema.sql`
- `Sql/0002_SeedDemoData.sql`

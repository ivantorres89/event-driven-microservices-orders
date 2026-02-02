# syntax=docker/dockerfile:1

# ------------------------------------------------------------
# OrderProcess - multi-target Dockerfile
#   - target=worker      -> OrderProcess.Worker (long-running)
#   - target=migrations  -> OrderProcess.DatabaseMigrations (one-shot)
# ------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Copy csproj first for better layer caching (all projects referenced in the graph)
COPY ./src/OrderProcess.Worker/OrderProcess.Worker.csproj ./src/OrderProcess.Worker/
COPY ./src/OrderProcess.DatabaseMigrations/OrderProcess.DatabaseMigrations.csproj ./src/OrderProcess.DatabaseMigrations/
COPY ./src/OrderProcess.Application/OrderProcess.Application.csproj ./src/OrderProcess.Application/
COPY ./src/OrderProcess.Domain/OrderProcess.Domain.csproj ./src/OrderProcess.Domain/
COPY ./src/OrderProcess.Infrastructure/OrderProcess.Infrastructure.csproj ./src/OrderProcess.Infrastructure/
COPY ./src/OrderProcess.Persistence/OrderProcess.Persistence.csproj ./src/OrderProcess.Persistence/
COPY ./src/OrderProcess.Persistence.Abstractions/OrderProcess.Persistence.Abstractions.csproj ./src/OrderProcess.Persistence.Abstractions/
COPY ./src/OrderProcess.Shared/OrderProcess.Shared.csproj ./src/OrderProcess.Shared/

# Restore both entrypoint projects so both targets can reuse the layer
RUN dotnet restore ./src/OrderProcess.Worker/OrderProcess.Worker.csproj \
 && dotnet restore ./src/OrderProcess.DatabaseMigrations/OrderProcess.DatabaseMigrations.csproj

# Copy source
COPY ./src ./src

# --------------------
# Publish: Worker
# --------------------
FROM restore AS publish-worker
RUN dotnet publish ./src/OrderProcess.Worker/OrderProcess.Worker.csproj -c Release -o /app/worker /p:UseAppHost=false

# --------------------
# Publish: Migrations
# --------------------
FROM restore AS publish-migrations
RUN dotnet publish ./src/OrderProcess.DatabaseMigrations/OrderProcess.DatabaseMigrations.csproj -c Release -o /app/migrations /p:UseAppHost=false

# --------------------
# Runtime: Worker
# --------------------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS worker
WORKDIR /app
COPY --from=publish-worker /app/worker ./
ENTRYPOINT ["dotnet", "OrderProcess.Worker.dll"]

# --------------------
# Runtime: Migrations (one-shot)
# --------------------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS migrations
WORKDIR /app
COPY --from=publish-migrations /app/migrations ./
ENTRYPOINT ["dotnet", "OrderProcess.DatabaseMigrations.dll"]

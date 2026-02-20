# Backstage (Developer Portal) — Portfolio Setup

## What is Backstage?

Backstage is an **open-source framework** for building a **Developer Portal**: a single UI where engineering teams can discover services, owners, docs (TechDocs), runbooks, APIs, and operational links (dashboards, pipelines, alerts, etc.). Backstage started at Spotify and is now a CNCF project.

In practice, you can think of it as:

- a **service catalog** (inventory + ownership + metadata)
- a **docs hub** (TechDocs: MkDocs rendered inside Backstage)
- a **link hub** (dashboards, runbooks, pipelines, repos)
- optionally: **software templates** (scaffolding new services) and integrations (Kubernetes, CI, cloud, etc.)

## Why it’s useful for Platform teams

A platform usually operates many building blocks (AKS, Azure SQL, Service Bus, Redis, microservices, etc.). Backstage gives you a **standardized contract** for each service/component:

- `owner`, `repo`, `runbook`, `dashboard`, `pipeline` (and whatever else your org mandates)

This repo uses **static YAML** (`catalog-info.yaml`) to demonstrate the portal without any Azure discovery.

## How this repo is wired

- Each component (services + frontend + architecture docs) contains:
  - `catalog-info.yaml` (entity definition)
  - `mkdocs.yml` + `docs/` (TechDocs)
- The repo root has a `catalog-info.yaml` that defines:
  - the **Domain**, **System**, **Resources**, and a **Location** that points at all components

## Run Backstage locally (free)

Backstage is free/open-source — you only pay for the compute if you host it.

### Prerequisites

- Node.js **Active LTS** (see Backstage docs)
- Yarn Classic (v1)
- Docker (recommended) for TechDocs generation (`mkdocs` runs in a container)

### Option A (recommended): Scaffold the app, then apply this repo config

1) From repo root, create a Backstage app in `./backstage`:

```bash
npx @backstage/create-app@latest --path ./backstage
```

2) Copy the portfolio configs:

```bash
cp developer-portal/app-config.contoso.yaml backstage/app-config.yaml
cp developer-portal/app-config.production.contoso.yaml backstage/app-config.production.yaml
```

3) Start the portal:

```bash
cd backstage
yarn install
yarn dev
```

Open:

- UI: `http://localhost:3000`
- Backend: `http://localhost:7007`

Login: choose **Guest**.

### Option B: Helper scripts

```bash
./scripts/backstage-init.sh
./scripts/backstage-start.sh
```

## What you should see in the UI

- **Catalog → Components**:
  - `order-accept`, `order-process`, `order-notification`, `contoso-frontend`, `contoso-architecture`
- Each component has:
  - **Links** (repo, local endpoints, architecture links)
  - **Docs** tab (TechDocs) where the runbook is accessible
- **Catalog → Systems**:
  - `contoso-orders` with architecture links

## Next steps (optional evolutions)

- Add real integrations (GitHub, Azure DevOps, AKS, Azure Resource Graph)
- Enforce golden-path metadata (ownership, tier, SLO, pager rotation)
- Add “live architecture” via Kubernetes plugin + service topology

Repository: https://github.com/ivantorres89/event-driven-microservices-orders

# Cloud deployment (AKS per environment) – GitHub Actions + Terraform

This repository is a monorepo demo. Cloud infrastructure and pipelines live under `/infra` and `.github/workflows`.

## What gets provisioned per environment

Terraform creates (per env in its own Resource Group):

- AKS cluster
- ACR
- Azure SQL Database (Business Critical by default – expensive!)
- Azure Cache for Redis
- Azure Service Bus with two queues: `order.accepted`, `order.processed`
- API Management (optional, enabled by default)

Tags are applied to every resource:

- `workload = contoso-orders`
- `env = dev|test|staging|prod`

## GitHub Actions deployment strategy

- `dev` deploys on every push to `main`
- `test`, `staging`, `prod` deploy by tags:
  - `test-1.0.0`
  - `staging-1.0.0`
  - `prod-1.0.0`

## One-time setup (Terraform state backend)

Terraform uses the AzureRM backend. Run locally once:

```bash
az login
bash infra/scripts/bootstrap-tfstate.sh
```

Then create GitHub **Environment secrets** for each environment (`dev`, `test`, `staging`, `prod`):

- `TFSTATE_RG`
- `TFSTATE_STORAGE_ACCOUNT`
- `TFSTATE_CONTAINER`

## One-time setup (OIDC / Workload Identity Federation)

Use a single Azure App Registration (or one per env) and configure GitHub OIDC federated credentials.

Required GitHub **Environment secrets** (per env):

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Terraform uses these env vars:

- `ARM_USE_OIDC=true`
- `ARM_CLIENT_ID`
- `ARM_TENANT_ID`
- `ARM_SUBSCRIPTION_ID`

## Required secrets for Azure SQL

Per environment secrets:

- `SQL_ADMIN_LOGIN`
- `SQL_ADMIN_PASSWORD`

> For a demo, keep these consistent across envs. In production, use Key Vault and rotate.

## Optional APIM publisher settings

Per environment (or repo-level) secrets:

- `APIM_PUBLISHER_NAME`
- `APIM_PUBLISHER_EMAIL`

## Deploying

- `dev`: push to `main`
- `test/staging/prod`: create a tag and push it:

```bash
git tag test-1.0.0
git push origin test-1.0.0
```

The workflow will:
1) `terraform apply` for the environment
2) Build & push docker images to ACR
3) Install `ingress-nginx` via Helm
4) Create K8S secret with SQL/Redis/ServiceBus connection strings
5) Run DB migrations as a Kubernetes Job
6) Deploy workloads and Ingress

## Notes

- Azure SQL Business Critical and APIM Standard are **expensive**. For cheap demos:
  - switch SQL SKU to a cheaper tier in `infra/tf/envs/*/terraform.tfvars.example`
  - use `apim_sku_name = "Developer_1"` or disable `enable_apim = false`

- APIM JWT policy example is in `infra/apim/policies/validate-jwt.xml`.
  In a real setup you would also configure APIM to route to the AKS ingress public IP / DNS.

### Dev image versioning (GitVersion)

For `dev` deployments (push to `main`), the workflow uses **GitVersion** to calculate a base SemVer and then produces a Docker-safe tag:

`<MajorMinorPatch>-dev.<run_number>`

Example: `0.1.0-dev.42`

If you want to bump the base version, create a normal SemVer tag on `main` with the `v` prefix (GitVersion reads only those), e.g.:

- `v1.0.0`
- `v1.1.0`

Environment promotion tags (`test-*`, `staging-*`, `prod-*`) are **not** used by GitVersion.


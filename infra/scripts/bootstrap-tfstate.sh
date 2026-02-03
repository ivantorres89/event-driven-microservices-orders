#!/usr/bin/env bash
set -euo pipefail

# Bootstraps an Azure Storage Account + blob container to store Terraform state.
# Intended to run locally once per subscription, then copy the outputs into GitHub Environment secrets.

TFSTATE_RG="${TFSTATE_RG:-rg-contoso-orders-tfstate}"
TFSTATE_LOCATION="${TFSTATE_LOCATION:-westeurope}"
TFSTATE_CONTAINER="${TFSTATE_CONTAINER:-tfstate}"

echo "Using:"
echo "  TFSTATE_RG=$TFSTATE_RG"
echo "  TFSTATE_LOCATION=$TFSTATE_LOCATION"
echo "  TFSTATE_CONTAINER=$TFSTATE_CONTAINER"

az account show >/dev/null

# Create RG
az group create -n "$TFSTATE_RG" -l "$TFSTATE_LOCATION" >/dev/null

# Create storage account name if not supplied
if [[ -z "${TFSTATE_STORAGE_ACCOUNT:-}" ]]; then
  # Storage account names must be globally unique, 3-24, lowercase/numbers only.
  RAND="$(cat /dev/urandom | tr -dc 'a-z0-9' | head -c 6)"
  TFSTATE_STORAGE_ACCOUNT="stcoorders${RAND}"
fi

echo "  TFSTATE_STORAGE_ACCOUNT=$TFSTATE_STORAGE_ACCOUNT"

# Create storage account (idempotent)
az storage account create   -g "$TFSTATE_RG"   -n "$TFSTATE_STORAGE_ACCOUNT"   -l "$TFSTATE_LOCATION"   --sku Standard_LRS   --kind StorageV2   --min-tls-version TLS1_2   >/dev/null

# Create container
ACCOUNT_KEY="$(az storage account keys list -g "$TFSTATE_RG" -n "$TFSTATE_STORAGE_ACCOUNT" --query '[0].value' -o tsv)"
az storage container create   --name "$TFSTATE_CONTAINER"   --account-name "$TFSTATE_STORAGE_ACCOUNT"   --account-key "$ACCOUNT_KEY"   >/dev/null

echo ""
echo "✅ Terraform state backend ready."
echo ""
echo "Set these GitHub Environment secrets:"
echo "  TFSTATE_RG=$TFSTATE_RG"
echo "  TFSTATE_STORAGE_ACCOUNT=$TFSTATE_STORAGE_ACCOUNT"
echo "  TFSTATE_CONTAINER=$TFSTATE_CONTAINER"

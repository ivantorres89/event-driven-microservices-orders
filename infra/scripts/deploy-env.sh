#!/usr/bin/env bash
set -euo pipefail

ENV_NAME="${1:-}"
VERSION="${2:-}"

if [[ -z "$ENV_NAME" || -z "$VERSION" ]]; then
  echo "Usage: $0 <dev|test|staging|prod> <version-tag-or-sha>"
  exit 2
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TF_DIR="$ROOT_DIR/infra/tf/envs/$ENV_NAME"

if [[ ! -d "$TF_DIR" ]]; then
  echo "Terraform env folder not found: $TF_DIR"
  exit 2
fi

: "${AZURE_SUBSCRIPTION_ID:?Missing AZURE_SUBSCRIPTION_ID}"
: "${TFSTATE_RG:?Missing TFSTATE_RG}"
: "${TFSTATE_STORAGE_ACCOUNT:?Missing TFSTATE_STORAGE_ACCOUNT}"
: "${TFSTATE_CONTAINER:?Missing TFSTATE_CONTAINER}"
: "${SQL_ADMIN_LOGIN:?Missing SQL_ADMIN_LOGIN}"
: "${SQL_ADMIN_PASSWORD:?Missing SQL_ADMIN_PASSWORD}"

LOCATION="${LOCATION:-westeurope}"

echo "==> Terraform apply ($ENV_NAME)"
pushd "$TF_DIR" >/dev/null

terraform --version
terraform init -input=false   -backend-config="subscription_id=$AZURE_SUBSCRIPTION_ID"   -backend-config="resource_group_name=$TFSTATE_RG"   -backend-config="storage_account_name=$TFSTATE_STORAGE_ACCOUNT"   -backend-config="container_name=$TFSTATE_CONTAINER"   -backend-config="key=contoso-orders-$ENV_NAME.tfstate"

terraform apply -auto-approve -input=false   -var "location=$LOCATION"   -var "sql_admin_login=$SQL_ADMIN_LOGIN"   -var "sql_admin_password=$SQL_ADMIN_PASSWORD"   ${APIM_PUBLISHER_NAME:+-var "apim_publisher_name=$APIM_PUBLISHER_NAME"}   ${APIM_PUBLISHER_EMAIL:+-var "apim_publisher_email=$APIM_PUBLISHER_EMAIL"}

RG_NAME="$(terraform output -raw resource_group_name)"
AKS_NAME="$(terraform output -raw aks_name)"
ACR_NAME="$(terraform output -raw acr_name)"
ACR_LOGIN_SERVER="$(terraform output -raw acr_login_server)"

SQL_CONN="$(terraform output -raw sql_connection_string)"
REDIS_CONN="$(terraform output -raw redis_connection_string)"
SB_CONN="$(terraform output -raw servicebus_connection_string)"

popd >/dev/null

echo "==> ACR login"
az acr login --name "$ACR_NAME"

TAG="$VERSION"

IMAGE_ORDER_ACCEPT="${ACR_LOGIN_SERVER}/order-accept:${TAG}"
IMAGE_ORDER_PROCESS="${ACR_LOGIN_SERVER}/order-process:${TAG}"
IMAGE_ORDER_PROCESS_MIGRATIONS="${ACR_LOGIN_SERVER}/order-process-migrations:${TAG}"
IMAGE_ORDER_NOTIFICATION="${ACR_LOGIN_SERVER}/order-notification:${TAG}"

echo "==> Docker build & push"
docker build -t "$IMAGE_ORDER_ACCEPT" "$ROOT_DIR/services/order-accept"
docker push "$IMAGE_ORDER_ACCEPT"

docker build --target worker -t "$IMAGE_ORDER_PROCESS" "$ROOT_DIR/services/order-process"
docker push "$IMAGE_ORDER_PROCESS"

docker build --target migrations -t "$IMAGE_ORDER_PROCESS_MIGRATIONS" "$ROOT_DIR/services/order-process"
docker push "$IMAGE_ORDER_PROCESS_MIGRATIONS"

docker build -t "$IMAGE_ORDER_NOTIFICATION" "$ROOT_DIR/services/order-notification"
docker push "$IMAGE_ORDER_NOTIFICATION"

echo "==> AKS credentials"
az aks get-credentials -g "$RG_NAME" -n "$AKS_NAME" --overwrite-existing

echo "==> Install ingress-nginx (if needed)"
if ! kubectl get ns ingress-nginx >/dev/null 2>&1; then
  kubectl create namespace ingress-nginx >/dev/null
fi

if ! helm status ingress-nginx -n ingress-nginx >/dev/null 2>&1; then
  helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx >/dev/null
  helm repo update >/dev/null
  helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx -n ingress-nginx >/dev/null
fi

echo "==> Apply namespace"
kubectl apply -f "$ROOT_DIR/infra/k8s/base/00-namespace.yaml"

echo "==> Apply secrets"
kubectl -n contoso-orders create secret generic contoso-orders-secrets   --from-literal=ConnectionStrings__Contoso="$SQL_CONN"   --from-literal=ConnectionStrings__Redis="$REDIS_CONN"   --from-literal=AzureServiceBus__ConnectionString="$SB_CONN"   --dry-run=client -o yaml | kubectl apply -f -

echo "==> Run migrations job"
kubectl -n contoso-orders delete job order-process-migrations --ignore-not-found >/dev/null 2>&1 || true
export IMAGE_ORDER_PROCESS_MIGRATIONS
envsubst < "$ROOT_DIR/infra/k8s/base/10-order-process-migrations-job.yaml" | kubectl apply -f -

if ! kubectl -n contoso-orders wait --for=condition=complete job/order-process-migrations --timeout=10m; then
  echo "❌ Migrations job failed. Logs:"
  kubectl -n contoso-orders logs job/order-process-migrations || true
  exit 1
fi

echo "==> Deploy workloads"
export IMAGE_ORDER_ACCEPT IMAGE_ORDER_PROCESS IMAGE_ORDER_NOTIFICATION

envsubst < "$ROOT_DIR/infra/k8s/base/20-order-accept-deployment.yaml" | kubectl apply -f -
kubectl apply -f "$ROOT_DIR/infra/k8s/base/21-order-accept-service.yaml"

envsubst < "$ROOT_DIR/infra/k8s/base/30-order-process-deployment.yaml" | kubectl apply -f -
envsubst < "$ROOT_DIR/infra/k8s/base/40-order-notification-deployment.yaml" | kubectl apply -f -

kubectl apply -f "$ROOT_DIR/infra/k8s/base/50-ingress.yaml"

echo "✅ Deploy complete."
echo "Next: wait for ingress external IP:"
echo "  kubectl -n ingress-nginx get svc ingress-nginx-controller"

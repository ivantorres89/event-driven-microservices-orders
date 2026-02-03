terraform {
  # Backend is configured at init time via -backend-config in infra/scripts/deploy-env.sh
  backend "azurerm" {}
}

module "contoso_orders" {
  source = "../../modules/contoso-orders"

  env      = "staging"
  location = var.location
  tags     = var.tags

  sql_admin_login    = var.sql_admin_login
  sql_admin_password = var.sql_admin_password

  # Optional tuning
  aks_node_vm_size      = var.aks_node_vm_size
  aks_node_min_count    = var.aks_node_min_count
  aks_node_max_count    = var.aks_node_max_count

  sql_database_sku_name = var.sql_database_sku_name
  sql_zone_redundant    = var.sql_zone_redundant

  redis_sku_name = var.redis_sku_name
  redis_family   = var.redis_family
  redis_capacity = var.redis_capacity

  enable_apim     = var.enable_apim
  apim_sku_name   = var.apim_sku_name
  apim_publisher_name  = var.apim_publisher_name
  apim_publisher_email = var.apim_publisher_email
}

output "resource_group_name" {
  value = module.contoso_orders.resource_group_name
}

output "aks_name" {
  value = module.contoso_orders.aks_name
}

output "acr_name" {
  value = module.contoso_orders.acr_name
}

output "acr_login_server" {
  value = module.contoso_orders.acr_login_server
}

output "sql_connection_string" {
  value     = module.contoso_orders.sql_connection_string
  sensitive = true
}

output "redis_connection_string" {
  value     = module.contoso_orders.redis_connection_string
  sensitive = true
}

output "servicebus_connection_string" {
  value     = module.contoso_orders.servicebus_connection_string
  sensitive = true
}

output "apim_gateway_url" {
  value = module.contoso_orders.apim_gateway_url
}

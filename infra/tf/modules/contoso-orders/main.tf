data "azurerm_client_config" "current" {}

resource "random_string" "suffix" {
  length  = 5
  upper   = false
  special = false
}

locals {
  # Tags for governance/cost control
  common_tags = merge(
    var.tags,
    {
      workload = var.resource_name_prefix
      env      = var.env
    }
  )

  # Resource names (keep within Azure naming constraints)
  rg_name          = "rg-${var.resource_name_prefix}-${var.env}"
  aks_name         = "aks-${var.resource_name_prefix}-${var.env}"
  la_name          = "la-${var.resource_name_prefix}-${var.env}"
  acr_name         = substr(replace("acr${var.resource_name_prefix}${var.env}${random_string.suffix.result}", "-", ""), 0, 50)
  sql_server_name  = substr(replace("sql${var.resource_name_prefix}${var.env}${random_string.suffix.result}", "-", ""), 0, 63)
  sb_name          = substr("sb-${var.resource_name_prefix}-${var.env}-${random_string.suffix.result}", 0, 50)
  redis_name       = substr("redis-${var.resource_name_prefix}-${var.env}-${random_string.suffix.result}", 0, 63)
  apim_name        = substr("apim-${var.resource_name_prefix}-${var.env}-${random_string.suffix.result}", 0, 50)
}

resource "azurerm_resource_group" "rg" {
  name     = local.rg_name
  location = var.location
  tags     = local.common_tags
}

# --- Log Analytics for AKS ---
resource "azurerm_log_analytics_workspace" "la" {
  name                = local.la_name
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.common_tags
}

# --- ACR ---
resource "azurerm_container_registry" "acr" {
  name                = local.acr_name
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  sku                 = var.acr_sku
  admin_enabled       = false
  tags                = local.common_tags
}

# --- AKS ---
resource "azurerm_kubernetes_cluster" "aks" {
  name                = local.aks_name
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  dns_prefix          = local.aks_name

  identity {
    type = "SystemAssigned"
  }

  default_node_pool {
    name                = "system"
    vm_size             = var.aks_node_vm_size
    enable_auto_scaling = true
    min_count           = var.aks_node_min_count
    max_count           = var.aks_node_max_count
    os_disk_size_gb     = 64
    type                = "VirtualMachineScaleSets"
  }

  oms_agent {
    log_analytics_workspace_id = azurerm_log_analytics_workspace.la.id
  }

  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  network_profile {
    network_plugin    = "azure"
    load_balancer_sku = "standard"
  }

  tags = local.common_tags
}

# Allow AKS nodes to pull from ACR
resource "azurerm_role_assignment" "aks_acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
}

# --- Azure SQL (Business Critical) ---
resource "azurerm_mssql_server" "sql" {
  name                         = local.sql_server_name
  resource_group_name          = azurerm_resource_group.rg.name
  location                     = azurerm_resource_group.rg.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"
  public_network_access_enabled = true
  tags                         = local.common_tags
}

# Allow Azure services (including AKS) to access SQL
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.sql.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_mssql_firewall_rule" "custom" {
  for_each = { for r in var.sql_firewall_rules : r.name => r }

  name             = each.value.name
  server_id        = azurerm_mssql_server.sql.id
  start_ip_address = each.value.start_ip
  end_ip_address   = each.value.end_ip
}

resource "azurerm_mssql_database" "db" {
  name                 = var.sql_database_name
  server_id            = azurerm_mssql_server.sql.id
  sku_name             = var.sql_database_sku_name
  zone_redundant       = var.sql_zone_redundant
  max_size_gb          = 32
  read_scale           = false
  tags                 = local.common_tags
}

# --- Redis (Azure Cache for Redis) ---
resource "azurerm_redis_cache" "redis" {
  name                = local.redis_name
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name

  capacity = var.redis_capacity
  family   = var.redis_family
  sku_name = var.redis_sku_name

  enable_non_ssl_port = false
  minimum_tls_version = "1.2"

  redis_configuration {}

  tags = local.common_tags
}

# --- Service Bus ---
resource "azurerm_servicebus_namespace" "sb" {
  name                = local.sb_name
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = var.servicebus_sku
  tags                = local.common_tags
}

resource "azurerm_servicebus_queue" "queues" {
  for_each = toset(var.servicebus_queues)

  name         = each.value
  namespace_id = azurerm_servicebus_namespace.sb.id

  enable_partitioning = false
}

resource "azurerm_servicebus_namespace_authorization_rule" "app" {
  name         = "app"
  namespace_id = azurerm_servicebus_namespace.sb.id

  listen = true
  send   = true
  manage = true
}

# --- APIM (perimeter) ---
resource "azurerm_api_management" "apim" {
  count               = var.enable_apim ? 1 : 0
  name                = local.apim_name
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name

  publisher_name  = var.apim_publisher_name
  publisher_email = var.apim_publisher_email

  sku_name = var.apim_sku_name

  tags = local.common_tags
}

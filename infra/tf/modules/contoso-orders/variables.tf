variable "env" {
  description = "Environment name: dev|test|staging|prod"
  type        = string
}

variable "location" {
  description = "Azure region, e.g. westeurope"
  type        = string
  default     = "westeurope"
}

variable "tags" {
  description = "Additional tags to apply to all resources"
  type        = map(string)
  default     = {}
}

variable "resource_name_prefix" {
  description = "Prefix used for resource naming"
  type        = string
  default     = "contoso-orders"
}

# --- AKS ---
variable "aks_node_vm_size" {
  type        = string
  default     = "Standard_B2s"
}

variable "aks_node_min_count" {
  type        = number
  default     = 1
}

variable "aks_node_max_count" {
  type        = number
  default     = 3
}

# --- ACR ---
variable "acr_sku" {
  type        = string
  default     = "Basic"
}

# --- Azure SQL ---
variable "sql_admin_login" {
  description = "SQL Server admin login"
  type        = string
}

variable "sql_admin_password" {
  description = "SQL Server admin password"
  type        = string
  sensitive   = true
}

variable "sql_database_name" {
  type        = string
  default     = "contoso"
}

variable "sql_database_sku_name" {
  description = "Azure SQL Database SKU name. Business Critical example: BC_Gen5_2"
  type        = string
  default     = "BC_Gen5_2"
}

variable "sql_zone_redundant" {
  description = "Enable zone redundancy (where supported by region/SKU)"
  type        = bool
  default     = false
}

variable "sql_firewall_rules" {
  description = "Optional additional SQL firewall rules (your IPs)."
  type = list(object({
    name     = string
    start_ip = string
    end_ip   = string
  }))
  default = []
}

# --- Redis ---
variable "redis_sku_name" {
  description = "Azure Cache for Redis SKU: Basic|Standard|Premium"
  type        = string
  default     = "Standard"
}

variable "redis_family" {
  description = "Redis family: C for Basic/Standard, P for Premium"
  type        = string
  default     = "C"
}

variable "redis_capacity" {
  description = "Redis capacity (0..6 for C family)"
  type        = number
  default     = 1
}

# --- Service Bus ---
variable "servicebus_sku" {
  type    = string
  default = "Standard"
}

variable "servicebus_queues" {
  description = "Queues to create"
  type        = list(string)
  default     = ["order.accepted", "order.processed"]
}

# --- APIM ---
variable "enable_apim" {
  type        = bool
  default     = true
}

variable "apim_sku_name" {
  description = "APIM SKU name: Developer_1, Standard_1, etc."
  type        = string
  default     = "Developer_1"
}

variable "apim_publisher_name" {
  type        = string
  default     = "Contoso"
}

variable "apim_publisher_email" {
  type        = string
  default     = "contoso@example.com"
}

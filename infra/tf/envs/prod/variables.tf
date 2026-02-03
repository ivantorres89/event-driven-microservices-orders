variable "location" {
  type    = string
  default = "westeurope"
}

variable "tags" {
  type    = map(string)
  default = {}
}

variable "sql_admin_login" {
  type = string
}

variable "sql_admin_password" {
  type      = string
  sensitive = true
}

variable "aks_node_vm_size" { type = string default = "Standard_B2s" }
variable "aks_node_min_count" { type = number default = 1 }
variable "aks_node_max_count" { type = number default = 3 }

variable "sql_database_sku_name" { type = string default = "BC_Gen5_2" }
variable "sql_zone_redundant" { type = bool default = false }

variable "redis_sku_name" { type = string default = "Standard" }
variable "redis_family" { type = string default = "C" }
variable "redis_capacity" { type = number default = 1 }

variable "enable_apim" { type = bool default = true }
variable "apim_sku_name" { type = string default = "Developer_1" }
variable "apim_publisher_name" { type = string default = "Contoso" }
variable "apim_publisher_email" { type = string default = "contoso@example.com" }

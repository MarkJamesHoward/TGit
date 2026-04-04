# Azure SQL Database resources
# NOTE: These resources are created manually in the Azure portal to ensure
# the free tier can be selected. This file is kept as documentation.

# resource "azurerm_mssql_server" "tgit" {
#   name                         = "tgit-sql-server"
#   resource_group_name          = azurerm_resource_group.tgit.name
#   location                     = azurerm_resource_group.tgit.location
#   version                      = "12.0"
#
#   azuread_administrator {
#     login_username              = data.azuread_service_principal.deploy.display_name
#     object_id                   = data.azuread_service_principal.deploy.object_id
#     azuread_authentication_only = true
#   }
# }

# resource "azurerm_mssql_database" "tgit" {
#   name      = "tgit"
#   server_id = azurerm_mssql_server.tgit.id
#   sku_name  = "Free"
# }

# resource "azurerm_mssql_firewall_rule" "allow_azure" {
#   name             = "AllowAzureServices"
#   server_id        = azurerm_mssql_server.tgit.id
#   start_ip_address = "0.0.0.0"
#   end_ip_address   = "0.0.0.0"
# }

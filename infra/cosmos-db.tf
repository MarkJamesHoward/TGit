# CosmosDB resources — replaced by Azure SQL Database
# Kept as documentation of previous infrastructure.

# resource "azurerm_cosmosdb_account" "tgit" {
#   name                         = "tgit"
#   location                     = azurerm_resource_group.tgit.location
#   resource_group_name          = azurerm_resource_group.tgit.name
#   offer_type                   = "Standard"
#   kind                         = "GlobalDocumentDB"
#   automatic_failover_enabled   = true
#   free_tier_enabled            = true
#
#   consistency_policy {
#     consistency_level = "Session"
#   }
#
#   capacity {
#     total_throughput_limit = 4000
#   }
#
#   geo_location {
#     location          = azurerm_resource_group.tgit.location
#     failover_priority = 0
#   }
# }

# API database and container
# resource "azurerm_cosmosdb_sql_database" "tgit" {
#   name                = "tgit"
#   resource_group_name = azurerm_resource_group.tgit.name
#   account_name        = azurerm_cosmosdb_account.tgit.name
# }

# resource "azurerm_cosmosdb_sql_container" "users" {
#   name                = "users"
#   resource_group_name = azurerm_resource_group.tgit.name
#   account_name        = azurerm_cosmosdb_account.tgit.name
#   database_name       = azurerm_cosmosdb_sql_database.tgit.name
#   partition_key_paths = ["/userEmail"]
# }

# Dashboard database and containers
# resource "azurerm_cosmosdb_sql_database" "tgit_dashboard" {
#   name                = "tgit-dashboard"
#   resource_group_name = azurerm_resource_group.tgit.name
#   account_name        = azurerm_cosmosdb_account.tgit.name
# }

# resource "azurerm_cosmosdb_sql_container" "passkey_users" {
#   name                = "passkey_users"
#   resource_group_name = azurerm_resource_group.tgit.name
#   account_name        = azurerm_cosmosdb_account.tgit.name
#   database_name       = azurerm_cosmosdb_sql_database.tgit_dashboard.name
#   partition_key_paths = ["/id"]
# }

# resource "azurerm_cosmosdb_sql_container" "passkey_credentials" {
#   name                = "passkey_credentials"
#   resource_group_name = azurerm_resource_group.tgit.name
#   account_name        = azurerm_cosmosdb_account.tgit.name
#   database_name       = azurerm_cosmosdb_sql_database.tgit_dashboard.name
#   partition_key_paths = ["/userId"]
# }

# resource "azurerm_cosmosdb_sql_container" "passkey_sessions" {
#   name                = "passkey_sessions"
#   resource_group_name = azurerm_resource_group.tgit.name
#   account_name        = azurerm_cosmosdb_account.tgit.name
#   database_name       = azurerm_cosmosdb_sql_database.tgit_dashboard.name
#   partition_key_paths = ["/id"]
# }

# resource "azurerm_cosmosdb_sql_container" "passkey_challenges" {
#   name                = "passkey_challenges"
#   resource_group_name = azurerm_resource_group.tgit.name
#   account_name        = azurerm_cosmosdb_account.tgit.name
#   database_name       = azurerm_cosmosdb_sql_database.tgit_dashboard.name
#   partition_key_paths = ["/id"]
# }

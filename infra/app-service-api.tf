resource "azurerm_linux_web_app" "tgit_api" {
  name                = "TGit-API"
  location            = azurerm_resource_group.tgit.location
  resource_group_name = azurerm_resource_group.tgit.name
  service_plan_id     = azurerm_service_plan.tgit.id

  site_config {
    application_stack {
      dotnet_version = "9.0"
    }
  }

  identity {
    type = "SystemAssigned"
  }

  app_settings = {
    "Storage__Type"        = "sql"
    "Sql__ConnectionString" = "Server=hvhsejcxpv.database.windows.net;Database=tgit-database;Authentication=Active Directory Default;Encrypt=true;TrustServerCertificate=false;"
  }
}

# Custom domain — no longer used, using default azurewebsites.net URL
# resource "azurerm_app_service_custom_hostname_binding" "tgit_api" {
#   hostname            = "api.tgit.app"
#   app_service_name    = azurerm_linux_web_app.tgit_api.name
#   resource_group_name = azurerm_resource_group.tgit.name
# }

# resource "azurerm_app_service_managed_certificate" "tgit_api" {
#   custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.tgit_api.id
# }

# resource "azurerm_app_service_certificate_binding" "tgit_api" {
#   hostname_binding_id = azurerm_app_service_custom_hostname_binding.tgit_api.id
#   certificate_id      = azurerm_app_service_managed_certificate.tgit_api.id
#   ssl_state           = "SniEnabled"
# }

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
}

# Custom domain
resource "azurerm_app_service_custom_hostname_binding" "tgit_api" {
  hostname            = "api.tgit.app"
  app_service_name    = azurerm_linux_web_app.tgit_api.name
  resource_group_name = azurerm_resource_group.tgit.name
}

# Managed SSL certificate
resource "azurerm_app_service_managed_certificate" "tgit_api" {
  custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.tgit_api.id
}

# Bind the certificate to the custom domain
resource "azurerm_app_service_certificate_binding" "tgit_api" {
  hostname_binding_id = azurerm_app_service_custom_hostname_binding.tgit_api.id
  certificate_id      = azurerm_app_service_managed_certificate.tgit_api.id
  ssl_state           = "SniEnabled"
}

# Create an App Service Plan
resource "azurerm_service_plan" "tgit" {
  name                = "TGit-service-plan"
  location            = azurerm_resource_group.tgit.location
  resource_group_name = azurerm_resource_group.tgit.name
  os_type             = "Linux"
  sku_name            = "B1"
}

# Create an App Service
resource "azurerm_linux_web_app" "tgit" {
  name                = "TGit-Dashboard"
  location            = azurerm_resource_group.tgit.location
  resource_group_name = azurerm_resource_group.tgit.name
  service_plan_id     = azurerm_service_plan.tgit.id

  site_config {
    application_stack {
      node_version = "20-lts"
    }
  }
}

# TODO: Uncomment after App Services are created
# # Custom domain
# resource "azurerm_app_service_custom_hostname_binding" "tgit_dashboard" {
#   hostname            = "tgit.app"
#   app_service_name    = azurerm_linux_web_app.tgit.name
#   resource_group_name = azurerm_resource_group.tgit.name
# }
#
# # Managed SSL certificate
# resource "azurerm_app_service_managed_certificate" "tgit_dashboard" {
#   custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.tgit_dashboard.id
# }
#
# # Bind the certificate to the custom domain
# resource "azurerm_app_service_certificate_binding" "tgit_dashboard" {
#   hostname_binding_id = azurerm_app_service_custom_hostname_binding.tgit_dashboard.id
#   certificate_id      = azurerm_app_service_managed_certificate.tgit_dashboard.id
#   ssl_state           = "SniEnabled"
# }

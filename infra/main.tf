# Create the TGit resource group
resource "azurerm_resource_group" "tgit" {
  location = "newzealandnorth"
  name     = "TGit"
}

# Grant the GitHub deploy SP contributor access to the resource group
data "azuread_service_principal" "deploy" {
  display_name = "VisualGit-TerraformUser"
}

resource "azurerm_role_assignment" "deploy_contributor" {
  scope                = azurerm_resource_group.tgit.id
  role_definition_name = "Contributor"
  principal_id         = data.azuread_service_principal.deploy.object_id
}
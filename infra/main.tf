# Create the TGit resource group
resource "azurerm_resource_group" "tgit" {
  location = "newzealandnorth"
  name     = "TGit_terraform"
}
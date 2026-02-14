# Create the TGit resource group
resource "azurerm_resource_group" "tgit" {
  location = "newzealandnorth"
  name     = "TGit"
}

# Grant the GitHub deploy SP contributor access to the resource group
data "azuread_service_principal" "deploy" {
  display_name = "VisualGit-TerraformUser"
}

data "azuread_application" "deploy" {
  client_id = data.azuread_service_principal.deploy.client_id
}

data "azurerm_subscription" "current" {}

resource "azurerm_role_assignment" "deploy_contributor" {
  scope                = azurerm_resource_group.tgit.id
  role_definition_name = "Contributor"
  principal_id         = data.azuread_service_principal.deploy.object_id
}

# SP needs Reader at subscription level to enumerate subscriptions during OIDC login
resource "azurerm_role_assignment" "deploy_subscription_reader" {
  scope                = data.azurerm_subscription.current.id
  role_definition_name = "Reader"
  principal_id         = data.azuread_service_principal.deploy.object_id
}

# OIDC federated credential for GitHub Actions (main branch)
resource "azuread_application_federated_identity_credential" "github_main" {
  application_id = data.azuread_application.deploy.id
  display_name   = "github-main-branch"
  audiences      = ["api://AzureADTokenExchange"]
  issuer         = "https://token.actions.githubusercontent.com"
  subject        = "repo:MarkJamesHoward/TGit:ref:refs/heads/main"
}

# Sync OIDC secrets to GitHub repo
resource "github_actions_secret" "azure_client_id" {
  repository      = "TGit"
  secret_name     = "AZURE_CLIENT_ID"
  plaintext_value = data.azuread_service_principal.deploy.client_id
}

resource "github_actions_secret" "azure_tenant_id" {
  repository      = "TGit"
  secret_name     = "AZURE_TENANT_ID"
  plaintext_value = data.azurerm_subscription.current.tenant_id
}

resource "github_actions_secret" "azure_subscription_id" {
  repository      = "TGit"
  secret_name     = "AZURE_SUBSCRIPTION_ID"
  plaintext_value = data.azurerm_subscription.current.subscription_id
}
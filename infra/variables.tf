variable "github_token" {
  description = "GitHub PAT for managing repository secrets"
  type        = string
  sensitive   = true
}

variable "webauthn_rp_name" {
  description = "WebAuthn Relying Party display name"
  type        = string
}

variable "webauthn_rp_id" {
  description = "WebAuthn Relying Party ID (domain)"
  type        = string
}

variable "webauthn_origin" {
  description = "WebAuthn expected origin URL"
  type        = string
}

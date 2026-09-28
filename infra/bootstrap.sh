#!/usr/bin/env bash
# One-time setup for the deploy workflow (D-44). Run it yourself, once, from a machine where you are signed in with:
#   az login          as an Owner of the target subscription
#   gh auth login     as an admin of the GitHub repository
#
# It creates:
#   - the resource group
#   - an Entra app registration + service principal for GitHub Actions, with a federated credential that trusts only
#     workflow runs on the main branch of this repository (OIDC: no client secret exists)
#   - the service principal's roles on the resource group: Contributor, and Role Based Access Control Administrator
#     (main.bicep assigns the API identity's roles and the service principal's own Key Vault role)
#   - the repository variables the workflow reads (ids only; none of them is a secret)
#
# Idempotent: re-running it finds and reuses what exists.
#
# Usage: infra/bootstrap.sh [--location westeurope] [--resource-group rg-claims-demo] [--repo owner/name] [--no-sql-reader]

set -euo pipefail

LOCATION="westeurope"
RESOURCE_GROUP="rg-claims-demo"
REPO="$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>/dev/null || true)"
APP_NAME="github-claims-deploy"
SQL_READER=true

while [[ $# -gt 0 ]]; do
  case "$1" in
    --location) LOCATION="$2"; shift 2 ;;
    --resource-group) RESOURCE_GROUP="$2"; shift 2 ;;
    --repo) REPO="$2"; shift 2 ;;
    --no-sql-reader) SQL_READER=false; shift ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
done

[[ -n "$REPO" ]] || { echo "Cannot tell the repository; pass --repo owner/name." >&2; exit 2; }

SUBSCRIPTION_ID="$(az account show --query id --output tsv)"
TENANT_ID="$(az account show --query tenantId --output tsv)"
echo "Subscription $(az account show --query name --output tsv) ($SUBSCRIPTION_ID), tenant $TENANT_ID"
echo "Repository $REPO, resource group $RESOURCE_GROUP in $LOCATION"
read -r -p "Continue? [y/N] " answer
[[ "$answer" == [yY] ]] || exit 1

echo "▶ Resource providers (a new subscription has most of them unregistered)"
for namespace in Microsoft.App Microsoft.OperationalInsights Microsoft.ManagedIdentity Microsoft.KeyVault \
                 Microsoft.Storage Microsoft.Sql Microsoft.Web; do
  az provider register --namespace "$namespace" --wait --output none
  echo "  $namespace registered"
done

echo "▶ Resource group"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
RG_ID="$(az group show --name "$RESOURCE_GROUP" --query id --output tsv)"

echo "▶ App registration and service principal"
CLIENT_ID="$(az ad app list --display-name "$APP_NAME" --query '[0].appId' --output tsv)"
if [[ -z "$CLIENT_ID" ]]; then
  CLIENT_ID="$(az ad app create --display-name "$APP_NAME" --query appId --output tsv)"
fi
SP_OBJECT_ID="$(az ad sp list --filter "appId eq '$CLIENT_ID'" --query '[0].id' --output tsv)"
if [[ -z "$SP_OBJECT_ID" ]]; then
  SP_OBJECT_ID="$(az ad sp create --id "$CLIENT_ID" --query id --output tsv)"
fi
echo "  client id $CLIENT_ID, service principal object id $SP_OBJECT_ID"

# The subject must match the token's "sub" claim exactly. Newer repositories use immutable subjects that carry the owner
# and repository ids (repo:owner@123/name@456:…), older ones the names only; GitHub reports the prefix it uses.
OIDC_SETTINGS="$(gh api "repos/$REPO/actions/oidc/customization/sub")"
if [[ "$(jq -r '.use_default' <<<"$OIDC_SETTINGS")" != true ]]; then
  echo "The repository has a custom OIDC subject template; set the federated credential's subject by hand." >&2
  exit 1
fi
SUBJECT="$(jq -r --arg repo "repo:$REPO" '.sub_claim_prefix // $repo' <<<"$OIDC_SETTINGS"):ref:refs/heads/main"

echo "▶ Federated credential: $SUBJECT"
CREDENTIAL="$(cat <<JSON
{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "${SUBJECT}",
  "audiences": ["api://AzureADTokenExchange"],
  "description": "GitHub Actions deploy workflow, main branch only"
}
JSON
)"
EXISTING_SUBJECT="$(az ad app federated-credential list --id "$CLIENT_ID" --query "[?name=='github-main'].subject | [0]" --output tsv)"
if [[ -z "$EXISTING_SUBJECT" ]]; then
  az ad app federated-credential create --id "$CLIENT_ID" --parameters "$CREDENTIAL" --output none
elif [[ "$EXISTING_SUBJECT" != "$SUBJECT" ]]; then
  echo "  replacing subject $EXISTING_SUBJECT"
  az ad app federated-credential update --id "$CLIENT_ID" --federated-credential-id github-main --parameters "$CREDENTIAL" --output none
fi

echo "▶ Roles on $RESOURCE_GROUP (a new service principal can take a minute to be visible to RBAC)"
for role in "Contributor" "Role Based Access Control Administrator"; do
  for attempt in 1 2 3 4 5 6; do
    if az role assignment create --assignee-object-id "$SP_OBJECT_ID" --assignee-principal-type ServicePrincipal \
         --role "$role" --scope "$RG_ID" --output none 2>/dev/null; then
      echo "  $role"
      break
    fi
    [[ $attempt == 6 ]] && { echo "Could not assign $role" >&2; exit 1; }
    sleep 10
  done
done

echo "▶ Repository variables on $REPO"
gh variable set AZURE_CLIENT_ID --repo "$REPO" --body "$CLIENT_ID"
gh variable set AZURE_TENANT_ID --repo "$REPO" --body "$TENANT_ID"
gh variable set AZURE_SUBSCRIPTION_ID --repo "$REPO" --body "$SUBSCRIPTION_ID"
gh variable set AZURE_RESOURCE_GROUP --repo "$REPO" --body "$RESOURCE_GROUP"
gh variable set AZURE_SP_OBJECT_ID --repo "$REPO" --body "$SP_OBJECT_ID"

# A read-only database user for you, so the audit log and Hangfire tables can be shown in the portal's query editor.
if [[ "$SQL_READER" == true ]]; then
  READER_NAME="$(az ad signed-in-user show --query userPrincipalName --output tsv 2>/dev/null || true)"
  READER_OBJECT_ID="$(az ad signed-in-user show --query id --output tsv 2>/dev/null || true)"
  if [[ -n "$READER_OBJECT_ID" ]]; then
    gh variable set SQL_READER_NAME --repo "$REPO" --body "$READER_NAME"
    gh variable set SQL_READER_OBJECT_ID --repo "$REPO" --body "$READER_OBJECT_ID"
    echo "  SQL reader: $READER_NAME"
  fi
fi

cat <<DONE

Done. Next:
  1. Push main, then run the workflow:  gh workflow run deploy.yml --repo $REPO --ref main
  2. The first run stops at "Check the image is publicly pullable": make the claims-api package public
     (github.com → your profile → Packages → claims-api → Package settings → Change visibility → Public),
     then re-run the failed jobs.
DONE

#!/usr/bin/env bash
# Resumes, pauses or reports the optional F2 Fabric capacity that infra/modules/fabric.bicep creates (WP-12). A
# paused capacity costs nothing for compute; pause it whenever nobody is demonstrating. dev does not use it: dev runs
# on the trial capacity in docs/ENVIRONMENT.md.
#
# Usage:
#   infra/scripts/fabric-capacity.sh resume|pause|status [environment]
#
# The environment (default dev) gives the names main.bicep uses: resource group rg-pitchside-<env>, capacity
# fcpitchside<env>. It signs in with the Azure CLI's current account and needs Contributor on the capacity.
set -euo pipefail

ACTION="${1:-}"
ENVIRONMENT="${2:-dev}"
RESOURCE_GROUP="rg-pitchside-$ENVIRONMENT"
CAPACITY="fcpitchside$ENVIRONMENT"
API_VERSION="2023-11-01"

fail() { printf 'error: %s\n' "$*" >&2; exit 1; }

case "$ACTION" in
  resume | pause | status) ;;
  *) echo "usage: $0 resume|pause|status [dev|demo]" >&2; exit 2 ;;
esac
[[ "$ENVIRONMENT" =~ ^(dev|demo)$ ]] || fail "the environment is dev or demo, got '$ENVIRONMENT'"
command -v az >/dev/null || fail "az is required"

subscription="$(az account show --query id -o tsv)" || fail "run az login first"
id="/subscriptions/$subscription/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Fabric/capacities/$CAPACITY"

state() { az rest --method get --url "https://management.azure.com$id?api-version=$API_VERSION" --query properties.state -o tsv; }

current="$(state)" || fail "capacity $CAPACITY not found in $RESOURCE_GROUP"
echo "==> $CAPACITY in $RESOURCE_GROUP: $current"

case "$ACTION" in
  status) exit 0 ;;
  # The capacity API calls pausing "suspend".
  resume) target="Active" operation="resume" ;;
  pause) target="Paused" operation="suspend" ;;
esac

# A suspended capacity reports Paused or Suspended depending on the API version; both mean it is not billing.
reached() { [[ "$1" == "$target" || ( "$target" == "Paused" && "$1" == "Suspended" ) ]]; }

if reached "$current"; then
  echo "    already $target"
  exit 0
fi

echo "==> $ACTION"
az rest --method post --url "https://management.azure.com$id/$operation?api-version=$API_VERSION" >/dev/null
for _ in $(seq 1 60); do
  current="$(state)"
  reached "$current" && { echo "    $current"; exit 0; }
  echo "    $current"
  sleep 10
done
fail "the capacity did not reach $target in 10 minutes"

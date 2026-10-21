#!/usr/bin/env bash
# Applies the committed Fabric artefacts (WP-12) to a Fabric workspace through the Fabric REST API:
#   1. the Eventhouse (default name "pitchside"),
#   2. the KQL database "pitchside", whose definition is every file under fabric/kql/schema, fabric/kql/queries and
#      fabric/activator joined in name order (tables, mapping, update policy, functions),
#   3. the KQL queryset "match-analysis" (fabric/queryset/match-analysis.json),
#   4. the Real-Time Dashboard "Pitchside Live" (fabric/dashboard/pitchside-live.json).
# Each item is created when missing and has its definition replaced when present, so running it again is safe.
# The Eventstream and the Activator rule are portal steps (docs/FABRIC.md); --export-activator saves the rule's
# definition once it exists.
#
# Usage:
#   infra/scripts/fabric-apply.sh <workspace-id>                     apply everything
#   infra/scripts/fabric-apply.sh <workspace-id> --export-activator  save the Activator definition to fabric/activator
#   infra/scripts/fabric-apply.sh <workspace-id> --dry-run           build the payloads only; no sign-in, no calls
#
# It signs in with the Azure CLI's current account (az login): the token comes from
# `az account get-access-token --resource https://api.fabric.microsoft.com`, is kept only in a temporary header file
# readable by the current user, and never appears in an argument, an output or a file in the repository.
#
# Optional environment: PITCHSIDE_EVENTHOUSE (default pitchside), PITCHSIDE_KQL_DATABASE (default pitchside).
set -euo pipefail
# Without this, `set -e` is off inside $(...), so a failed call there would carry on with an empty value.
shopt -s inherit_errexit

# PITCHSIDE_FABRIC_API points the script at a stand-in API for testing; leave it unset.
API="${PITCHSIDE_FABRIC_API:-https://api.fabric.microsoft.com/v1}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
EVENTHOUSE_NAME="${PITCHSIDE_EVENTHOUSE:-pitchside}"
DATABASE_NAME="${PITCHSIDE_KQL_DATABASE:-pitchside}"
QUERYSET_NAME="match-analysis"
DASHBOARD_NAME="Pitchside Live"
ACTIVATOR_NAME="pitchside-alerts"
SCHEMA_FOLDERS=(fabric/kql/schema fabric/kql/queries fabric/activator)

step() { printf '\n==> %s\n' "$*"; }
# Progress goes to stderr, so a function's printed result (an item id, a response body) stays on stdout alone.
info() { printf '    %s\n' "$*" >&2; }
fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

usage() {
  echo "usage: $0 <workspace-id> [--dry-run | --export-activator]" >&2
  exit 2
}

[[ $# -ge 1 ]] || usage
WORKSPACE_ID="$1"
MODE="${2:-apply}"
[[ "$WORKSPACE_ID" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]] \
  || fail "the workspace id must be a GUID, got '$WORKSPACE_ID'"
case "$MODE" in
  apply | --dry-run | --export-activator) ;;
  *) usage ;;
esac

for tool in jq base64; do
  command -v "$tool" >/dev/null || fail "$tool is required"
done
if [[ "$MODE" != "--dry-run" ]]; then
  for tool in az curl; do
    command -v "$tool" >/dev/null || fail "$tool is required"
  done
fi

umask 077
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
AUTH="$WORK/auth.header"

b64() { base64 <"$1" | tr -d '\n'; }

# A definition body: {"definition": {"parts": [...]}} from path=file pairs.
definition() {
  local parts='[]' path file
  while [[ $# -gt 0 ]]; do
    path="$1" file="$2"
    shift 2
    parts="$(jq --arg path "$path" --arg payload "$(b64 "$file")" \
      '. + [{path: $path, payload: $payload, payloadType: "InlineBase64"}]' <<<"$parts")"
  done
  jq -n --argjson parts "$parts" '{definition: {parts: $parts}}'
}

# Calls the Fabric API. Prints the response body, or for a long-running operation (202) waits for it and prints its
# result. Any other status stops the script with the error the API returned.
fabric() {
  local method="$1" path="$2" body="${3:-}"
  local out="$WORK/response.json" headers="$WORK/response.headers" code
  local args=(-sS -X "$method" -H @"$AUTH" -H "Content-Type: application/json" -D "$headers" -o "$out" -w '%{http_code}')
  [[ -n "$body" ]] && args+=(--data-binary @"$body")
  code="$(curl "${args[@]}" "$API$path")" || fail "$method $path did not complete"
  case "$code" in
    200 | 201) cat "$out" ;;
    202) wait_for_operation "$headers" ;;
    *) fail "$method $path answered $code: $(cat "$out")" ;;
  esac
}

wait_for_operation() {
  local headers="$1" id retry status state="$WORK/operation.json" result="$WORK/result.json" code
  id="$(tr -d '\r' <"$headers" | awk 'tolower($1) == "x-ms-operation-id:" { print $2 }')"
  retry="$(tr -d '\r' <"$headers" | awk 'tolower($1) == "retry-after:" { print $2 }')"
  [[ -n "$id" ]] || fail "a 202 came without an operation id"
  for _ in $(seq 1 120); do
    sleep "${retry:-5}"
    code="$(curl -sS -H @"$AUTH" -o "$state" -w '%{http_code}' "$API/operations/$id")" || fail "polling operation $id did not complete"
    [[ "$code" == 200 ]] || fail "operation $id answered $code: $(cat "$state")"
    status="$(jq -r '.status' "$state")"
    case "$status" in
      Succeeded)
        code="$(curl -sS -H @"$AUTH" -o "$result" -w '%{http_code}' "$API/operations/$id/result")" || fail "reading the result of $id did not complete"
        if [[ "$code" == 200 ]]; then cat "$result"; else echo '{}'; fi
        return 0
        ;;
      Failed) fail "operation $id failed: $(jq -c '.error' "$state")" ;;
      *) info "operation $id: $status" ;;
    esac
  done
  fail "operation $id did not finish in time"
}

# The id of the item of a kind (eventhouses, kqlDatabases, ...) with this display name, or nothing.
find_item() {
  fabric GET "/workspaces/$WORKSPACE_ID/$1" | jq -r --arg name "$2" '.value[] | select(.displayName == $name) | .id' | head -n 1
}

# Creates the item with the definition, or replaces the definition of the existing one. Prints the item id.
upsert_item() {
  local kind="$1" name="$2" body="$3" id request="$WORK/request.json"
  id="$(find_item "$kind" "$name")" || fail "listing $kind failed"
  if [[ -z "$id" ]]; then
    jq --arg name "$name" '. + {displayName: $name}' "$body" >"$request"
    id="$(fabric POST "/workspaces/$WORKSPACE_ID/$kind" "$request" | jq -r '.id')" || fail "creating $kind '$name' failed"
    [[ -n "$id" && "$id" != null ]] || fail "creating $kind '$name' returned no id"
    info "created $kind '$name' ($id)"
  else
    fabric POST "/workspaces/$WORKSPACE_ID/$kind/$id/updateDefinition" "$body" >/dev/null
    info "updated the definition of $kind '$name' ($id)"
  fi
  echo "$id"
}

build_schema() {
  local schema="$WORK/DatabaseSchema.kql" folder file
  : >"$schema"
  for folder in "${SCHEMA_FOLDERS[@]}"; do
    while IFS= read -r file; do
      { cat "$file"; printf '\n'; } >>"$schema"
      info "schema: ${file#"$ROOT"/}"
    done < <(find "$ROOT/$folder" -maxdepth 1 -name '*.kql' | LC_ALL=C sort)
  done
  echo "$schema"
}

fill() {
  # Replaces {{NAME}} placeholders from NAME=value pairs.
  local file="$1" out="$2"
  shift 2
  local filter='.' pair
  local args=()
  for pair in "$@"; do
    args+=(--arg "${pair%%=*}" "${pair#*=}")
    filter="$filter | walk(if type == \"string\" then gsub(\"\\\\{\\\\{${pair%%=*}\\\\}\\\\}\"; \$${pair%%=*}) else . end)"
  done
  jq "${args[@]}" "$filter" "$file" >"$out"
  if grep -q '{{[A-Z_]*}}' "$out"; then
    fail "unfilled placeholder in $out: $(grep -o '{{[A-Z_]*}}' "$out" | sort -u | tr '\n' ' ')"
  fi
}

if [[ "$MODE" == "--dry-run" ]]; then
  step "Dry run: building the payloads in $WORK"
  schema="$(build_schema)"
  jq -n --arg eventhouse "<eventhouse-id>" '{databaseType: "ReadWrite", parentEventhouseItemId: $eventhouse}' >"$WORK/DatabaseProperties.json"
  definition DatabaseProperties.json "$WORK/DatabaseProperties.json" DatabaseSchema.kql "$schema" >"$WORK/kql-database.json"
  fill "$ROOT/fabric/queryset/match-analysis.json" "$WORK/RealTimeQueryset.json" \
    "QUERY_SERVICE_URI=https://example.kusto.fabric.microsoft.com" "DATABASE_NAME=$DATABASE_NAME"
  definition RealTimeQueryset.json "$WORK/RealTimeQueryset.json" >"$WORK/kql-queryset.json"
  fill "$ROOT/fabric/dashboard/pitchside-live.json" "$WORK/RealTimeDashboard.json" \
    "QUERY_SERVICE_URI=https://example.kusto.fabric.microsoft.com" "DATABASE_ID=00000000-0000-0000-0000-000000000000" \
    "WORKSPACE_ID=$WORKSPACE_ID"
  definition RealTimeDashboard.json "$WORK/RealTimeDashboard.json" >"$WORK/kql-dashboard.json"
  info "DatabaseSchema.kql: $(grep -c '^\.' "$schema") commands, $(grep -c '^\.create-or-alter function' "$schema") functions"
  for payload in kql-database kql-queryset kql-dashboard; do
    jq -e '.definition.parts | length > 0' "$WORK/$payload.json" >/dev/null || fail "$payload has no parts"
    info "$payload.json: $(jq -r '[.definition.parts[].path] | join(", ")' "$WORK/$payload.json"), $(wc -c <"$WORK/$payload.json") bytes"
  done
  step "Dry run complete; nothing was sent"
  exit 0
fi

step "Signing in to the Fabric API with the Azure CLI account"
token="$(az account get-access-token --resource https://api.fabric.microsoft.com --query accessToken -o tsv)" \
  || fail "az account get-access-token failed; run az login first"
printf 'Authorization: Bearer %s\n' "$token" >"$AUTH"
unset token
info "signed in as $(az account show --query user.name -o tsv)"

step "Workspace $WORKSPACE_ID"
workspace="$(fabric GET "/workspaces/$WORKSPACE_ID")"
info "name: $(jq -r '.displayName' <<<"$workspace")"
capacity="$(jq -r '.capacityId // empty' <<<"$workspace")"
[[ -n "$capacity" ]] || fail "the workspace is not on a capacity; assign it to the trial or F2 capacity first (docs/FABRIC.md)"
info "capacity: $capacity"

if [[ "$MODE" == "--export-activator" ]]; then
  step "Exporting the Activator '$ACTIVATOR_NAME'"
  id="$(find_item reflexes "$ACTIVATOR_NAME")" || fail "listing reflexes failed"
  [[ -n "$id" ]] || fail "no Activator named '$ACTIVATOR_NAME' in the workspace; create it first (fabric/activator/README.md)"
  fabric POST "/workspaces/$WORKSPACE_ID/reflexes/$id/getDefinition" >"$WORK/reflex.json"
  jq -r '.definition.parts[] | select(.path == "ReflexEntities.json") | .payload' "$WORK/reflex.json" | base64 -d \
    | jq . >"$ROOT/fabric/activator/ReflexEntities.json"
  info "wrote fabric/activator/ReflexEntities.json ($(jq length "$ROOT/fabric/activator/ReflexEntities.json") entities); review it before committing"
  exit 0
fi

step "Eventhouse '$EVENTHOUSE_NAME'"
eventhouse="$(find_item eventhouses "$EVENTHOUSE_NAME")" || fail "listing eventhouses failed"
if [[ -z "$eventhouse" ]]; then
  jq -n --arg name "$EVENTHOUSE_NAME" '{displayName: $name, description: "Pitchside live match events (WP-12)"}' >"$WORK/eventhouse.json"
  eventhouse="$(fabric POST "/workspaces/$WORKSPACE_ID/eventhouses" "$WORK/eventhouse.json" | jq -r '.id')"
  [[ -n "$eventhouse" && "$eventhouse" != null ]] || fail "creating the Eventhouse returned no id"
  info "created ($eventhouse)"
else
  info "exists ($eventhouse)"
fi
query_uri="$(fabric GET "/workspaces/$WORKSPACE_ID/eventhouses/$eventhouse" | jq -r '.properties.queryServiceUri // empty')"
[[ -n "$query_uri" ]] || fail "the Eventhouse has no query URI yet; run the script again in a minute"
info "query URI: $query_uri"

step "KQL database '$DATABASE_NAME': tables, mapping, update policy and functions"
schema="$(build_schema)"
jq -n --arg eventhouse "$eventhouse" '{databaseType: "ReadWrite", parentEventhouseItemId: $eventhouse}' >"$WORK/DatabaseProperties.json"
definition DatabaseProperties.json "$WORK/DatabaseProperties.json" DatabaseSchema.kql "$schema" >"$WORK/kql-database.json"
database="$(upsert_item kqlDatabases "$DATABASE_NAME" "$WORK/kql-database.json")" || fail "applying the KQL database failed"

step "Checking the database definition reads back"
fabric POST "/workspaces/$WORKSPACE_ID/kqlDatabases/$database/getDefinition" >"$WORK/database-definition.json"
jq -r '.definition.parts[] | select(.path == "DatabaseSchema.kql") | .payload' "$WORK/database-definition.json" | base64 -d >"$WORK/applied.kql"
for table in events_raw events; do
  grep -Eq "table ${table}[ (]" "$WORK/applied.kql" || fail "table $table is not in the applied schema"
done
grep -q 'ExpandEnvelope' "$WORK/applied.kql" || fail "the update policy function is not in the applied schema"
info "tables events_raw and events, $(grep -c 'create-or-alter function' "$WORK/applied.kql") functions"

step "KQL queryset '$QUERYSET_NAME'"
fill "$ROOT/fabric/queryset/match-analysis.json" "$WORK/RealTimeQueryset.json" "QUERY_SERVICE_URI=$query_uri" "DATABASE_NAME=$DATABASE_NAME"
definition RealTimeQueryset.json "$WORK/RealTimeQueryset.json" >"$WORK/kql-queryset.json"
upsert_item kqlQuerysets "$QUERYSET_NAME" "$WORK/kql-queryset.json" >/dev/null

step "Real-Time Dashboard '$DASHBOARD_NAME'"
fill "$ROOT/fabric/dashboard/pitchside-live.json" "$WORK/RealTimeDashboard.json" \
  "QUERY_SERVICE_URI=$query_uri" "DATABASE_ID=$database" "WORKSPACE_ID=$WORKSPACE_ID"
definition RealTimeDashboard.json "$WORK/RealTimeDashboard.json" >"$WORK/kql-dashboard.json"
upsert_item kqlDashboards "$DASHBOARD_NAME" "$WORK/kql-dashboard.json" >/dev/null

step "Done"
info "Eventhouse $eventhouse, KQL database $database"
info "Next: the Eventstream from Event Hubs into table events_raw (docs/FABRIC.md, portal steps), then a replay."

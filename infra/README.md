# Infrastructure

Everything Pitchside runs in Azure, as Bicep, deployed with `azd`. The specification is
[docs/specs/WP-09-infrastructure.md](../docs/specs/WP-09-infrastructure.md); this file is the operator's guide.

Every resource is identity-based. No key, connection string or password is a parameter, an output, an app setting
or a file in this repository: Cosmos DB and Event Hubs have local auth disabled, the checkpoint storage account
refuses shared keys, the registry admin user is off, Key Vault is in RBAC mode and Speech has local auth disabled.
The apps authenticate with `DefaultAzureCredential` and their user-assigned identity (`AZURE_CLIENT_ID`).

## Layout

| File | What it is for |
| --- | --- |
| `main.bicep` | Subscription-scope entry point: creates `rg-pitchside-<env>` and composes the modules |
| `main.dev.bicepparam` | `dev`: Speech F0, web scales to zero |
| `main.demo.bicepparam` | `demo`: Speech S0, one warm web replica |
| `modules/identity.bicep` | One user-assigned managed identity per app: `id-pitchside-{api,agents,web}-<env>` |
| `modules/monitoring.bicep` | Log Analytics workspace, workspace-based Application Insights, the alert action group (only when addresses are given) |
| `modules/environment.bicep` | Container Apps environment (Consumption profile); logs to Log Analytics by diagnostic setting, OpenTelemetry traces and logs to Application Insights through the managed agent |
| `modules/registry.bicep` | Container Registry Basic, admin user off; AcrPull for the three identities |
| `modules/cosmos.bicep` | Cosmos DB serverless, continuous backup 7 days, database `pitchside` and its seven containers, data-plane Data Contributor for the API and agents identities |
| `modules/eventhubs.bicep` | Event Hubs namespace, hub `match-events` (4 partitions), checkpoint storage account and `checkpoints` container; Data Sender, Data Receiver and Storage Blob Data Contributor |
| `modules/keyvault.bicep` | Key Vault, RBAC mode; Secrets User for the API identity. Holds only the optional MCP key |
| `modules/speech.bicep` | Azure AI Speech with a custom subdomain (needed for Entra ID); Speech User for the API and agents identities |
| `modules/ai.bicep` | Foundry: role assignment only (Cognitive Services OpenAI User) on an account created outside this template |
| `modules/fetch-image.bicep` | Reads an app's current image so a later `azd provision` keeps it |
| `modules/app-api.bicep` | `pitchside-api`: external ingress, sticky sessions, exactly one replica, probes on `/alive` and `/health` |
| `modules/app-agents.bicep` | `pitchside-agents`: no ingress (a worker), 0 to 2 replicas on the KEDA `azure-eventhub` scaler with its identity |
| `modules/app-web.bicep` | `pitchside-web`: nginx with the Angular build, proxies `/api`, `/hubs`, `/mcp` to the API, external, 0 to 2 replicas |
| `modules/alerts.bicep` | API availability test on `/health` with its alert, and the narratives fallback-rate log alert |

Fabric (WP-12) is not part of this template.

### Cosmos DB containers

`matches`, `events`, `factsheets`, `moments`, `narratives`, `overlay`, `audit`, all partitioned on `/matchId`, the
names `CosmosMatchStore` uses. Entra ID data-plane roles cannot create containers, so the template does, and the API
only checks they exist.

### Role assignments

| Identity | Role | Scope |
| --- | --- | --- |
| api, agents, web | AcrPull | registry |
| api, agents | Cosmos DB Built-in Data Contributor (`00000000-0000-0000-0000-000000000002`, data plane) | Cosmos DB account |
| api, agents | Azure Event Hubs Data Sender, Azure Event Hubs Data Receiver | Event Hubs namespace |
| api, agents | Storage Blob Data Contributor | checkpoint storage account |
| api, agents | Cognitive Services Speech User | Speech account |
| api, agents | Cognitive Services OpenAI User | Foundry account (only when `FOUNDRY_RESOURCE_ID` is set) |
| api | Key Vault Secrets User | Key Vault |

The person or pipeline that provisions needs Owner, or Contributor plus User Access Administrator, on the
subscription, because the template writes role assignments.

### App settings

The keys are documented in [docs/CONFIGURATION.md](../docs/CONFIGURATION.md).

| App | Settings |
| --- | --- |
| api | `AZURE_CLIENT_ID`, `OTEL_SERVICE_NAME`, `Storage__Provider=Cosmos`, `Storage__Cosmos__AccountEndpoint`, `Storage__Cosmos__Database`, `Bus__Provider=EventHubs`, `Bus__EventHubs__Namespace`, `Bus__EventHubs__HubName`, `Bus__EventHubs__CheckpointBlobUri`, `Pitchside__RunAgentsInApi=true`, `UsageGuard__Enabled=true`, `Agents__Provider` (`Foundry` when an endpoint is given, else `Scripted`); with an endpoint also `Foundry__Endpoint`, `Foundry__Deployments__Fast`, `Foundry__Deployments__Quality`, `Evaluation__Provider=Foundry`; `Mcp__ApiKey` from Key Vault when enabled |
| agents | `AZURE_CLIENT_ID`, `OTEL_SERVICE_NAME`, the `Bus__*` settings, `UsageGuard__Enabled=true`, `Agents__Provider`, and the `Foundry__*` settings when an endpoint is given |
| web | `API_HOST`, the API's FQDN, which nginx proxies to over HTTPS |

The environment's OpenTelemetry agent sets `OTEL_EXPORTER_OTLP_ENDPOINT` on every app, which is what turns on the
OTLP exporter in `Pitchside.ServiceDefaults`.

## Parameters

Per-environment values are in the `.bicepparam` files. Values that are per person or per subscription are read from
the environment, so `azd env set NAME value` (or `export NAME=value` for a hand deployment) sets them:

| Variable | Parameter | Default |
| --- | --- | --- |
| `AZURE_LOCATION` | `location` | `southafricanorth` |
| `PITCHSIDE_OWNER` | `owner` tag | `maribodevs` |
| `FOUNDRY_ENDPOINT` | `foundryEndpoint` | `https://ais-pitchside-dev.openai.azure.com/`, the Foundry resource in docs/ENVIRONMENT.md, so the apps call Foundry and the judge scores groundedness; set it empty for the scripted crew |
| `FOUNDRY_DEPLOYMENT_FAST` | `foundryFastDeployment` | `pitchside-fast` |
| `FOUNDRY_DEPLOYMENT_QUALITY` | `foundryQualityDeployment` | `pitchside-quality` |
| `FOUNDRY_RESOURCE_ID` | `foundryResourceId` | the id of `ais-pitchside-dev`, so the app identities get Cognitive Services OpenAI User on it; set it empty to grant no role |
| `PITCHSIDE_ALERT_EMAILS` | `alertEmails`, comma-separated | empty: no action group, alerts show in the portal only |
| `PITCHSIDE_MCP_KEY_FROM_VAULT` | `mcpApiKeyFromKeyVault` | `false` |
| `SERVICE_{API,AGENTS,WEB}_RESOURCE_EXISTS` | `{api,agents,web}Exists` | set by azd |

To run an environment with no Foundry calls and no role on the Foundry account, set both `FOUNDRY_ENDPOINT` and
`FOUNDRY_RESOURCE_ID` to empty values.

azd uses `infra/main.bicepparam`. The `preprovision` hook in `azure.yaml` copies `main.<azd env name>.bicepparam` to
it before every provision (the copy is git-ignored), so the azd environment must be named `dev` or `demo`.

## Deploy by hand

The provisioning order from the specification, with what can be checked at each step:

1. `az login`, `az account set --subscription <id>`; check with `az account show`.
2. Human step: confirm the Foundry resource and project exist in the chosen region with a fast and a quality chat
   deployment. Region preference is South Africa North, then West Europe, then Sweden Central, wherever the models
   have capacity (`az cognitiveservices model list --location <region>`). Record the region in the specification.
3. Create the environment and provision:

   ```bash
   azd auth login
   azd env new dev
   azd env set AZURE_LOCATION southafricanorth
   azd env set FOUNDRY_ENDPOINT https://<foundry-resource>.openai.azure.com/
   azd env set FOUNDRY_DEPLOYMENT_FAST <fast-deployment>
   azd env set FOUNDRY_DEPLOYMENT_QUALITY <quality-deployment>
   azd env set FOUNDRY_RESOURCE_ID /subscriptions/<id>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<name>
   azd env set PITCHSIDE_ALERT_EMAILS <address>,<address>
   azd provision
   az resource list -g rg-pitchside-dev -o table
   ```

4. Role assignments are part of the template. Check each identity with
   `az role assignment list --assignee <principal id> --all -o table` and the Cosmos DB data-plane ones with
   `az cosmosdb sql role assignment list -g rg-pitchside-dev -a <account>`.
5. `azd deploy`, then once more `azd provision`. The first provision runs a placeholder image without probes; the
   second one sees the deployed images and adds the `/alive` and `/health` probes. Check:

   ```bash
   curl -fsS "$(azd env get-value API_URL)/health"
   curl -fsS "$(azd env get-value WEB_URL)/"
   ```

   then start a replay from the web UI and confirm a verified narrative on
   `GET /api/v1/matches/<matchId>/narratives` within 60 s (the deploy workflow's smoke test does the same with curl).
6. Container Apps authentication stays off: judges need free access, and the usage guard limits the load.
7. On 24 October repeat 3 to 5 with `azd env new demo`. The demo parameter file keeps one web replica warm; the API
   always runs exactly one.
8. Budgets, per resource group, with an 80% email alert:

   ```bash
   az consumption budget create --budget-name pitchside-dev --resource-group rg-pitchside-dev \
     --amount <monthly amount> --category cost --time-grain monthly \
     --start-date <first of this month> --end-date 2026-11-30
   ```

   and add the 80% notification and its addresses in the portal (Cost Management, Budgets).

A deployment without azd is the same template. Once the apps have been deployed, export the three
`SERVICE_*_RESOURCE_EXISTS` flags first: `fetch-image.bicep` keeps an app's running image only when its flag is
`true`, and both parameter files default the flags to `false`, which would put the placeholder image back and take the
app offline. azd sets them itself after its first deploy.

```bash
export SERVICE_API_RESOURCE_EXISTS=true SERVICE_AGENTS_RESOURCE_EXISTS=true SERVICE_WEB_RESOURCE_EXISTS=true
export AZURE_LOCATION=southafricanorth
az deployment sub create --location "$AZURE_LOCATION" --name pitchside-dev \
  --template-file infra/main.bicep --parameters infra/main.dev.bicepparam
```

Validate offline with the Bicep CLI:

```bash
bicep build infra/main.bicep --stdout > /dev/null
bicep build-params infra/main.dev.bicepparam --stdout > /dev/null
bicep build-params infra/main.demo.bicepparam --stdout > /dev/null
```

## Continuous deployment

`.github/workflows/deploy.yml` deploys `dev` on every push to `main` and either environment on a manual run. It skips
itself until the chosen environment's client id exists. One-time setup:

1. A deployment identity. `id-pitchside-github` (docs/ENVIRONMENT.md) already exists: Contributor and User Access
   Administrator on both resource groups, federated credentials for `environment:dev` and `environment:demo`, and
   its client id in the repository variable `AZURE_CLIENT_ID`. It still needs **Reader on the subscription**:
   `main.bicep` deploys at subscription scope, and `azd env refresh` reads that deployment's outputs, which a role
   on a resource group alone does not allow.
   - Because that one identity can write to both environments, the `demo` approval stops the workflow but not the
     identity. To make it a real boundary, create one identity per environment, each with Contributor and AcrPush
     on its own resource group and registry, one federated credential for its own environment, and Reader on the
     subscription, and set `AZURE_CLIENT_ID_DEV` and `AZURE_CLIENT_ID_DEMO`; they take precedence over
     `AZURE_CLIENT_ID`.
2. Repository variables (not secrets; none of them is one): `AZURE_CLIENT_ID` (or the two per-environment ones),
   `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_LOCATION`. They are repository variables because a job's `if`
   cannot see variables scoped to an environment.
3. GitHub environments `dev` and `demo`; `demo` with Sipho as required reviewer.

The workflow builds the three images, tags them with the commit SHA, pushes them to the environment's registry,
runs `azd deploy <service> --from-package <image>` for each, and smoke-tests `/health`, the web root and a replay
through the web proxy reaching a verified narrative within 60 s. It deploys only: infrastructure changes go through
`azd provision` by hand, reviewed in a pull request first.

## Roll back

Redeploy the previous commit's images, which stay in the registry under their SHA:

```bash
azd deploy api --from-package <registry>/pitchside/api:<previous sha>
azd deploy agents --from-package <registry>/pitchside/agents:<previous sha>
azd deploy web --from-package <registry>/pitchside/web:<previous sha>
```

or run the deploy workflow manually on that commit. An infrastructure change is rolled back by reverting its commit
and running `azd provision`.

## Tear down

After judging, on 11 November:

```bash
azd down --environment demo --purge --force
azd down --environment dev --purge --force
```

`--purge` removes the soft-deleted Key Vault and Cognitive Services accounts, so their names are free again. Delete
the GitHub federated credentials and the budgets afterwards.

## Cost

Expected monthly cost per resource. Prices depend on the region and on use, so the figures are measured from Cost
Management after the first full week of each environment, not estimated here.

| Resource | SKU or plan | dev | demo |
| --- | --- | --- | --- |
| Container Apps environment and apps | Consumption; API 1 vCPU, 2 GiB, always one replica; agents 0.5 vCPU, 1 GiB, 0 to 2; web 0.25 vCPU, 0.5 GiB, 0 to 2 (demo keeps 1) | to measure | to measure |
| Container Registry | Basic | to measure | to measure |
| Cosmos DB | serverless, continuous backup 7 days | to measure | to measure |
| Event Hubs | Basic, 1 throughput unit | to measure | to measure |
| Storage account (checkpoints) | Standard LRS | to measure | to measure |
| Key Vault | Standard | to measure | to measure |
| Log Analytics and Application Insights | pay as you go, 30-day retention, 1 GB daily cap | to measure | to measure |
| Availability test and alerts | standard test every 5 min from 5 locations, one metric alert, one log alert | to measure | to measure |
| Azure AI Speech | F0 (dev), S0 (demo) | to measure | to measure |
| Foundry model deployments | outside this template | to measure | to measure |

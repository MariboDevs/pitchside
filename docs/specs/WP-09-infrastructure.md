# WP-09: Azure resources, deployment, operations

| | |
| --- | --- |
| Branch | `feat/wp09-infra` |
| Depends on | WP-03 for the first deploy; grows with every WP |
| Owner | Sipho, executed by Claude Code running locally with `az`, `azd` and `bicep` signed in to the subscription |
| Effort | 2 days for the first environment, then half a day per added service |
| Changes contracts | none |

## Goal

Everything Pitchside needs in Azure exists as reviewed code, is created by one command, runs the hosted demo
until 10 November on a known monthly cost, and leaves nothing a judge could break. Claude Code local performs
the provisioning; this document is its script. Where a step needs a human in a portal, it says so and stops.

## Environments

| Name | Resource group | Purpose |
| --- | --- | --- |
| `dev` | `rg-pitchside-dev` | the team's working environment from 12 October |
| `demo` | `rg-pitchside-demo` | the judged environment, frozen from 25 October, up to 10 November |

Region: the one where the chosen Foundry models are available with capacity; check with
`az cognitiveservices model list` for the region before choosing, and record the choice in this file.
Preference order: South Africa North, West Europe, Sweden Central.

## Resources (Bicep, `infra/`)

| Module | Resource | Notes |
| --- | --- | --- |
| `environment.bicep` | Container Apps environment, Log Analytics workspace | consumption plan |
| `identity.bicep` | one user-assigned managed identity per app | role assignments below |
| `registry.bicep` | Container Registry, Basic | pull via identity, admin user off |
| `app-api.bicep` | Container App `pitchside-api` | external ingress, 1 replica fixed (Akka single node), WebSockets on, health probes `/alive` and `/health` |
| `app-agents.bicep` | Container App `pitchside-agents` | internal, 0 to 2 replicas, scale on the moment queue depth via KEDA Event Hubs scaler when the bus is Event Hubs |
| `app-web.bicep` | Container App `pitchside-web` | nginx serving the Angular build and proxying `/api`, `/hubs`, `/mcp` to the API, external ingress, scale 0 to 2 |
| `cosmos.bicep` | Cosmos DB account, serverless, database `pitchside`, the seven containers with `/matchId` partition key | continuous backup 7 days; local auth disabled; RBAC data-plane roles to the identities |
| `eventhubs.bicep` | Event Hubs namespace Basic, hub `match-events` 4 partitions, consumer groups `api`, `agents`, `fabric` | plus a Storage account for checkpoints |
| `keyvault.bicep` | Key Vault, RBAC mode | holds only what cannot be identity-based (none expected; the MCP API key if set) |
| `monitoring.bicep` | Application Insights (workspace-based), two alerts: API unavailable 5 min, narratives fallback rate above 20% in 15 min | action group emails Sipho and Pretty |
| `ai.bicep` | **parameters only**: the Foundry project endpoint and deployment names are inputs, the project itself is created once by hand or by `az` outside this template because model deployments and quotas are account-level decisions | the identities get `Cognitive Services OpenAI User` on the resource |
| `speech.bicep` | Speech resource, F0 for dev, S0 for demo | identity role `Cognitive Services Speech User` |
| `fabric` | see WP-12; Fabric capacity is created in its own module `fabric.bicep` (F2) and paused when idle | |

`main.bicep` composes modules; `main.{dev,demo}.bicepparam` hold environment values. `azure.yaml` maps the
three services for `azd`. Tags on every resource: `project=pitchside`, `env`, `owner`.

## Provisioning script for Claude Code local

Run in order; stop and report at any failure. Each step names its verification.

1. `az login` and `az account set --subscription <id>`; verify with `az account show`.
2. **Human step.** Confirm the Foundry project exists in the chosen region with two chat deployments (a small
   fast model and a larger quality model). If not, create the Foundry resource and project in the Azure
   portal or with `az cognitiveservices account create` and deploy the models; record the endpoint and the two
   deployment names in `infra/main.dev.bicepparam` as parameters, never in code. Verify with one
   `curl` chat completion using an `az account get-access-token --resource https://cognitiveservices.azure.com`
   bearer token.
3. `azd auth login`, `azd env new dev`, `azd env set` for every parameter, `azd provision`. Verify every
   resource in `az resource list -g rg-pitchside-dev -o table`.
4. Role assignments: API and agents identities get Cosmos DB Built-in Data Contributor (data-plane RBAC via
   `az cosmosdb sql role assignment create`), Azure Event Hubs Data Sender and Receiver, Storage Blob Data
   Contributor on the checkpoint account, Cognitive Services OpenAI User, Cognitive Services Speech User.
   Verify each with `az role assignment list --assignee`.
5. `azd deploy`. Verify `/health` on the API and web URLs and a replay started through the web UI reaching a
   verified narrative within 60 s, checked with `curl` on the narratives endpoint.
6. Enable **Container Apps authentication** (Easy Auth) on the API and web apps only if the team decides the
   demo is gated; the rules require free access for judges, so the default is open with the usage guard.
7. Repeat 3 to 5 for `demo` on 24 October, then set the `demo` apps' min replicas so the API never cold-starts
   while judging runs (`minReplicas: 1`).
8. Cost: `az consumption budget create` on each resource group with an 80% email alert; record expected
   monthly cost per resource in `infra/README.md`.

## CI and CD

- `.github/workflows/deploy.yml`: on push to `main`, build images, `azd deploy` to `dev` with OIDC federated
  credentials (no secrets in GitHub), run the smoke test (replay to verified narrative). Manual dispatch with
  environment `demo` requires an approval from Sipho.
- Image tags are the commit SHA; the deployed SHA is shown in the control room footer.

## Operations

- `infra/README.md`: what each module is for, how to deploy by hand, how to roll back (redeploy the previous
  SHA), how to pause Fabric capacity, how to tear down after judging (`azd down --purge`, scheduled for
  11 November).
- Aspire dashboard is local only; in Azure, Application Insights holds the same OpenTelemetry traces, and the
  video can show either.

## Done when

```bash
az bicep build --file infra/main.bicep
azd provision --environment dev
azd deploy --environment dev
curl -fsS https://<api>/health && curl -fsS https://<web>/
```

`IMPLEMENTATION-GAP.md` records the URLs, the region chosen and the `az resource list` output.

## Notes for the implementer

- Never write a key or connection string anywhere, including a `.bicepparam`. Everything is identity-based.
  If a service only supports keys, stop and report.
- The Fabric capacity and the Foundry model deployments are the two places a portal click may be needed; say
  so in the report rather than working around it.

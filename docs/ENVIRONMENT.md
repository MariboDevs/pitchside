# Environment

What exists outside the repository after Phase 0 setup on 8 October 2026: accounts, tooling, GitHub settings
and the Azure foundation that WP-09 builds on. Ids, names, endpoints and URLs only. No key, secret, token or
connection string appears here or anywhere else in this repository; every service is reached with an
identity.

## Region: South Africa North

South Africa North is the first region in the preference order and every GPT chat model in the Foundry
catalogue is listed there as generally available with the GlobalStandard SKU. GlobalStandard routes
inference globally, so the region of the resource does not change model availability or latency. Quota,
not availability, is the constraint; see the Foundry section. Both resource groups, the Foundry resource,
the Speech resource and the deployment identity are in `southafricanorth`.

## Azure

| Item | Value |
| --- | --- |
| Account | sipho@maribodevs.com |
| Tenant | `6073b6f1-927f-4ff2-8f6a-9884f43e84da` (siphomaribodevs.onmicrosoft.com) |
| Subscription | Microsoft Azure Sponsorship, `1641c920-b05d-4921-8a24-029ca939893a`, bills in USD |
| Resource providers registered | Microsoft.App, Microsoft.ContainerRegistry, Microsoft.DocumentDB, Microsoft.EventHub, Microsoft.KeyVault, Microsoft.CognitiveServices, Microsoft.OperationalInsights, Microsoft.Insights, Microsoft.Fabric, Microsoft.Storage, Microsoft.ManagedIdentity (all `Registered`) |

The same Azure CLI profile also holds a second sponsorship subscription under sipho@smesnapshot.com in
another tenant. Pitchside uses only the subscription above; `az account show` must name it before any
provisioning command.

### Resource groups

| Name | Id | Tags |
| --- | --- | --- |
| `rg-pitchside-dev` | `/subscriptions/1641c920-b05d-4921-8a24-029ca939893a/resourceGroups/rg-pitchside-dev` | project=pitchside, env=dev, owner=sipho@maribodevs.com |
| `rg-pitchside-demo` | `/subscriptions/1641c920-b05d-4921-8a24-029ca939893a/resourceGroups/rg-pitchside-demo` | project=pitchside, env=demo, owner=sipho@maribodevs.com |

### Microsoft Foundry

| Item | Value |
| --- | --- |
| Resource | `ais-pitchside-dev`, kind AIServices, SKU S0, system-assigned identity, local auth (keys) disabled |
| Resource id | `/subscriptions/1641c920-b05d-4921-8a24-029ca939893a/resourceGroups/rg-pitchside-dev/providers/Microsoft.CognitiveServices/accounts/ais-pitchside-dev` |
| Resource identity principal | `3bac8280-84dd-4e3d-a0ef-c73f84749093` |
| Project | `pitchside-dev` |
| Project endpoint | `https://ais-pitchside-dev.services.ai.azure.com/api/projects/pitchside-dev` |
| OpenAI endpoint | `https://ais-pitchside-dev.openai.azure.com/` |
| Services endpoint | `https://ais-pitchside-dev.cognitiveservices.azure.com/` |
| Deployment `pitchside-fast` | gpt-6-luna, version 2026-09-22, GlobalStandard, capacity 100 (100K tokens per minute) |
| Deployment `pitchside-quality` | gpt-6.1-sol, version 2026-09-29, GlobalStandard, capacity 100 (100K tokens per minute) |

Models chosen from `az cognitiveservices model list --location southafricanorth` (newest GA, not preview):

| Role | Intended model | State |
| --- | --- | --- |
| fast | `gpt-6-luna` 2026-09-22, the fast cost-efficient model of the GPT-6 family | deployed 14 October as `pitchside-fast` |
| quality | `gpt-6.1-sol` 2026-09-29, the newest GA general-purpose model, no access request needed | deployed 14 October as `pitchside-quality` |

Quota history. On 8 October the subscription sat on the Foundry Free Tier (Tier 0), which carries quota only
for gpt-5-mini, gpt-4.1-mini and o4-mini, so every GPT-6 model reported a limit of 0 and `pitchside-fast` ran
gpt-5-mini as a stand-in. Two quota request forms were submitted on 8 October; the first was declined for
regional capacity in South Africa North. On 14 October the subscription was on Tier 1 with 1,000K tokens per
minute GlobalStandard for gpt-6-luna, gpt-6-sol, gpt-6-astra and gpt-6.1-sol in South Africa North, and both
deployments were created at 100K each. Verified on 14 October: one chat completion per deployment with a
bearer token from `az account get-access-token --resource https://cognitiveservices.azure.com` returned
HTTP 200 with model ids `gpt-6-luna-2026-09-22` and `gpt-6.1-sol-2026-09-29`.

The Azure CLI profile on this machine also holds the sipho@smesnapshot.com sponsorship subscription and the
default has been observed to switch to it between sessions. Every provisioning command passes
`--subscription 1641c920-b05d-4921-8a24-029ca939893a` explicitly.

### Role assignments

| Principal | Role | Scope |
| --- | --- | --- |
| sipho@maribodevs.com (object id `727d691c-2de9-492d-a52c-31be862bc912`) | Cognitive Services OpenAI User | `ais-pitchside-dev` |
| `id-pitchside-github` | Cognitive Services Speech User | `spch-pitchside-dev` |
| `id-pitchside-github` | Contributor | `rg-pitchside-dev`, `rg-pitchside-demo` |
| `id-pitchside-github` | User Access Administrator | `rg-pitchside-dev`, `rg-pitchside-demo` |

Pretty Kunene and Hlumela Tshazibana have no identity in this tenant yet; the tenant holds one member
account and three guests, none of them identifiable as either. Their OpenAI User assignments are added when
they are invited as guests.

### Deployment identity for GitHub Actions

| Item | Value |
| --- | --- |
| Name | `id-pitchside-github`, user-assigned managed identity in `rg-pitchside-dev` |
| Resource id | `/subscriptions/1641c920-b05d-4921-8a24-029ca939893a/resourcegroups/rg-pitchside-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-pitchside-github` |
| Client id | `89e3ce3e-6e8a-4ac7-95bc-0e3a63a30052` |
| Principal id | `06983e89-3126-499b-ac88-193588a256e9` |
| Federated credential `github-env-dev` | subject `repo:MariboDevs/pitchside:environment:dev` |
| Federated credential `github-branch-main` | subject `repo:MariboDevs/pitchside:ref:refs/heads/main` |
| Issuer, audience | `https://token.actions.githubusercontent.com`, `api://AzureADTokenExchange` |

GitHub Actions repository variables (not secrets; none of these values is secret): `AZURE_CLIENT_ID`,
`AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`. A GitHub environment named `dev` exists for the workflow to
target.

### Speech

| Item | Value |
| --- | --- |
| Resource | `spch-pitchside-dev`, kind SpeechServices, SKU F0, system-assigned identity, local auth disabled |
| Resource id | `/subscriptions/1641c920-b05d-4921-8a24-029ca939893a/resourceGroups/rg-pitchside-dev/providers/Microsoft.CognitiveServices/accounts/spch-pitchside-dev` |
| Endpoint | `https://spch-pitchside-dev.cognitiveservices.azure.com/` |

### Budgets

The subscription bills in USD. R2,500 at the September 2026 rate of about R16.4 per dollar is about 150 USD.

| Budget | Scope | Amount | Alerts |
| --- | --- | --- | --- |
| `budget-pitchside-dev` | `rg-pitchside-dev` | 150 USD per month, October 2026 to September 2027 | 80% and 100% actual spend, email sipho@maribodevs.com |
| `budget-pitchside-demo` | `rg-pitchside-demo` | 150 USD per month, October 2026 to September 2027 | 80% and 100% actual spend, email sipho@maribodevs.com |

### Fabric

| Item | Value |
| --- | --- |
| Trial capacity | `Trial-20261014T172212Z-sJ_44vBLKEyx5iy2Lj5HJw`, SKU FTL4, South Africa North, active, 60 days from 14 October 2026 |
| Capacity id | `2f03905a-f931-496e-8e8c-49485a21c5a5` |
| Capacity administrator | mario@siphomaribodevs.onmicrosoft.com (the tenant's native member account); sipho@maribodevs.com has Admin through the Fabric administrator role |
| Workspace | `pitchside-dev`, id `74e1627c-0077-4c79-a543-84c9af743169`, assigned to the trial capacity |

The trial had to be started from the native account because sipho@maribodevs.com exists in the tenant as an
external identity and Fabric reports it ineligible for a capacity trial. The trial ends around 13 December,
after judging. WP-12 creates the paid F2 capacity (`fabric.bicep`) and moves the workspace to it; the trial
is the dev capacity until then.

## GitHub (MariboDevs/pitchside, internal repository, organisation on the Enterprise plan)

| Setting | State |
| --- | --- |
| Pull request #1 (`docs/specs`) | merged with a merge commit; branch deleted |
| Ruleset `main` (id 24699198, active, target: default branch) | require a pull request before merging (merge or squash); required status check `Build, test, calibrate` from the `CI` workflow, strict; block force pushes; block deletions; Copilot code review requested automatically on push |
| Secret scanning | enabled |
| Push protection | enabled |
| Code security (GitHub Advanced Security) | enabled |
| Dependabot alerts | enabled |
| Dependabot security updates | enabled |
| Code scanning default setup (CodeQL) | enabled, default query suite, languages auto-detected: C# and GitHub Actions today; JavaScript/TypeScript is added automatically when the Angular app lands |
| Environment `dev` | created |
| Collaborators | not added in Phase 0, by decision on 8 October |
| Copilot | organisation plan is Copilot Business, seat management "assign selected", 0 seats assigned, coding agent and code review therefore unusable until a seat is assigned |

## Local toolchain (Sipho's machine, WSL2)

| Tool | Version |
| --- | --- |
| .NET SDK | 10.0.301 (satisfies `global.json` 10.0.100 with latestFeature roll-forward) |
| Aspire CLI | 13.6.1 (installed in Phase 0) |
| Node, npm | 22.15.0, 11.4.2 |
| Docker | 29.8.0, daemon running |
| Azure CLI | 2.86.0 |
| Azure Developer CLI | 1.34.0 (1.35.0 available) |
| Bicep | 0.46.1 (0.48.1 available) |
| GitHub CLI | 2.45.0, signed in as siphomaribo |
| gitleaks | 8.30.1 (installed in Phase 0 to `~/.local/bin`) |

`dotnet build Pitchside.slnx` and `dotnet test Pitchside.slnx` pass (32 tests, 0 warnings). Git author for
this repository is Sipho Maribo, sipho@maribodevs.com. The Azure MCP server (`azmcp server start`) is
registered in the Claude Code configuration, uses the Azure CLI credential and lists the subscription above.
`azd auth login` is signed in as sipho@maribodevs.com.

Note on line endings: the checkout on the Windows mount has CRLF endings while the repository stores LF, so
`git status` lists every file as modified until `.gitattributes` or `core.autocrlf` is settled. Phase 0
commits only this file.

## Needs a human

1. **Foundry quota.** Resolved on 14 October; both GPT-6 deployments exist. Nothing to do.
2. **Fabric.** Resolved on 14 October; trial capacity and `pitchside-dev` workspace exist. Nothing to do.
3. **Copilot seat.** Assign a Copilot Business seat to siphomaribo in the organisation (Settings, Copilot,
   Access) so that automatic code review and the coding agent work; the ruleset rule is already in place.
   Then in Settings, Copilot, Policies confirm "Copilot code review" and "Copilot coding agent" are enabled.
   Verification pull request #2 (8 October, closed unmerged) ran CI and CodeQL successfully under the
   ruleset, but no Copilot review was requested, which is the expected result with no seat assigned.
   Open another throwaway pull request after the seat is assigned to confirm.
4. **Team identities.** Invite Pretty Kunene and Hlumela Tshazibana as guests in the Entra tenant and add
   them as GitHub collaborators when the team decides to; then grant Cognitive Services OpenAI User on
   `ais-pitchside-dev`.

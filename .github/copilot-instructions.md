# Copilot instructions for Pitchside

Read `CLAUDE.md` at the repository root; its rules apply to every contributor, human or agent.

- .NET 10, warnings as errors, central package management in `Directory.Packages.props`. Add a package version
  there before referencing it.
- One type per file, file-scoped namespaces, no nested types, no empty catch blocks, no task markers.
- Tests are xUnit with Shouldly. A change to the engine must keep `dotnet test` green, including the
  calibration gate and the committed sample under `data/samples/seed-7`. If a deliberate engine change alters
  the sample, regenerate it with `dotnet run --project src/Pitchside.Cli -- simulate --seed 7 --out data/samples/seed-7`
  in the same pull request and say so in the description.
- Everything is fictional: never introduce a real club, player, competition or venue name.
- Nothing may reference betting or odds.
- No secrets in any file. Configuration comes from user-secrets locally and Key Vault in Azure.
- Prose has no em dashes. Write plain English.

# .NET Engineer

You are a .NET/Dapr backend engineer for Spring Voyage V2.

## Ownership

- `v2/src/Cvoya.Spring.Core/` — domain interfaces and types
- `v2/src/Cvoya.Spring.Dapr/` — Dapr actor implementations, routing, execution, orchestration
- `v2/src/Cvoya.Spring.Host.Api/` — ASP.NET Core API host
- `v2/src/Cvoya.Spring.Host.Worker/` — headless worker host
- `v2/src/Cvoya.Spring.Cli/` — CLI
- `v2/tests/Cvoya.Spring.Core.Tests/`
- `v2/tests/Cvoya.Spring.Dapr.Tests/`

## Required Reading

1. `v2/CONVENTIONS.md` — coding patterns (mandatory)
2. `v2/docs/SpringVoyage-v2-plan.md` — architecture (relevant sections for your issue)
3. `CONTRIBUTING.md` — issue and PR workflow

## Working Style

- Read the GitHub issue description and acceptance criteria carefully before starting
- Define interfaces in `Cvoya.Spring.Core`, implement in `Cvoya.Spring.Dapr`
- Write tests alongside implementation — every public method needs at least one test
- Use `ActorTestBase<TActor>` for actor tests
- Follow the message handling pattern from CONVENTIONS.md Section 10
- Run `dotnet build` and `dotnet test` before committing

## Cross-Repo Awareness

Some issues in this repo are tagged `cloud-dependency` — they are driven by needs in the private cloud repo. When working on these:

- The **public rationale** in the issue explains why the interface/extension is needed. Design for that public use case.
- Keep `Cvoya.Spring.Core` free of external dependencies. Interfaces here must be implementation-agnostic.
- The cloud repo will add its own implementation via DI. Design extension points that support decoration, composition, or keyed services.
- Do NOT add cloud-specific concepts (tenants, billing, etc.) to Core interfaces unless the issue explicitly calls for it with a public rationale.

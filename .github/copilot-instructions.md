# Copilot instructions for BaGetter

## Build, test, and lint commands
- **Run the service locally:** from `src\BaGetter`, run `dotnet run`.
- **Build (Docker image):** `docker build .` (used in `.github\workflows\publish.yml`).
- **Run tests:** `dotnet test tests\BaGetter.Web.Tests\BaGetter.Web.Tests.csproj`.
- **Run a single test:** `dotnet test tests\BaGetter.Web.Tests\BaGetter.Web.Tests.csproj --filter "FullyQualifiedName~PackageModelFacts"`.
- **Docs site (Docusaurus, in `docs\`):** `yarn`, `yarn start`, `yarn build`.

## High-level architecture
- `src\BaGetter` is the app entry point and hosts the NuGet service APIs; most core logic lives in `src\BaGetter.Core`.
- `src\BaGetter.Web` contains the NuGet server APIs and the web UI.
- `src\BaGetter.Protocol` is the SDK for interacting with NuGet servers.
- Database providers are split into separate projects: `BaGetter.Database.*` (MySql, PostgreSql, Sqlite, SqlServer).
- Cloud integrations are separated by provider: `BaGetter.Aliyun`, `BaGetter.Aws`, `BaGetter.Azure`, `BaGetter.Gcp`, `BaGetter.Tencent`.
- Tests live under `tests\BaGetter.Web.Tests` and reference the web project.

## Key conventions
- Central Package Management is enabled; add package versions in `Directory.Packages.props` and omit `Version` in project `PackageReference` entries.
- The .NET SDK version is pinned in `global.json` (`9.0.306`).
- Test dependencies are centralized in `tests\Directory.Build.props` and tests use xUnit + Moq.
- `nuget.config` clears default sources and uses only `https://api.nuget.org/v3/index.json`.

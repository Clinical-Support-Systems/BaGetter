# Project Context

- **Owner:** Kori Francis
- **Project:** CSS fork of BaGetter powering private NuGet feeds at https://packages.clinicalsupportsystems.com.
- **Stack:** ASP.NET Core (.NET 9), Azure App Service for Containers, Azure Container Registry, Azure Blob Storage, Azure Table Storage.
- **Created:** 2026-05-03T15:50:35.157-04:00

## Learnings

- 2026-05-03: Initial team setup for auth model replacement and compatibility-first rollout.
- 2026-05-03T15:50:35.157-04:00: Produced 7-PR phased sequence for Basic auth hardening + Google OAuth. Key files: `src/BaGetter.Web/Authentication/NugetBasicAuthenticationHandler.cs`, `src/BaGetter/Startup.cs`, `src/BaGetter.Web/Feeds/FeedReadAuthenticationMiddleware.cs`, `src/BaGetter.Core/Configuration/BaGetterOptions.cs`, `src/BaGetter.Core/Configuration/FeedOptions.cs`.
- `NugetBasicAuthenticationHandler` already exists and handles per-feed credential/ApiKey validation including SHA-256 hashed keys. PR 1 is hardening, not greenfield.
- `Startup.cs` has a verbose `LogWarning` diagnostic middleware that logs all request/response paths — potential auth header leak in staging; flag for removal in PR 4.
- `FeedReadAuthenticationMiddleware` issues `ChallengeAsync()` without scheme-locking; if Google OAuth becomes the default challenge scheme, this middleware will redirect NuGet clients to OAuth. This is the critical sequencing risk in PR 3→4 transition.
- Auth middleware order in pipeline: `UseAuthentication → UseFeedResolutionMiddleware → UseFeedReadAuthenticationMiddleware → UseAuthorization`. Must be preserved.
- `BaGetterOptions.Authentication.Credentials[]` is the global Basic credential store. `FeedOptions.ApiKeys[]` is per-feed push key store. These are separate config paths.
- 10 human blockers documented in `.squad/decisions/inbox/mal-auth-pr-sequence.md`. Most critical: Google OAuth client provisioning (blocker 1), admin identity model (blocker 2), upload page fate (blocker 4).

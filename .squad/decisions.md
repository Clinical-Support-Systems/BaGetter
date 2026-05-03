# Squad Decisions

## Active Decisions

### 2026-05-03T15:50:35.157-04:00: Auth model for NuGet + UI
**By:** Squad (Coordinator)

**What:**
- NuGet protocol routes use Basic auth (including token-style Basic auth).
- Browser/admin UI uses Google OAuth with a secure cookie.
- NuGet protocol routes must never redirect to Google OAuth; UI routes may redirect.
- /health (or a minimal health endpoint) may remain anonymous.
- /upload is disabled or Google-admin-only.
- Preserve BaGetter ApiKey support for package push.
- Future support should allow feed-scoped and package-scoped tokens, especially for licensed packages.
- Do not remove IP restrictions until auth tests pass in production.
- Preserve root /v3/index.json compatibility and multi-feed support.

**Why:** Replace brittle IP allowlisting without breaking existing restore/push flows or customer consumption tooling.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

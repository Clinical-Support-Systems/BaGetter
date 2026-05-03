# Multi-Feed Design (CSS Fork)

## Goals

- Support multiple logical feeds in one BaGetter deployment.
- Keep existing single-feed behavior when `Feeds` is not configured.
- Preserve root feed compatibility while enabling canonical `/{feed}` URLs.
- Keep migration explicit and operator-invoked.

## Routing and Alias Rules

- Canonical NuGet protocol shape:
  - `/{feed}/v3/index.json`
  - `/{feed}/v3/search`
  - `/{feed}/v3/autocomplete`
  - `/{feed}/v3/registration/...`
  - `/{feed}/v3/package/...`
  - `/{feed}/api/v2/package...`
- Root protocol routes remain available and alias to the default feed for compatibility.
- Root UI routes (`/`, `/packages`, `/upload`, `/stats`) alias to the default feed.
- Feed-prefixed UI routes are canonical (`/{feed}`, `/{feed}/packages/...`, `/{feed}/upload`, `/{feed}/stats`).

## Storage and Database Scoping

- Azure Blob package paths are feed-scoped in multi-feed mode:
  - `{feedPrefix}/{lowerId}/{normalizedVersion}/...`
- Legacy single-feed mode keeps the historical `packages/...` prefix.

- Azure Table keys are feed-scoped in multi-feed mode:
  - `PartitionKey = "{partitionPrefix}|{lowerPackageId}"`
  - `RowKey = "{lowerNormalizedVersion}"`
- Legacy single-feed mode keeps:
  - `PartitionKey = "{lowerPackageId}"`
  - `RowKey = "{lowerNormalizedVersion}"`

## Search Behavior (Azure Table)

- Table remains the authoritative index (no additional index store).
- Query filters are OData-only and feed-bounded.
- Prefix search is constrained to a feed partition range.
- Empty search is constrained to the full feed partition range.
- Versions autocomplete queries the exact feed + package partition.

## Migration Policy

- Migration is explicit and operator-run.
- No automatic startup migration.
- No implicit key rewrite at runtime.
- Root feed compatibility remains through aliasing during migration windows.

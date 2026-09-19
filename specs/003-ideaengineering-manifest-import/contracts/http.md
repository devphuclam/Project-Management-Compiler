# Contract: HTTP and UI Integration

## Manifest import

```text
POST /api/manifest-import
Content-Type: application/json

{
  "repositoryRoot": "D:\\Work\\Projects\\IDEAEngineering",
  "manifestPath": "planning/project-management-compiler-manifest.json",
  "mode": "GIT_COMMIT",
  "requestedCommit": "<40-hex-sha>"
}
```

Response `200` returns the result envelope for valid, candidate, preview, and
validation-failed attempts. Malformed JSON/request shape returns `400`. No route
accepts a raw source body or a path outside the repository boundary.

Read routes:

- `GET /api/manifest-import/official`
- `GET /api/manifest-import/latest`
- `GET /api/manifest-import/preview`

Proposal routes:

- `GET /api/proposals`
- `POST /api/proposals`
- `PUT /api/proposals/{id}`
- `POST /api/proposals/{id}/preview`
- `GET /api/proposals/{id}/export`

The existing `/api/execution` endpoint returns the compatibility proposal marker and
never changes official source execution.

## UI obligations

The import surface exposes repository root, manifest path, requested commit, and mode.
The primary status surface shows official/preview/candidate, safe repository identity,
commit/preview identity, contract, validation, diagnostics count, import time,
snapshot ID, and source readiness. Row inspection separates recording state,
execution/result state, baseline, actual, remaining, forecast, blocker, health, and
provenance. Proposal actions use proposal language and explicit preview mode.

# Contract: Import the Newest Official Commit Available Locally

## Purpose

Give a non-technical user a normal source-read action without asking them to find a Git branch or commit identifier. The operation is local-only and routes through the existing manifest validation/import/state path.

## Endpoint

`POST /api/manifest-import/default-branch`

## Request

JSON body:

```json
{
  "repositoryRoot": "C:\\Projects\\Example",
  "manifestPath": "planning/project-management-compiler-manifest.json",
  "analysisAsOfOverride": null,
  "maxFileBytes": null,
  "maxTotalBytes": null,
  "mapping": null
}
```

`repositoryRoot` and `manifestPath` are required. Existing optional analysis date, bounded capture-size, and mapping settings may be forwarded unchanged. The request MUST NOT accept import mode, a branch name, a tag, or a commit SHA.

## Resolution and import sequence

1. Validate the request and local repository root using the existing import constraints.
2. Read the local symbolic target for `refs/remotes/origin/HEAD` using the existing injectable Git command seam.
3. Accept only a complete target under `refs/remotes/origin/` with a valid ref name; reject other namespaces and malformed values.
4. Resolve the target to a full commit object ID locally. No `fetch`, network call, ref update, checkout, or working-tree fallback is allowed.
5. Invoke the existing importer once with official-commit mode and that exact resolved commit ID.
6. Record the result through `ManifestImportApplicationService` and `CompilerApplicationState`, preserving existing official/candidate/preview authority rules.
7. Return the existing manifest-import response shape, including the exact resolved source identity and classification.

The identity displayed to the user MUST refer to the resolved commit actually imported, not merely the symbolic ref name.

## Success response

`200 OK` with the existing manifest-import response contract. A valid official result is then available through current state/view endpoints. The endpoint does not itself persist browser preferences or auto-navigate the client; client navigation happens only after an official success response.

## Failures

| Condition | HTTP behavior | Required user recovery |
|---|---|---|
| Invalid request fields | Existing `400` request error behavior. | Correct the local folder/manifest values. |
| Local default symbolic ref absent | `422` typed manifest-import error; no import attempt is promoted. | Offer the existing advanced exact-source/commit action. |
| Ref target malformed, outside approved remote-tracking namespace, or not a commit | `422` typed error; no fallback. | Explain that no safe default source version was available; offer exact source selection. |
| Git read/resolve command fails | `422` typed error (or cancellation propagation consistent with current API); no official state replacement. | Show concise recovery guidance and preserve any loaded official project. |
| Manifest validation/import fails | Existing manifest-import error/result behavior. | Use existing diagnostics/recovery action; retain previous official state. |
| Import is classified as candidate or preview | Existing response classification; it does not become official. | Keep candidate/preview visibly separate; retain prior official snapshot. |

Errors MUST NOT expose credentials, raw environment variables, or private source contents. Diagnostics may include safe local context already permitted by the existing response policy.

## Security and authority

- The source repository is a read-only input; this endpoint must not change its files or Git metadata.
- No network access is permitted or implied.
- Only the exact resolved commit may be read.
- A failure never replaces a valid official snapshot.
- Existing exact-commit import remains available as the deliberate advanced recovery flow.

## Acceptance checks

- Fake-runner tests assert the symbolic-ref and commit-resolution commands, allowed arguments, limits, cancellation, and that no network/mutating Git command is called.
- Application tests assert one resolution, one official import with exact SHA, and official-state retention on failures/candidate outcomes.
- API tests assert request cannot select a mode or commit and successful response identifies the commit actually imported.

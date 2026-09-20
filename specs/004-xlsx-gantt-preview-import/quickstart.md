# Quickstart: XLSX Gantt Preview Import

## Prerequisites

- .NET 10 SDK/runtime already available.
- A built Project Management Compiler checkout.
- A newly exported `<ProjectName>_CARIO_GANTT.xlsx` produced after the PMC
  provenance marker is implemented.
- No Excel installation or external service is required.

## Build and focused tests

From the repository root:

```powershell
dotnet build .\ProjectManagementCompiler.sln --no-restore
dotnet run --project .\tests\ProjectManagementCompiler.Tests\ProjectManagementCompiler.Tests.csproj --no-restore
```

The test runner must report zero failures. The feature-specific tests cover the
export marker, valid import, package and contract rejection, state isolation,
and lifecycle behavior.

## Manual local flow

1. Start the application with the repository's normal local launcher or:

   ```powershell
   dotnet run --project .\src\ProjectManagementCompiler\ProjectManagementCompiler.csproj
   ```

2. Import an official IDEAEngineering manifest and export a fresh
   `*_CARIO_GANTT.xlsx` workbook.
3. Choose `Import XLSX preview` in the source-intake panel.
4. Select the newly exported workbook.
5. Confirm the UI switches to `XLSX Preview`, shows `Read-only` and
   `Non-authoritative`, and displays the task table plus daily Gantt.
6. Confirm proposal controls are unavailable in preview mode.
7. Select `Official source` and confirm the official Gantt and dashboard return.
8. Select `Clear preview` and confirm the preview disappears.

## API smoke flow

With the app listening on `http://127.0.0.1:5050`:

```powershell
$xlsx = 'C:\path\to\Project_CARIO_GANTT.xlsx'
$response = Invoke-WebRequest `
  -Uri 'http://127.0.0.1:5050/api/xlsx-preview' `
  -Method Post `
  -Form @{ file = Get-Item -LiteralPath $xlsx }
$response.StatusCode
$response.Content | ConvertFrom-Json | Select-Object fileName, projectId, contractVersion, readOnly, authoritative

Invoke-RestMethod 'http://127.0.0.1:5050/api/xlsx-preview'
Invoke-WebRequest 'http://127.0.0.1:5050/api/xlsx-preview' -Method Delete
```

Expected success values are `readOnly = true` and `authoritative = false`.
The GET after DELETE returns `404` with `NO_ACTIVE_XLSX_PREVIEW`.

## Negative validation checks

Use feature tests or a copied workbook to verify each of these produces a
structured `422` validation response and leaves official state unchanged:

- remove one required worksheet;
- remove or alter `PMC_EXPORT_KIND` or `PMC_EXPORT_CONTRACT_VERSION`;
- alter a required header or date cell;
- duplicate a task identity;
- truncate/corrupt the ZIP package;
- exceed the application package ceiling.

The detailed package and endpoint rules are in
[contracts/xlsx-preview.md](contracts/xlsx-preview.md); the model and lifecycle
rules are in [data-model.md](data-model.md).

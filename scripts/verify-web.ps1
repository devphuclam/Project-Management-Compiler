[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '..'))
$appDll = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\ProjectManagementCompiler\bin\Debug\net10.0\ProjectManagementCompiler.dll'))
$appWorkingDirectory = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\ProjectManagementCompiler'))
$fixture = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'tests\fixtures\ideaengineering-real-shaped'))
$programPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\ProjectManagementCompiler\Program.cs'))
$appJsPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\ProjectManagementCompiler\wwwroot\app.js'))
$indexHtmlPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\ProjectManagementCompiler\wwwroot\index.html'))
$stylesCssPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\ProjectManagementCompiler\wwwroot\styles.css'))

if (-not (Test-Path -LiteralPath $appDll -PathType Leaf)) {
    throw "Built application was not found at '$appDll'."
}

function Assert-Condition {
    param(
        [Parameter(Mandatory)]
        [bool] $Condition,
        [Parameter(Mandatory)]
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-JsonApi {
    param(
        [Parameter(Mandatory)]
        [string] $Uri,
        [Parameter(Mandatory)]
        [string] $Method,
        [Parameter()]
        [object] $Body
    )

    $parameters = @{
        Uri = $Uri
        Method = $Method
        TimeoutSec = 30
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 50 -Compress
    }

    Invoke-RestMethod @parameters
}

function Invoke-XlsxPreviewUpload {
    param(
        [Parameter(Mandatory)]
        [string] $Uri,
        [Parameter(Mandatory)]
        [string] $FilePath
    )

    $client = [System.Net.Http.HttpClient]::new()
    $multipart = [System.Net.Http.MultipartFormDataContent]::new()
    $fileContent = [System.Net.Http.ByteArrayContent]::new([IO.File]::ReadAllBytes($FilePath))
    try {
        $multipart.Add($fileContent, 'file', [IO.Path]::GetFileName($FilePath))
        $response = $client.PostAsync($Uri, $multipart).GetAwaiter().GetResult()
        [PSCustomObject]@{
            StatusCode = [int]$response.StatusCode
            Body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }
    finally {
        $fileContent.Dispose()
        $multipart.Dispose()
        $client.Dispose()
    }
}

$process = Start-Process -FilePath 'dotnet' -ArgumentList @('exec', ('"{0}"' -f $appDll)) -WorkingDirectory $appWorkingDirectory -WindowStyle Hidden -PassThru
$temporaryXlsx = $null
try {
    $health = $null
    for ($attempt = 0; $attempt -lt 40 -and $null -eq $health; $attempt++) {
        try {
            $health = Invoke-RestMethod -Uri 'http://127.0.0.1:5050/api/health' -TimeoutSec 2
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }

    Assert-Condition ($null -ne $health) 'Loopback API did not become ready.'
    Assert-Condition ($health.binding -eq 'http://127.0.0.1:5050') 'API binding must be the loopback MVP binding.'
    Assert-Condition (-not $health.publicNetworkBinding) 'API must not advertise a public network binding.'

    $summary = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/compile' -Method Post -Body @{
        sourcePath = $fixture
        asOfDate = '2026-09-28'
    }
    Assert-Condition ($summary.project.id -eq 'IE-PROD-ROADMAP-001') 'API compile did not return the real-shaped project.'
    Assert-Condition ($summary.views.dashboard.totalCards -eq 53) 'API compile did not preserve the 53 delivery cards.'
    Assert-Condition ($null -ne $summary.sources -and $summary.sources.Count -gt 0) 'API compile did not expose safe source metadata.'
    $summaryJson = $summary | ConvertTo-Json -Depth 50 -Compress
    Assert-Condition (-not $summaryJson.Contains($fixture, [StringComparison]::OrdinalIgnoreCase)) 'Application source metadata must not expose an absolute source path.'
    Assert-Condition (-not ($summaryJson -match '"content"\s*:')) 'Application source metadata must not expose captured document content.'
    Assert-Condition (@($summary.sources.documents.relativeFile | Where-Object { [IO.Path]::IsPathRooted($_) }).Count -eq 0) 'Application source metadata must expose relative document paths only.'

    $disabledReadinessSummary = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/compile' -Method Post -Body @{
        sourcePath = $fixture
        asOfDate = '2026-09-28'
        includeManagementEvidence = $false
        managementEvidenceIncrementPath = 'specs/004-technical-pilot-readiness'
    }
    Assert-Condition ($disabledReadinessSummary.managementControl.discoveryState -eq 'NOT_REQUESTED') 'A disabled readiness switch must remain NOT_REQUESTED even when a path is supplied.'
    Assert-Condition (@($disabledReadinessSummary.managementEvidence.observations).Count -eq 0) 'A disabled readiness switch must not attach management evidence.'
    Assert-Condition (@($disabledReadinessSummary.sources.documents.relativeFile | Where-Object { $_ -like 'specs/004-technical-pilot-readiness/*' }).Count -eq 0) 'A disabled readiness switch must not capture readiness increment files.'

    $readinessSummary = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/compile' -Method Post -Body @{
        sourcePath = $fixture
        asOfDate = '2026-09-28'
        includeManagementEvidence = $true
        managementEvidenceIncrementPath = 'specs/004-technical-pilot-readiness'
    }
    Assert-Condition ($readinessSummary.managementControl.discoveryState -eq 'KNOWN') 'API readiness compile did not resolve the active increment.'
    Assert-Condition ($readinessSummary.managementEvidence.incrementId -eq 'IE-INC-READY-001') 'API readiness compile did not preserve increment identity.'
    Assert-Condition ($readinessSummary.managementEvidence.incrementPhaseId -eq 'PH0') 'API readiness compile did not preserve the explicit readiness phase.'
    Assert-Condition (@($readinessSummary.managementEvidence.observations | Where-Object { $_.evidenceKind -eq 'READINESS_CHECK' }).Count -eq 7) 'API readiness compile did not expose P01-P07 evidence.'
    Assert-Condition (@($readinessSummary.managementEvidence.observations | Where-Object { $_.evidenceKind -eq 'DECISION_RECORD' }).Count -eq 6) 'API readiness compile did not expose D0-D5 decisions.'
    Assert-Condition (@($readinessSummary.managementEvidence.observations | Where-Object { $_.evidenceKind -eq 'GATE_EXECUTION' -and $_.stateCode -eq 'NOT-RUN' }).Count -eq 1) 'API readiness compile must expose PG4 execution state separately.'
    Assert-Condition (@($readinessSummary.managementEvidence.observations | Where-Object { $_.evidenceKind -eq 'GATE_OUTCOME' -and $_.resultCode -eq 'NOT-APPLICABLE' }).Count -eq 1) 'API readiness compile must expose PG4 outcome separately.'
    Assert-Condition ($readinessSummary.views.managementControl.readiness.Count -eq 7) 'ManagementControlView must expose the seven readiness rows.'
    Assert-Condition ($readinessSummary.views.managementControl.currentGate.executionState -eq 'NOT-RUN' -and $readinessSummary.views.managementControl.currentGate.outcome -eq 'NOT-APPLICABLE') 'ManagementControlView must keep PG4 execution and outcome separate.'
    Assert-Condition (@($readinessSummary.views.managementControl.attentionGroups | Where-Object { $_.code -eq 'OPEN_DECISIONS' }).Count -eq 1) 'ManagementControlView must group open decisions.'
    $readinessJson = $readinessSummary | ConvertTo-Json -Depth 50 -Compress
    Assert-Condition (-not $readinessJson.Contains($fixture, [StringComparison]::OrdinalIgnoreCase)) 'Readiness API output must not expose an absolute source path.'
    Assert-Condition (-not $readinessJson.Contains('Reviewer evidence pending', [StringComparison]::Ordinal)) 'Readiness API output must not expose raw source content.'

    $project = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/project' -Method Get
    Assert-Condition ($project.phases.Count -eq 6) 'Canonical project did not preserve six phases.'
    Assert-Condition ($project.workPackages.Count -eq 35) 'Canonical project did not preserve 35 work packages.'
    Assert-Condition ($project.deliveryCards.Count -eq 53) 'Canonical project did not preserve 53 delivery cards.'
    Assert-Condition ($project.milestones.Count -eq 7) 'Canonical project did not preserve seven milestones.'
    Assert-Condition ($project.baseline.plannedEffortHours -eq 512) 'Canonical project did not preserve 512 authoritative hours.'
    Assert-Condition ($project.baseline.reserveHours -eq 88) 'Canonical project did not preserve 88 reserve hours.'
    Assert-Condition ($project.baseline.capacityHours -eq 600) 'Canonical project did not preserve 600 capacity hours.'
    Assert-Condition (@($project.workPackages | Where-Object { $_.id -eq 'P04' }).Count -eq 1) 'WorkPackage P04 must remain present.'
    Assert-Condition (@($project.deliveryCards | Where-Object { $_.id -eq 'P04' }).Count -eq 1) 'DeliveryCard P04 must remain present.'
    Assert-Condition (-not @($summary.warnings | Where-Object { $_.code -in @('INVALID_DEPENDENCY_SUBJECT_KIND', 'INVALID_DEPENDENCY_PREDECESSOR_KIND', 'DUPLICATE_DEPENDENCY') }).Count) 'Real-shaped compile reported a false typed-ID collision diagnostic.'

    $views = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/views' -Method Get
    Assert-Condition ($views.gantt.items.Count -eq 53) 'API views did not expose the shared Gantt projection.'
    Assert-Condition ($views.gantt.milestones.Count -eq 7) 'API views did not expose milestone markers.'
    $ganttP04 = @($views.gantt.items | Where-Object { $_.workItemId -eq 'P04' })
    Assert-Condition ($ganttP04.Count -eq 1) 'Gantt must expose DeliveryCard P04 exactly once.'
    Assert-Condition ($ganttP04[0].dependencyIds -contains 'P03') 'Gantt P04 must use its DeliveryCard dependency.'
    $ganttP04Plan = @($ganttP04[0].lanes | Where-Object { $_.lane -eq 'PLAN' })
    Assert-Condition ($ganttP04Plan.Count -eq 1) 'Gantt P04 must retain its PLAN lane.'
    Assert-Condition (-not $ganttP04[0].hasExecutionEvidence) 'Planning-only Gantt P04 must not claim execution evidence.'
    Assert-Condition (@($ganttP04[0].sourceReferences).Count -gt 0) 'Gantt P04 must expose safe item-level source references.'
    Assert-Condition (@($ganttP04[0].lanes | Where-Object { $_.lane -eq 'ACTUAL' }).Count -eq 0) 'Planning-only Gantt must not fabricate ACTUAL lanes.'
    Assert-Condition (@($views.gantt.milestones | Where-Object { $_.plannedDate }).Count -eq 7) 'Gantt milestones must retain all seven authored dates.'
    Assert-Condition (@($views.gantt.milestones | Where-Object { @($_.sourceReferences).Count -gt 0 }).Count -eq 7) 'Gantt milestones must expose safe item-level source references.'
    Assert-Condition (@($views.dependencyNetwork.nodes | Where-Object { $_.id -eq 'P04' }).Count -eq 2) 'Dependency network must retain both typed P04 nodes.'

    $jsonResponse = Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/exports/project.json' -TimeoutSec 30
    $jsonText = [string] $jsonResponse.Content
    $contentDisposition = [string] $jsonResponse.Headers['Content-Disposition']
    Assert-Condition ($contentDisposition -match '_project\.json') 'JSON export filename must follow the <ProjectName>_project.json contract.'
    Assert-Condition ($contentDisposition -notmatch 'filename="project\.json"') 'JSON export must not use the generic project.json filename.'
    Assert-Condition (-not ($jsonText -match '"content"\s*:')) 'Persisted JSON must not leak captured source content.'
    Assert-Condition (-not $jsonText.Contains($fixture, [StringComparison]::OrdinalIgnoreCase)) 'Persisted JSON must not leak an absolute source path.'
    Assert-Condition ($jsonText.Contains('"executionOverlay"', [StringComparison]::Ordinal)) 'Persisted JSON must include the execution overlay.'
    Assert-Condition ($jsonText.Contains('"managementEvidence"', [StringComparison]::Ordinal)) 'Persisted JSON must include the management evidence layer.'

    $reopened = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/reopen' -Method Post -Body @{
        json = $jsonText
        asOfDate = '2026-09-28'
    }
    Assert-Condition ($reopened.baseline.id -eq $summary.baseline.id) 'API reopen changed the baseline identity.'
    Assert-Condition ($reopened.baseline.version -eq $summary.baseline.version -and $reopened.baseline.plannedEffortHours -eq $summary.baseline.plannedEffortHours) 'API reopen changed immutable baseline values.'
    Assert-Condition ($reopened.managementEvidence.incrementId -eq 'IE-INC-READY-001' -and @($reopened.managementEvidence.observations | Where-Object { $_.evidenceKind -eq 'READINESS_CHECK' }).Count -eq 7) 'API reopen did not preserve management evidence.'
    Assert-Condition ($reopened.views.managementControl.currentGate.executionState -eq 'NOT-RUN' -and $reopened.views.managementControl.currentGate.outcome -eq 'NOT-APPLICABLE') 'API reopen did not preserve separate gate evidence.'
    Assert-Condition ($reopened.analysis.executionStatus.overdue -eq 0 -and $reopened.analysis.executionStatus.atRisk -eq 0) 'Planning-only API reopen must not fabricate execution alerts.'

    $temporaryXlsx = [IO.Path]::Combine([IO.Path]::GetTempPath(), ('pmc-verify-{0}.xlsx' -f [Guid]::NewGuid().ToString('N')))
    Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/exports/cario.xlsx' -OutFile $temporaryXlsx -TimeoutSec 30
    $fileStream = [IO.File]::OpenRead($temporaryXlsx)
    $archive = [IO.Compression.ZipArchive]::new($fileStream, [IO.Compression.ZipArchiveMode]::Read)
    try {
        $entryNames = @($archive.Entries | ForEach-Object FullName)
        foreach ($required in @(
            '[Content_Types].xml',
            '_rels/.rels',
            'xl/workbook.xml',
            'xl/_rels/workbook.xml.rels',
            'xl/worksheets/sheet1.xml',
            'xl/worksheets/sheet2.xml',
            'xl/worksheets/sheet3.xml',
            'xl/worksheets/sheet4.xml',
            'xl/worksheets/sheet5.xml',
            'xl/worksheets/sheet6.xml',
            'xl/worksheets/sheet7.xml',
            'xl/styles.xml'
        )) {
            Assert-Condition ($entryNames -contains $required) "XLSX package is missing '$required'."
        }

        $workbookEntry = $archive.GetEntry('xl/workbook.xml')
        $reader = [IO.StreamReader]::new($workbookEntry.Open())
        try {
            $workbookXml = [xml] $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $sheetNames = @($workbookXml.SelectNodes("//*[local-name()='sheet']") | ForEach-Object { $_.name })
        Assert-Condition (([string]::Join(',', $sheetNames)) -eq '01_TASKS,02_ASSIGNMENTS,03_CHILDREN_MILESTONES,04_DEPENDENCIES,05_PROJECT_INFO,06_IMPORT_WARNINGS,07_GANTT') 'XLSX worksheet names are not the six-CARIO-plus-Gantt contract.'

        $taskEntry = $archive.GetEntry('xl/worksheets/sheet1.xml')
        $taskReader = [IO.StreamReader]::new($taskEntry.Open())
        try {
            $taskXml = [xml] $taskReader.ReadToEnd()
        }
        finally {
            $taskReader.Dispose()
        }

        $taskHeader = @($taskXml.SelectNodes("//*[local-name()='row'][1]//*[local-name()='t']") | ForEach-Object { $_.'#text' })
        Assert-Condition (([string]::Join('|', $taskHeader)) -eq 'Work Item Type|Task ID|Phase|Work Package|Nội dung công việc|Ngày bắt đầu dự kiến|Deadline|Mức độ ưu tiên|Đơn vị / Phòng ban|Ban|Ghi chú|Trạng thái ban đầu|Planned Effort (hours)|Baseline / Analysis State|Source Reference') '01_TASKS headers do not match the CARIO contract.'
        Assert-Condition ($taskXml.OuterXml.Contains('Decision', [StringComparison]::Ordinal) -or $taskXml.OuterXml.Contains('Milestone', [StringComparison]::Ordinal)) '01_TASKS must include decision/milestone records.'

        $dependencyEntry = $archive.GetEntry('xl/worksheets/sheet4.xml')
        $dependencyReader = [IO.StreamReader]::new($dependencyEntry.Open())
        try {
            $dependencyXml = [xml] $dependencyReader.ReadToEnd()
        }
        finally {
            $dependencyReader.Dispose()
        }
        $dependencyHeader = @($dependencyXml.SelectNodes("//*[local-name()='row'][1]//*[local-name()='t']") | ForEach-Object { $_.'#text' })
        Assert-Condition (([string]::Join('|', $dependencyHeader)) -eq 'Subject Kind|Subject ID|Predecessor Kind|Predecessor ID|Dependency Type|Analysis Eligibility|Validation State|Source Reference') '04_DEPENDENCIES headers must retain typed endpoints.'
        Assert-Condition ($dependencyXml.OuterXml.Contains('WorkPackage', [StringComparison]::Ordinal) -and $dependencyXml.OuterXml.Contains('DeliveryCard', [StringComparison]::Ordinal)) '04_DEPENDENCIES must distinguish work-package and delivery-card edges.'
        $warningEntry = $archive.GetEntry('xl/worksheets/sheet6.xml')
        $warningReader = [IO.StreamReader]::new($warningEntry.Open())
        try {
            $warningXml = [xml] $warningReader.ReadToEnd()
        }
        finally {
            $warningReader.Dispose()
        }
        Assert-Condition ($warningXml.OuterXml.Contains('CARIO_MAPPING_PRIORITY_UNRESOLVED', [StringComparison]::Ordinal)) 'CARIO warning sheet must include unresolved priority mappings.'
        Assert-Condition ($warningXml.OuterXml.Contains('CARIO_MAPPING_DEPARTMENT_UNRESOLVED', [StringComparison]::Ordinal)) 'CARIO warning sheet must include unresolved department mappings.'
        Assert-Condition ($warningXml.OuterXml.Contains('CARIO_MAPPING_TEAM_UNRESOLVED', [StringComparison]::Ordinal)) 'CARIO warning sheet must include unresolved team mappings.'
        $p04TaskRow = @($taskXml.SelectNodes("//*[local-name()='row']") | Where-Object {
            @($_.SelectNodes(".//*[local-name()='t']") | ForEach-Object { $_.'#text' }) -contains 'P04'
        } | Select-Object -First 1)
        Assert-Condition ($p04TaskRow.Count -eq 1) 'CARIO task sheet must contain the real-shaped P04 row.'
        $p04TaskRowText = [string]::Join('|', @($p04TaskRow[0].SelectNodes(".//*[local-name()='t']") | ForEach-Object { $_.'#text' }))
        Assert-Condition ($p04TaskRowText.Contains('2026-09-23', [StringComparison]::Ordinal)) 'CARIO task sheet must retain the real-shaped P04 baseline date.'
        Assert-Condition (-not $p04TaskRowText.Contains('2026-09-25', [StringComparison]::Ordinal)) 'CARIO planned-date cells must not be overwritten by P04 actual start.'

        $ganttEntry = $archive.GetEntry('xl/worksheets/sheet7.xml')
        $ganttReader = [IO.StreamReader]::new($ganttEntry.Open())
        try {
            $ganttXml = [xml] $ganttReader.ReadToEnd()
        }
        finally {
            $ganttReader.Dispose()
        }
        Assert-Condition ($ganttXml.OuterXml.Contains('Recorded %', [StringComparison]::Ordinal) -and $ganttXml.OuterXml.Contains('Not recorded', [StringComparison]::Ordinal)) 'Gantt export must expose a fail-safe recorded percentage column.'
        Assert-Condition ($ganttXml.OuterXml.Contains('2026-09-18', [StringComparison]::Ordinal) -and $ganttXml.OuterXml.Contains('2026-09-19', [StringComparison]::Ordinal)) 'Gantt export must expose adjacent daily axis columns.'
        Assert-Condition ($ganttXml.OuterXml.Contains('PLAN', [StringComparison]::Ordinal) -and $ganttXml.OuterXml.Contains('ACTUAL', [StringComparison]::Ordinal) -and $ganttXml.OuterXml.Contains('ALERT', [StringComparison]::Ordinal)) 'Gantt export must preserve the distinct management lanes.'
        Assert-Condition ($ganttXml.OuterXml.Contains('xSplit="15"', [StringComparison]::Ordinal) -and $ganttXml.OuterXml.Contains('ySplit="5"', [StringComparison]::Ordinal)) 'Gantt export must freeze identity and header regions.'
    }
    finally {
        $archive.Dispose()
        $fileStream.Dispose()
    }

    $previewUpload = Invoke-XlsxPreviewUpload -Uri 'http://127.0.0.1:5050/api/xlsx-preview' -FilePath $temporaryXlsx
    Assert-Condition ($previewUpload.StatusCode -eq 200) "XLSX preview upload must return 200; got $($previewUpload.StatusCode)."
    $previewPayload = $previewUpload.Body | ConvertFrom-Json
    Assert-Condition ($previewPayload.readOnly -eq $true -and $previewPayload.authoritative -eq $false) 'XLSX preview must be explicitly read-only and non-authoritative.'
    Assert-Condition ($previewPayload.projectId -eq $summary.project.id -and $previewPayload.contractVersion -eq '1.0') 'XLSX preview must preserve the exported project identity and contract version.'
    Assert-Condition (@($previewPayload.dateAxis).Count -gt 0 -and @($previewPayload.tasks).Count -gt 0) 'XLSX preview must expose the daily axis and task rows.'

    $activePreview = Invoke-RestMethod -Uri 'http://127.0.0.1:5050/api/xlsx-preview' -TimeoutSec 30
    Assert-Condition ($activePreview.snapshotId -eq $previewPayload.snapshotId) 'XLSX preview GET must return the active uploaded snapshot.'

    $clearPreviewResponse = Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/xlsx-preview' -Method Delete -TimeoutSec 30
    Assert-Condition ([int]$clearPreviewResponse.StatusCode -eq 204) 'XLSX preview clear must return 204.'
    $emptyPreviewStatus = $null
    try {
        Invoke-RestMethod -Uri 'http://127.0.0.1:5050/api/xlsx-preview' -TimeoutSec 30 | Out-Null
    }
    catch {
        if ($_.Exception.Response) {
            $emptyPreviewStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }
    Assert-Condition ($emptyPreviewStatus -eq 404) 'Cleared XLSX preview must return NO_ACTIVE_XLSX_PREVIEW.'

    $sourceRoot = $null
    $configuredSourceRoot = [Environment]::GetEnvironmentVariable('IDEAENGINEERING_ROOT')
    if (-not [string]::IsNullOrWhiteSpace($configuredSourceRoot) -and (Test-Path -LiteralPath (Join-Path $configuredSourceRoot '.git') -PathType Container)) {
        $sourceRoot = [IO.Path]::GetFullPath($configuredSourceRoot)
    }
    if ($null -eq $sourceRoot) {
        $sourceSearch = [IO.DirectoryInfo]$repositoryRoot
        for ($depth = 0; $depth -lt 8 -and $null -eq $sourceRoot -and $null -ne $sourceSearch; $depth++) {
            $candidate = Join-Path $sourceSearch.FullName 'IDEAEngineering'
            if (Test-Path -LiteralPath (Join-Path $candidate '.git') -PathType Container) {
                $sourceRoot = [IO.Path]::GetFullPath($candidate)
            }
            $sourceSearch = $sourceSearch.Parent
        }
    }
    Assert-Condition ($null -ne $sourceRoot) 'Accepted IDEAEngineering checkout was not found beside the product repository.'
    $sourceRemote = (& git -C $sourceRoot remote get-url origin 2>$null | Out-String).Trim()
    Assert-Condition ($sourceRemote -match 'github\.com/devphuclam/IDEAEngineering(?:\.git)?$') 'The selected IDEAEngineering checkout has an unexpected origin remote.'
    $acceptedCommit = '0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4'
    & git -C $sourceRoot cat-file -e ($acceptedCommit + '^{commit}')
    Assert-Condition ($LASTEXITCODE -eq 0) 'The accepted IDEAEngineering compatibility commit is not available as a commit object.'

    $manifestImport = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/manifest-import' -Method Post -Body @{
        repositoryRoot = $sourceRoot
        manifestPath = 'planning/project-management-compiler-manifest.json'
        mode = 'GIT_COMMIT'
        requestedCommit = $acceptedCommit
    }
    Assert-Condition ($manifestImport.classification -eq 'OFFICIAL_COMMIT') ("Manifest API did not classify the accepted commit as official. Actual: $($manifestImport.classification). Diagnostics: $((@($manifestImport.diagnostics) | ForEach-Object { $_.code + ':' + $_.message }) -join ' | ')")
    Assert-Condition ($manifestImport.snapshot.metadata.sourceIdentity -eq $acceptedCommit) 'Manifest API did not retain the exact accepted source commit.'
    Assert-Condition ($manifestImport.snapshot.metadata.sourceReadiness -eq 'PASS') 'Manifest API did not retain source readiness PASS.'
    Assert-Condition ($manifestImport.snapshot.metadata.validationResult -eq 'PASS_WITH_WARNINGS') 'Manifest API did not retain PASS_WITH_WARNINGS validation.'
    Assert-Condition ($manifestImport.snapshot.project.phases.Count -eq 6 -and $manifestImport.snapshot.project.workPackages.Count -eq 35 -and $manifestImport.snapshot.project.deliveryCards.Count -eq 53 -and $manifestImport.snapshot.project.milestones.Count -eq 7) 'Manifest API did not preserve exact planning totals.'
    Assert-Condition ($manifestImport.snapshot.project.baseline.plannedEffortHours -eq 512 -and $manifestImport.snapshot.project.baseline.reserveHours -eq 88 -and $manifestImport.snapshot.project.baseline.capacityHours -eq 600) 'Manifest API did not preserve exact baseline totals.'
    $manifestP01 = @($manifestImport.snapshot.sourceExecution.records | Where-Object { $_.entity.id -eq 'P01' })[0]
    Assert-Condition ($manifestP01.recordingState -eq 'RECORDED' -and $manifestP01.executionState -eq 'IN_PROGRESS' -and $manifestP01.resultState -eq 'NOT_APPLICABLE') 'Manifest API did not preserve P01 execution truth.'
    Assert-Condition ($null -eq $manifestP01.actualEffortHours -and $null -eq $manifestP01.remainingEffortHours) 'Manifest API must not invent P01 effort.'
    Assert-Condition (@($manifestImport.snapshot.sourceExecution.records | Where-Object { $_.recordingState -eq 'NOT_RECORDED' }).Count -eq 52) 'Manifest API must retain 52 NOT_RECORDED cards.'
    Assert-Condition ($manifestImport.snapshot.project.managementEvidence.discoveryState -eq 'KNOWN' -and $manifestImport.snapshot.project.managementEvidence.incrementId -eq 'IE-INC-READY-001') 'Manifest API must retain the declared readiness register as independent management evidence.'
    Assert-Condition (@($manifestImport.snapshot.diagnostics | Where-Object { $_.code -eq 'PMC-FIXTURE-001' }).Count -eq 0) 'Fixture catalogue oracle reported a mismatch for the accepted commit.'
    $manifestJson = $manifestImport | ConvertTo-Json -Depth 60 -Compress
    Assert-Condition (-not $manifestJson.Contains($sourceRoot, [StringComparison]::OrdinalIgnoreCase)) 'Manifest response must not expose the local source root.'
    Assert-Condition (-not ($manifestJson -match '"content"\s*:')) 'Manifest response must not expose raw source bodies.'

    $officialSnapshot = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/manifest-import/official' -Method Get
    Assert-Condition ($officialSnapshot.metadata.snapshotId -eq $manifestImport.snapshot.metadata.snapshotId) 'Official snapshot inspection returned a different snapshot.'
    $officialExport = Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/manifest-import/exports/official.json' -TimeoutSec 30
    $officialExportText = [string]$officialExport.Content
    Assert-Condition ([string]$officialExport.Headers['Content-Disposition'] -match 'manifest-official\.json') 'Official manifest export must use an authority-specific filename.'
    Assert-Condition ($officialExportText.Contains('"schema":"2.0"', [StringComparison]::Ordinal)) 'Official manifest export must use canonical schema 2.0.'
    Assert-Condition (-not $officialExportText.Contains($sourceRoot, [StringComparison]::OrdinalIgnoreCase)) 'Official manifest export must not expose the local source root.'
    Assert-Condition (-not ($officialExportText -match '"content"\s*:')) 'Official manifest export must not expose raw source bodies.'

    $failedManifestImport = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/manifest-import' -Method Post -Body @{
        repositoryRoot = $sourceRoot
        manifestPath = 'planning/project-management-compiler-manifest.json'
        mode = 'GIT_COMMIT'
        requestedCommit = ('0' * 40)
    }
    Assert-Condition ($failedManifestImport.classification -eq 'FAILED' -and $null -eq $failedManifestImport.snapshot) 'An invalid source commit must fail without producing a snapshot.'
    $officialAfterFailure = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/manifest-import/official' -Method Get
    Assert-Condition ($officialAfterFailure.metadata.snapshotId -eq $manifestImport.snapshot.metadata.snapshotId) 'A failed candidate must retain the last valid official snapshot.'

    $proposal = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/proposals' -Method Post -Body @{
        targetKind = 'DeliveryCard'
        targetId = 'P01'
        proposedChanges = @{ executionState = 'COMPLETED'; actualFinish = '2026-09-20' }
        requestedLifecycle = 'READY_FOR_REVIEW'
    }
    Assert-Condition ($proposal.lifecycle -eq 'DRAFT') 'An incomplete completion proposal must remain DRAFT.'
    Assert-Condition (@($proposal.diagnostics | Where-Object { $_.code -eq 'PMC-PROPOSAL-002' }).Count -eq 1) 'Proposal completion rules must explain missing evidence.'
    $proposalPreview = Invoke-JsonApi -Uri ('http://127.0.0.1:5050/api/proposals/{0}/preview' -f $proposal.id) -Method Post
    Assert-Condition (-not $proposalPreview.isAuthoritative -and $proposalPreview.isEstimated) 'Proposal preview must be visibly estimated and non-authoritative.'
    $proposalExport = Invoke-WebRequest -Uri ('http://127.0.0.1:5050/api/proposals/{0}/export' -f $proposal.id) -TimeoutSec 30
    $proposalExportText = [string]$proposalExport.Content
    $proposalExportPayload = $proposalExportText | ConvertFrom-Json
    Assert-Condition ($proposalExportPayload.proposalOnly -and $proposalExportPayload.authority -eq 'LOCAL_PROPOSAL') 'Proposal export must retain local proposal authority.'
    Assert-Condition (-not $proposalExportText.Contains($sourceRoot, [StringComparison]::OrdinalIgnoreCase)) 'Proposal export must not expose the local source root.'
    $compatibilityProposal = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/execution' -Method Post -Body @{
        workItemId = 'P02'
        executionState = 'IN_PROGRESS'
        actualStart = '2026-09-19'
        lastUpdatedAt = '2026-09-19T10:00:00Z'
        evidenceReference = @{ relativeFile = 'planning/idea-technical-pilot-execution-register.json' }
    }
    Assert-Condition ($compatibilityProposal.proposalOnly -and -not $compatibilityProposal.authoritative) '/api/execution must be a proposal-only compatibility alias.'
    Assert-Condition (@($compatibilityProposal.proposal.evidence).Count -eq 0) '/api/execution must not fabricate controlled evidence from a legacy reference.'
    Assert-Condition (-not ($compatibilityProposal | ConvertTo-Json -Depth 30 -Compress).Contains('COMPATIBILITY_UPDATE', [StringComparison]::Ordinal)) '/api/execution must not emit the retired COMPATIBILITY_UPDATE evidence type.'
    Assert-Condition ($compatibilityProposal.proposal.proposedChanges.legacyEvidenceReference -eq 'planning/idea-technical-pilot-execution-register.json') '/api/execution must preserve a legacy evidence reference as a proposal field.'
    Assert-Condition ((Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/manifest-import/official' -Method Get).metadata.snapshotId -eq $manifestImport.snapshot.metadata.snapshotId) 'Creating and previewing proposals must not change official snapshot identity.'

    $appJs = Get-Content -LiteralPath $appJsPath -Raw
    $indexHtml = Get-Content -LiteralPath $indexHtmlPath -Raw
    $stylesCss = Get-Content -LiteralPath $stylesCssPath -Raw
    Assert-Condition (-not $appJs.Contains('innerHTML', [StringComparison]::OrdinalIgnoreCase)) 'Browser UI must not use unsafe innerHTML rendering.'
    Assert-Condition ($appJs.Contains('const rawPath = byId("management-evidence-path").value.trim();', [StringComparison]::Ordinal)) 'Frontend intake must normalize the readiness path independently from the checkbox.'
    Assert-Condition ($appJs.Contains('includeManagementEvidence: includeManagementEvidence,', [StringComparison]::Ordinal)) 'Frontend intake must send the checkbox as the authoritative enable/disable switch.'
    Assert-Condition ($appJs.Contains('managementEvidenceIncrementPath: includeManagementEvidence ? rawPath : null', [StringComparison]::Ordinal)) 'Frontend intake must ignore the readiness path when the switch is disabled.'
    Assert-Condition (-not $appJs.Contains('includeManagementEvidence: includeManagementEvidence || Boolean(managementEvidenceIncrementPath)', [StringComparison]::Ordinal)) 'Frontend intake must not enable readiness from path presence alone.'
    Assert-Condition ($indexHtml.Contains('When disabled, this path is ignored.', [StringComparison]::Ordinal)) 'Readiness path helper text must explain disabled-switch semantics.'
    Assert-Condition ($appJs.Contains('Readiness and gate evidence are shown separately.', [StringComparison]::Ordinal) -and $appJs.Contains('Current gate evidence is shown separately.', [StringComparison]::Ordinal)) 'Loaded readiness wording must keep baseline and gate evidence separate.'
    Assert-Condition ($appJs.Contains('Actual phase entry and gate authorization are not loaded.', [StringComparison]::Ordinal) -and $appJs.Contains('Authoritative gate evidence is not loaded.', [StringComparison]::Ordinal)) 'Unloaded readiness wording must remain truthful.'
    Assert-Condition ($appJs.Contains('renderManagementControl', [StringComparison]::Ordinal) -and $appJs.Contains('management-control', [StringComparison]::Ordinal)) 'Browser UI must expose the bounded management control view.'
    Assert-Condition ($appJs.Contains('READINESS CONTEXT', [StringComparison]::Ordinal) -and $appJs.Contains('CURRENT GATE EVIDENCE', [StringComparison]::Ordinal)) 'Overview must distinguish evidence-derived readiness context and current gate evidence.'
    Assert-Condition ($appJs.Contains('renderReadinessAttentionQueue', [StringComparison]::Ordinal) -and $appJs.Contains('PARENT CONTEXT · READINESS', [StringComparison]::Ordinal)) 'Browser UI must expose grouped readiness attention and typed parent context.'
    Assert-Condition ($appJs.Contains('P01–P07 readiness', [StringComparison]::Ordinal) -and $appJs.Contains('Evidence inspector', [StringComparison]::Ordinal)) 'Browser UI must expose readiness and inspector sections.'
    Assert-Condition ($indexHtml.Contains('management-evidence-path', [StringComparison]::Ordinal) -and $indexHtml.Contains('Include repository readiness evidence', [StringComparison]::Ordinal)) 'Source intake must expose the explicit repository-readiness evidence profile.'
    Assert-Condition ($indexHtml.Contains('Required only when repository readiness evidence is enabled', [StringComparison]::Ordinal)) 'Source intake must explain that the readiness path is required when enabled.'
    Assert-Condition ($appJs.Contains('MANAGEMENT_EVIDENCE_PATH_REQUIRED', [StringComparison]::Ordinal) -or $appJs.Contains('Select the readiness increment path', [StringComparison]::Ordinal)) 'Browser intake must expose a clear missing-readiness-path validation.'
    Assert-Condition ($indexHtml.Contains('Readiness control', [StringComparison]::Ordinal)) 'Browser UI must expose the readiness control tab.'
    Assert-Condition ($appJs.Contains('gantt-timeline', [StringComparison]::Ordinal)) 'Gantt renderer must expose a split timeline surface.'
    Assert-Condition ($appJs.Contains('gantt-as-of-marker', [StringComparison]::Ordinal)) 'Gantt renderer must expose an explicit as-of marker.'
    Assert-Condition ($appJs.Contains('gantt-plan-bar', [StringComparison]::Ordinal)) 'Gantt renderer must render immutable PLAN bars.'
    Assert-Condition ($appJs.Contains('gantt-actual-bar', [StringComparison]::Ordinal)) 'Gantt renderer must render ACTUAL bars.'
    Assert-Condition ($appJs.Contains('gantt-alert-marker', [StringComparison]::Ordinal)) 'Gantt renderer must render ALERT markers.'
    Assert-Condition ($appJs.Contains('gantt-milestone', [StringComparison]::Ordinal)) 'Gantt renderer must render milestone markers.'
    Assert-Condition ($appJs.Contains('data-gantt-column', [StringComparison]::Ordinal) -and $appJs.Contains('createMenu("Columns", "gantt-columns-menu")', [StringComparison]::Ordinal)) 'Gantt must expose optional metadata-column controls in the compact Columns menu.'
    Assert-Condition ($appJs.Contains('Visible in the task list', [StringComparison]::Ordinal) -and -not $appJs.Contains('toolbar.appendChild(columnOptions)', [StringComparison]::Ordinal)) 'Gantt column checkboxes must not consume an always-visible toolbar row.'
    Assert-Condition ($appJs.Contains('ganttTaskColumnDefinition', [StringComparison]::Ordinal) -and $appJs.Contains('--gantt-task-columns', [StringComparison]::Ordinal)) 'Gantt task and timeline panes must share the active metadata-column layout.'
    Assert-Condition ($appJs.Contains('Select a task to inspect its direct links', [StringComparison]::Ordinal) -and $appJs.Contains('predecessor → successor', [StringComparison]::Ordinal)) 'Gantt must explain the selection-first dependency direction in the visible controls.'
    Assert-Condition ($appJs.Contains('if (!state.gantt.showDependencies || !selectedRowKey) return svg;', [StringComparison]::Ordinal) -and $appJs.Contains('state.gantt.showDependencies = true;', [StringComparison]::Ordinal)) 'Selecting a row must reveal only its focused direct dependency connectors.'
    Assert-Condition ($appJs.Contains('path.appendChild(title)', [StringComparison]::Ordinal) -and $appJs.Contains('DEPENDENCY IMPACT', [StringComparison]::Ordinal) -and $appJs.Contains('Blocks', [StringComparison]::Ordinal)) 'Gantt dependency impact must expose readable endpoint and downstream labels.'
    Assert-Condition ($appJs.Contains('Show dependencies', [StringComparison]::Ordinal)) 'Gantt toolbar must expose dependency visibility.'
    Assert-Condition ($appJs.Contains('Expand all', [StringComparison]::Ordinal)) 'Gantt toolbar must expose expand-all.'
    Assert-Condition ($appJs.Contains('Collapse all', [StringComparison]::Ordinal)) 'Gantt toolbar must expose collapse-all.'
    Assert-Condition ($appJs.Contains('fit-project', [StringComparison]::Ordinal)) 'Gantt toolbar must expose fit-project.'
    Assert-Condition ($appJs.Contains('critical-only', [StringComparison]::Ordinal)) 'Gantt filters must expose critical-only.'
    Assert-Condition ($appJs.Contains('late-start', [StringComparison]::Ordinal)) 'Gantt filters must expose late-start work.'
    Assert-Condition ($appJs.Contains('START_DELAY', [StringComparison]::Ordinal)) 'Gantt late-start filter must use the derived START_DELAY alert.'
    Assert-Condition ($appJs.Contains('alert.message || alert.label || alert.reason', [StringComparison]::Ordinal)) 'Gantt alert markers must show the derived alert message safely.'
    Assert-Condition ($appJs.Contains('formatIsoWeek', [StringComparison]::Ordinal)) 'Week zoom must expose stable ISO week labels.'
    Assert-Condition ($appJs.Contains('startOfIsoWeek', [StringComparison]::Ordinal)) 'Week zoom ticks must align to ISO week starts.'
    Assert-Condition ($appJs.Contains('AS OF ', [StringComparison]::Ordinal)) 'Gantt as-of marker must use a clear uppercase analysis label.'
    Assert-Condition ($appJs.Contains('gantt-critical-bar', [StringComparison]::Ordinal)) 'Critical-path mode must provide a distinct bar treatment.'
    Assert-Condition ($appJs.Contains('recordedPercent: true', [StringComparison]::Ordinal) -and $appJs.Contains('gantt-column-recorded-percent', [StringComparison]::Ordinal) -and $appJs.Contains('recordedPercentLabel', [StringComparison]::Ordinal)) 'Interactive Gantt must expose a fail-safe Recorded % column in the task list.'
    $legacyForecastLabel = $appJs.IndexOf('appendDetailField(actualFields, "Calculated " + ["fore", "cast"].join("")', [StringComparison]::Ordinal)
    Assert-Condition ($appJs.Contains('appendDetailField(actualFields, "Source forecast"', [StringComparison]::Ordinal) -and $legacyForecastLabel -lt 0) 'Gantt execution evidence must label the imported forecast as Source forecast.'
    Assert-Condition ($appJs.Contains('typeof value === "number"', [StringComparison]::Ordinal)) 'Gantt date formatting must accept its internal timestamp scale.'
    Assert-Condition ($appJs.Contains('minor = []', [StringComparison]::Ordinal)) 'Month zoom must not duplicate the month axis as its own detail axis.'
    Assert-Condition ($appJs.Contains('let minor = []', [StringComparison]::Ordinal)) 'Month zoom tick generation must allow an empty detail axis.'
    Assert-Condition ($appJs.Contains('filterActive', [StringComparison]::Ordinal) -and $appJs.Contains('!filterActive', [StringComparison]::Ordinal)) 'Active Gantt filters must reveal matching descendants through collapsed summaries.'
    Assert-Condition ($stylesCss.Contains('grid-auto-rows: 44px', [StringComparison]::Ordinal)) 'Gantt rows must stay within the approved readable management density.'
    Assert-Condition ($stylesCss.Contains('.gantt-task-header > span', [StringComparison]::Ordinal) -and $stylesCss.Contains('text-overflow: ellipsis', [StringComparison]::Ordinal)) 'Gantt metadata headers must remain inside their task-pane columns.'
    Assert-Condition ($stylesCss.Contains('gantt-hide-attention', [StringComparison]::Ordinal)) 'Gantt must be able to hide the attention column without changing the timeline.'
    Assert-Condition ($stylesCss.Contains('.gantt-timeline-row.gantt-dimmed', [StringComparison]::Ordinal) -and $stylesCss.Contains('opacity: 1', [StringComparison]::Ordinal)) 'Gantt timeline evidence must remain visible when row selection dims metadata.'
    Assert-Condition ($stylesCss.Contains('stroke-linejoin: round', [StringComparison]::Ordinal) -and $stylesCss.Contains('.gantt-mode-hint', [StringComparison]::Ordinal)) 'Gantt dependency connectors must use a readable stepped-line treatment and visible explanation.'
    Assert-Condition ($appJs.Contains('if (dimmed) taskRow.classList.add("gantt-dimmed")', [StringComparison]::Ordinal) -and -not $appJs.Contains('[taskRow, timelineRow].forEach(element => {', [StringComparison]::Ordinal)) 'Gantt row selection must not dim the timeline evidence rows.'
    Assert-Condition (-not $appJs.Contains('draggable', [StringComparison]::OrdinalIgnoreCase)) 'PLAN bars must not be draggable.'
    Assert-Condition (-not $indexHtml.Contains('cdn.', [StringComparison]::OrdinalIgnoreCase)) 'Gantt must not add CDN assets.'
    $ganttStart = $appJs.IndexOf('function renderGantt(view', [StringComparison]::Ordinal)
    $ganttEnd = $appJs.IndexOf('function renderKanban', [StringComparison]::Ordinal)
    Assert-Condition ($ganttStart -ge 0 -and $ganttEnd -gt $ganttStart) 'Gantt renderer source boundary must be discoverable.'
    $ganttSource = $appJs.Substring($ganttStart, $ganttEnd - $ganttStart)
    Assert-Condition (-not $ganttSource.Contains('FORECAST', [StringComparison]::OrdinalIgnoreCase)) 'Gantt must not fabricate forecast presentation.'
    Assert-Condition ($appJs.Contains('Recorded completion', [StringComparison]::Ordinal)) 'Browser UI must label completion as recorded execution evidence.'
    Assert-Condition ($appJs.Contains('getFullYear', [StringComparison]::Ordinal) -and $appJs.Contains('getMonth', [StringComparison]::Ordinal) -and $appJs.Contains('getDate', [StringComparison]::Ordinal)) 'Browser as-of default must use the browser-local calendar date.'
    Assert-Condition ($indexHtml.Contains('id="as-of-date" type="date" required', [StringComparison]::Ordinal)) 'Browser as-of date must be visibly required.'
    Assert-Condition ($appJs.Contains('/api/reopen', [StringComparison]::Ordinal)) 'Browser UI must expose canonical JSON reopen.'
    Assert-Condition ($appJs.Contains('Captured source metadata', [StringComparison]::Ordinal)) 'Browser Source / Warnings view must expose captured source metadata.'
    Assert-Condition ($appJs.Contains('/api/manifest-import', [StringComparison]::Ordinal) -and $appJs.Contains('renderManifestTrust', [StringComparison]::Ordinal)) 'Browser UI must expose the dedicated manifest import and trust surface.'
    Assert-Condition ($appJs.Contains('Source snapshot', [StringComparison]::Ordinal) -and $appJs.Contains('Snapshot ID', [StringComparison]::Ordinal) -and $appJs.Contains('sourceReadiness', [StringComparison]::Ordinal)) 'Browser UI must expose official/preview snapshot metadata.'
    Assert-Condition ($appJs.Contains('proposalOnly', [StringComparison]::Ordinal) -and $appJs.Contains('previewProposal', [StringComparison]::Ordinal) -and $appJs.Contains('non-authoritative', [StringComparison]::OrdinalIgnoreCase)) 'Browser execution editing must remain proposal-only with an explicit preview action.'
    Assert-Condition ($appJs.Contains('Recording state', [StringComparison]::Ordinal) -and $appJs.Contains('Result state', [StringComparison]::Ordinal) -and $appJs.Contains('forecastFinish', [StringComparison]::Ordinal) -and $appJs.Contains('sourceExecution', [StringComparison]::Ordinal)) 'Row inspector must expose source execution truth separately from planning and proposals.'
    Assert-Condition ($appJs.Contains('relativeFile', [StringComparison]::Ordinal) -and $appJs.Contains('documentId', [StringComparison]::Ordinal)) 'Browser source review must expose document ID and relative file metadata.'
    Assert-Condition (-not $appJs.Contains('source.Content', [StringComparison]::OrdinalIgnoreCase)) 'Browser source review must not render captured source content.'
    Assert-Condition (-not $appJs.Contains($fixture, [StringComparison]::OrdinalIgnoreCase)) 'Browser source review must not embed an absolute source path.'
    Assert-Condition ($appJs.Contains('project-control-center', [StringComparison]::Ordinal)) 'Browser UI must expose a project control center summary.'
    Assert-Condition ($appJs.Contains('Needs attention', [StringComparison]::Ordinal)) 'Browser UI must expose an actionable attention queue.'
    Assert-Condition ($appJs.Contains('NEXT BASELINE CONTROL POINT', [StringComparison]::Ordinal)) 'Browser UI must expose the next baseline control point readout.'
    Assert-Condition ($appJs.Contains('attentionOnly', [StringComparison]::Ordinal)) 'Gantt state must support a focused attention mode.'
    Assert-Condition ($appJs.Contains('Select a task to inspect its direct links', [StringComparison]::Ordinal)) 'Gantt detail flow must explain how to inspect a task.'
    Assert-Condition ($indexHtml.Contains('Project control center', [StringComparison]::OrdinalIgnoreCase)) 'Browser shell must label the project control center.'
    Assert-Condition ($stylesCss.Contains('.summary-hero', [StringComparison]::Ordinal) -and $stylesCss.Contains('.attention-queue', [StringComparison]::Ordinal)) 'Browser UI must style the control center and attention queue.'
    Assert-Condition ($stylesCss.Contains('.manifest-trust-strip', [StringComparison]::Ordinal) -and $stylesCss.Contains('.proposal-result-panel', [StringComparison]::Ordinal)) 'Browser UI must style trust metadata and proposal review surfaces.'
    Assert-Condition ($indexHtml.Contains('id="source-intake-panel"', [StringComparison]::Ordinal)) 'Loaded projects must have a collapsible source-intake panel.'
    Assert-Condition ($indexHtml.Contains('id="manifest-repository-root"', [StringComparison]::Ordinal) -and $indexHtml.Contains('id="manifest-path"', [StringComparison]::Ordinal) -and $indexHtml.Contains('id="manifest-source-commit"', [StringComparison]::Ordinal) -and $indexHtml.Contains('id="manifest-mode"', [StringComparison]::Ordinal)) 'Manifest import form must expose repository, manifest, commit and mode inputs.'
    Assert-Condition ($indexHtml.Contains('No write-back', [StringComparison]::Ordinal) -and $indexHtml.Contains('review artifact', [StringComparison]::OrdinalIgnoreCase)) 'Manifest and execution UI must state the no-write-back boundary.'
    Assert-Condition ($indexHtml.Contains('id="source-intake-toggle"', [StringComparison]::Ordinal)) 'Source-intake collapse control must be keyboard-addressable.'
    Assert-Condition ($indexHtml.Contains('data-nav-group="plan"', [StringComparison]::Ordinal) -and $indexHtml.Contains('data-nav-group="execution"', [StringComparison]::Ordinal) -and $indexHtml.Contains('data-nav-group="analysis"', [StringComparison]::Ordinal)) 'Primary navigation must group plan, execution, and analysis views.'
    Assert-Condition ($appJs.Contains('No active schedule alerts', [StringComparison]::Ordinal)) 'Project health must stay scoped to derived schedule alerts rather than whole-project health.'
    Assert-Condition ($appJs.Contains('No execution evidence', [StringComparison]::Ordinal)) 'Planning-only projects must not present missing execution evidence as zero completion.'
    Assert-Condition ($appJs.Contains('health.overall', [StringComparison]::Ordinal)) 'Execution evidence display must use the canonical health indicator rather than a Gantt-lane heuristic.'
    Assert-Condition ($appJs.Contains('Dependency CPM Finish', [StringComparison]::Ordinal)) 'CPM finish must be labeled as a dependency-only calculation in the overview.'
    Assert-Condition ($appJs.Contains('gantt-detail-drawer', [StringComparison]::Ordinal)) 'Gantt inspector must use the contextual overlay drawer.'
    Assert-Condition ($appJs.Contains('createMenu("View options", "gantt-view-menu")', [StringComparison]::Ordinal)) 'Gantt secondary views and filters must be progressively disclosed.'
    Assert-Condition ($stylesCss.Contains('.page > * { min-width: 0;', [StringComparison]::Ordinal)) 'Page children must be allowed to shrink without causing document-level horizontal overflow.'
    Assert-Condition ($stylesCss.Contains('grid-template-columns: minmax(0, 1fr) minmax(0, 1fr)', [StringComparison]::Ordinal)) 'Summary grids must use shrinkable columns at responsive widths.'
    Assert-Condition ($stylesCss.Contains('.gantt-workspace', [StringComparison]::Ordinal) -and $stylesCss.Contains('.gantt-detail-drawer', [StringComparison]::Ordinal)) 'Gantt layout must overlay a responsive contextual drawer without permanently shrinking the chart.'
    Assert-Condition ($indexHtml.Contains('class="app-header-brand"', [StringComparison]::Ordinal) -and $indexHtml.Contains('class="brand-mark"', [StringComparison]::Ordinal)) 'Browser shell must expose a compact product identity.'
    Assert-Condition ($indexHtml.Contains('id="execution-panel-toggle"', [StringComparison]::Ordinal) -and $indexHtml.Contains('id="execution-panel-body"', [StringComparison]::Ordinal)) 'Execution updates must be progressively disclosed from the shell.'
    Assert-Condition ($appJs.Contains('data-gantt-preset', [StringComparison]::Ordinal) -and $appJs.Contains('applyGanttPreset', [StringComparison]::Ordinal)) 'Gantt must expose named management view presets.'
    Assert-Condition ($appJs.Contains('Plan only', [StringComparison]::Ordinal)) 'Planning-only rows must use a readable planning state label.'
    Assert-Condition ($appJs.Contains('isDerivedPlan', [StringComparison]::Ordinal) -and $appJs.Contains('derived summary dates', [StringComparison]::Ordinal)) 'Summary rows must expose rolled-up plan dates from dated children.'
    Assert-Condition ($appJs.Contains('row.kind !== "DeliveryCard"', [StringComparison]::Ordinal) -and $appJs.Contains('resolveWorkItemVariance', [StringComparison]::Ordinal)) 'Inspector variance must be scoped to typed DeliveryCard rows.'
    Assert-Condition ($appJs.Contains('planStartOrigin', [StringComparison]::Ordinal) -and $appJs.Contains('planFinishOrigin', [StringComparison]::Ordinal)) 'Inspector must distinguish authored and derived plan boundaries.'
    Assert-Condition ($appJs.Contains('resolveAlertAnchor', [StringComparison]::Ordinal) -and $appJs.Contains('["START_DELAY", "OVERDUE", "AT_RISK", "SUSPENDED", "CANCELLED"]', [StringComparison]::Ordinal) -and $appJs.Contains('["COMPLETED_LATE", "COMPLETED_ON_TIME"]', [StringComparison]::Ordinal)) 'Alert markers must use explicit alert-specific anchor semantics.'
    Assert-Condition ($appJs.Contains('marker-end', [StringComparison]::Ordinal) -and $appJs.Contains('gantt-dependency-arrow', [StringComparison]::Ordinal)) 'Dependency connectors must expose direction arrowheads.'
    Assert-Condition ($appJs.Contains('["critical-path", "Critical path"]', [StringComparison]::Ordinal)) 'Gantt must expose a named Critical path preset.'
    Assert-Condition ($appJs.Contains('WITH_EVIDENCE', [StringComparison]::Ordinal) -and $appJs.Contains('hasExecutionEvidence', [StringComparison]::Ordinal)) 'Execution preset must show explicit execution evidence, not only in-progress state.'
    Assert-Condition ($appJs.Contains('visibleRows.length', [StringComparison]::Ordinal) -and $appJs.Contains('presetLabel', [StringComparison]::Ordinal)) 'Gantt must announce visible row count and active view state.'
    Assert-Condition ($appJs.Contains('Calculated CPM', [StringComparison]::Ordinal) -and $appJs.Contains('Analysis-layer dates do not replace the authored baseline.', [StringComparison]::Ordinal)) 'Inspector must label CPM values as calculated dependency analysis.'
    Assert-Condition ($appJs.Contains('SOURCE EVIDENCE', [StringComparison]::Ordinal) -and $appJs.Contains('sourceReferences', [StringComparison]::Ordinal)) 'Inspector must expose safe item-level source evidence.'
    Assert-Condition ($appJs.Contains('through AS OF', [StringComparison]::Ordinal)) 'Open actual bars must explain the as-of endpoint as observation time.'
    Assert-Condition ($appJs.Contains('gantt-actual-finish-marker', [StringComparison]::Ordinal) -and $appJs.Contains('ACTUAL finish recorded', [StringComparison]::Ordinal)) 'Finish-only actual evidence must remain visible without fabricating a start date.'
    Assert-Condition ($appJs.Contains('node("button", null, "gantt-alert-marker"', [StringComparison]::Ordinal) -and $appJs.Contains('marker.type = "button"', [StringComparison]::Ordinal)) 'Alert markers must be keyboard-operable controls.'
    Assert-Condition ($appJs.Contains('delivery-card count', [StringComparison]::Ordinal)) 'Completion language must remain explicitly card-count based.'
    $summaryStart = $appJs.IndexOf('function renderSummary', [StringComparison]::Ordinal)
    $summaryEnd = $appJs.IndexOf('function renderTable', [StringComparison]::Ordinal)
    Assert-Condition ($summaryStart -ge 0 -and $summaryEnd -gt $summaryStart -and -not $appJs.Substring($summaryStart, $summaryEnd - $summaryStart).Contains('addEventListener', [StringComparison]::Ordinal)) 'Summary rendering must not accumulate click listeners.'
    Assert-Condition ($appJs.Contains('zoom: "week"', [StringComparison]::Ordinal)) 'Gantt must default to the approved readable weekly planning scale.'
    Assert-Condition ($appJs.Contains('gantt-week-start', [StringComparison]::Ordinal) -and $appJs.Contains('gantt-month-start', [StringComparison]::Ordinal)) 'Gantt timeline must identify weekly and monthly boundaries.'
    Assert-Condition ($stylesCss.Contains('.gantt-zoom-week .gantt-day-grid:not(.gantt-week-start)', [StringComparison]::Ordinal)) 'Weekly Gantt styling must suppress dense daily grid lines.'
    Assert-Condition ($appJs.Contains('ManagementPresentationRow', [StringComparison]::Ordinal)) 'Management UI must expose a narrow presentation projection.'
    Assert-Condition ($appJs.Contains('normalizeDisplayTitle', [StringComparison]::Ordinal)) 'Management UI must normalize redundant source prefixes for display only.'
    Assert-Condition ($appJs.Contains('structureMode', [StringComparison]::Ordinal) -and $appJs.Contains('Structure', [StringComparison]::Ordinal)) 'Gantt must expose an explicit Structure mode.'
    Assert-Condition ($appJs.Contains('Needs attention', [StringComparison]::Ordinal) -and $appJs.Contains('preset === "attention"', [StringComparison]::Ordinal)) 'Gantt must expose a first-class Needs attention preset.'
    Assert-Condition ($appJs.Contains('SCHEDULED PHASE', [StringComparison]::Ordinal) -and $appJs.Contains('NEXT BASELINE CONTROL POINT', [StringComparison]::Ordinal)) 'Overview must expose baseline-derived phase and control point context.'
    Assert-Condition ($appJs.Contains('No execution evidence', [StringComparison]::Ordinal)) 'Planning-only overview must name the missing execution evidence plainly.'
    Assert-Condition ($appJs.Contains('Dependency CPM Finish', [StringComparison]::Ordinal)) 'Schedule overview must label dependency CPM separately from resource constraints.'
    Assert-Condition ($appJs.Contains('primaryOwner', [StringComparison]::Ordinal) -and $appJs.Contains('sourceName', [StringComparison]::Ordinal)) 'Management rows must separate primary owner and source title from the canonical projection.'
    Assert-Condition ($appJs.Contains('WorkPackage', [StringComparison]::Ordinal) -and $appJs.Contains('isStructureMode', [StringComparison]::Ordinal)) 'Default schedule presentation must be able to quiet WorkPackage rows without removing them from the model.'
    Assert-Condition ($appJs.Contains('SCHEDULED PHASE', [StringComparison]::Ordinal) -and -not $appJs.Contains('CURRENT PHASE', [StringComparison]::Ordinal)) 'Baseline-selected phase must be labeled as scheduled, not actual/current.'
    Assert-Condition ($appJs.Contains('NEXT BASELINE CONTROL POINT', [StringComparison]::Ordinal) -and -not $appJs.Contains('NEXT CONTROL POINT', [StringComparison]::Ordinal)) 'Baseline milestone chronology must be labeled as a baseline control point.'
    Assert-Condition ($appJs.Contains('No active schedule alerts', [StringComparison]::Ordinal) -and -not $appJs.Contains('No active alerts', [StringComparison]::Ordinal)) 'Health and empty attention copy must stay scoped to derived schedule alerts.'
    Assert-Condition ($appJs.Contains('No schedule exceptions are derived from the loaded evidence.', [StringComparison]::Ordinal)) 'Empty attention copy must not claim whole-project health.'
    Assert-Condition ($appJs.Contains('Repository readiness: Loaded', [StringComparison]::Ordinal) -and $appJs.Contains('Not configured', [StringComparison]::Ordinal) -and $appJs.Contains('Could not resolve increment', [StringComparison]::Ordinal)) 'Overview must disclose state-dependent repository-readiness scope.'
    Assert-Condition (-not $appJs.Contains('Readiness/gate execution records are not part of the current MVP1 intake.', [StringComparison]::Ordinal)) 'Overview must not use obsolete MVP1 readiness-intake wording.'
    Assert-Condition ($appJs.Contains('Waiting for', [StringComparison]::Ordinal) -and $appJs.Contains('Pending action', [StringComparison]::Ordinal) -and $appJs.Contains('Blocker', [StringComparison]::Ordinal)) 'Readiness inspector must separate owner, waiting, pending and blocker meaning.'
    Assert-Condition (-not $appJs.Contains('Process manager', [StringComparison]::Ordinal) -and -not $appJs.Contains('Product Decision Authority', [StringComparison]::Ordinal)) 'Role presentation must not invent formal organization-wide titles.'
    $controlPointStart = $appJs.IndexOf('function controlPointState', [StringComparison]::Ordinal)
    $controlPointEnd = $appJs.IndexOf('function renderControlStrip', [StringComparison]::Ordinal)
    $controlPointSource = if ($controlPointStart -ge 0 -and $controlPointEnd -gt $controlPointStart) { $appJs.Substring($controlPointStart, $controlPointEnd - $controlPointStart) } else { '' }
    Assert-Condition ($controlPointSource.Contains('Planned · result not evaluated', [StringComparison]::Ordinal) -and -not $controlPointSource.Contains('stateLabel(', [StringComparison]::Ordinal)) 'Baseline control points must retain an unevaluated result state without execution evidence.'
    Assert-Condition (-not $appJs.Contains('|| milestones[0] || null', [StringComparison]::Ordinal)) 'Baseline chronology must not reuse the earliest milestone after the final control point.'
    Assert-Condition ($appJs.Contains('No upcoming baseline control point', [StringComparison]::Ordinal)) 'Post-baseline chronology must explain when no future control point remains.'
    $presentationStart = $appJs.IndexOf('function createGanttPresentation', [StringComparison]::Ordinal)
    $presentationEnd = $appJs.IndexOf('function formatDate', [StringComparison]::Ordinal)
    Assert-Condition ($presentationStart -ge 0 -and $presentationEnd -gt $presentationStart) 'Gantt presentation projection source boundary must be discoverable.'
    $presentationSource = $appJs.Substring($presentationStart, $presentationEnd - $presentationStart)
    Assert-Condition ($presentationSource.Contains('row.kind !== "WorkPackage"', [StringComparison]::Ordinal) -and $presentationSource.Contains('isStructureMode()', [StringComparison]::Ordinal)) 'Default Gantt behavior must quiet WorkPackage rows while Structure mode retains them.'
    $timelineBackgroundStart = $appJs.IndexOf('function renderTimelineBackground', [StringComparison]::Ordinal)
    $timelineBackgroundEnd = $appJs.IndexOf('function createBar', [StringComparison]::Ordinal)
    Assert-Condition ($timelineBackgroundStart -ge 0 -and $timelineBackgroundEnd -gt $timelineBackgroundStart) 'Daily timeline background source boundary must be discoverable.'
    $timelineBackgroundSource = $appJs.Substring($timelineBackgroundStart, $timelineBackgroundEnd - $timelineBackgroundStart)
    Assert-Condition ($timelineBackgroundSource.Contains('gantt-day-grid', [StringComparison]::Ordinal) -and $timelineBackgroundSource.Contains('gantt-week-start', [StringComparison]::Ordinal) -and $timelineBackgroundSource.Contains('dateAt(cursor, 1)', [StringComparison]::Ordinal)) 'Gantt background must retain date bands while marking calmer weekly boundaries.'
    $dashboardStart = $appJs.IndexOf('function renderDashboard', [StringComparison]::Ordinal)
    $dashboardEnd = $appJs.IndexOf('function appendTree', [StringComparison]::Ordinal)
    Assert-Condition ($dashboardStart -ge 0 -and $dashboardEnd -gt $dashboardStart) 'Dashboard renderer source boundary must be discoverable.'
    $dashboardSource = $appJs.Substring($dashboardStart, $dashboardEnd - $dashboardStart)
    Assert-Condition (-not $dashboardSource.Contains('renderControlStrip(summary, context)', [StringComparison]::Ordinal) -and -not $dashboardSource.Contains('renderAttentionQueue(summary, false)', [StringComparison]::Ordinal)) 'Dashboard must not repeat the Level-1 control strip and attention queue.'
    Assert-Condition ($stylesCss.Contains('background-image: none', [StringComparison]::Ordinal)) 'Management workspace surfaces must not rely on decorative gradients.'
    Assert-Condition ($stylesCss.Contains('.gantt-preset', [StringComparison]::Ordinal) -and $stylesCss.Contains('.execution-panel-body', [StringComparison]::Ordinal)) 'Management workspace must style presets and the progressive execution updater.'
    Assert-Condition ($stylesCss.Contains('.app-header { flex-direction: column;', [StringComparison]::Ordinal)) 'Mobile workspace header must stack identity and actions.'
    $program = Get-Content -LiteralPath $programPath -Raw
    Assert-Condition ($program.Contains('http://127.0.0.1:5050', [StringComparison]::Ordinal)) 'Program must bind to the loopback address.'
    Assert-Condition (-not $program.Contains('0.0.0.0', [StringComparison]::Ordinal)) 'Program must not bind to all interfaces.'

    [PSCustomObject]@{
        Health = $health.status
        ProjectId = $summary.project.id
        Cards = $summary.views.dashboard.totalCards
        ManifestClassification = $manifestImport.classification
        OfficialSnapshot = $manifestImport.snapshot.metadata.snapshotId
        ProposalLifecycle = $proposal.lifecycle
        ReopenOverdue = $reopened.analysis.executionStatus.overdue
        ReopenAtRisk = $reopened.analysis.executionStatus.atRisk
        JsonBytes = $jsonText.Length
        XlsxBytes = (Get-Item -LiteralPath $temporaryXlsx).Length
        SheetCount = $sheetNames.Count
        SecurityChecks = 'PASS'
    } | ConvertTo-Json -Compress
}
finally {
    if ($archive) {
        $archive.Dispose()
    }
    if ($fileStream) {
        $fileStream.Dispose()
    }
    if ($temporaryXlsx -and (Test-Path -LiteralPath $temporaryXlsx)) {
        Remove-Item -LiteralPath $temporaryXlsx -Force
    }
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}

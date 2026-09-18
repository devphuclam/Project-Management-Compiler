[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '..'))
$appDll = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\ProjectManagementCompiler\bin\Debug\net10.0\ProjectManagementCompiler.dll'))
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

$process = Start-Process -FilePath 'dotnet' -ArgumentList @('exec', ('"{0}"' -f $appDll)) -WorkingDirectory $repositoryRoot -WindowStyle Hidden -PassThru
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

    $execution = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/execution' -Method Post -Body @{
        workItemId = 'P04'
        executionState = 'IN_PROGRESS'
        actualStart = '2026-09-25'
        lastUpdatedAt = '2026-09-28T10:00:00Z'
    }
    Assert-Condition ($execution.analysis.executionStatus.overdue -eq 1) 'API execution update did not recalculate overdue status.'
    $executionP04 = @($execution.views.gantt.items | Where-Object { $_.workItemId -eq 'P04' })[0]
    $executionP04Plan = @($executionP04.lanes | Where-Object { $_.lane -eq 'PLAN' })[0]
    Assert-Condition ($executionP04Plan.start -eq $ganttP04Plan[0].start -and $executionP04Plan.finish -eq $ganttP04Plan[0].finish) 'Execution update must not mutate the P04 PLAN lane.'
    Assert-Condition ($executionP04.hasExecutionEvidence) 'Execution update must mark P04 as carrying explicit execution evidence.'
    Assert-Condition (@($executionP04.lanes | Where-Object { $_.lane -eq 'ACTUAL' -and $_.start -eq '2026-09-25' }).Count -eq 1) 'API execution update did not produce the P04 ACTUAL lane.'
    Assert-Condition (@($executionP04.lanes | Where-Object { $_.lane -eq 'ACTUAL' -and $null -eq $_.finish -and $_.isOpenEnded }).Count -eq 1) 'In-progress ACTUAL evidence must remain open-ended while the browser displays it through as-of.'
    Assert-Condition (@($execution.analysis.alerts | Where-Object { $_.workItemId -eq 'P04' -and $_.alertCode -eq 'OVERDUE' }).Count -eq 1) 'API execution update did not produce the P04 OVERDUE alert.'

    $completedFinishOnly = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/execution' -Method Post -Body @{
        workItemId = 'P05'
        executionState = 'COMPLETED'
        actualFinish = '2026-09-28'
        lastUpdatedAt = '2026-09-28T10:00:30Z'
    }
    Assert-Condition ($completedFinishOnly.analysis.executionStatus.completed -eq 1) 'Completed execution with finish-only evidence must count as completed.'
    $completedP05 = @($completedFinishOnly.views.gantt.items | Where-Object { $_.workItemId -eq 'P05' })[0]
    Assert-Condition (@($completedP05.lanes | Where-Object { $_.lane -eq 'ACTUAL' -and $null -eq $_.start -and $_.finish -eq '2026-09-28' }).Count -eq 1) 'Finish-only completion must retain its ACTUAL finish without fabricating an actual start.'
    Assert-Condition (@($completedFinishOnly.analysis.healthIndicators | Where-Object { $_.id -eq 'health.overall' -and $_.status -eq 'KNOWN' }).Count -eq 1) 'Finish-only execution evidence must make canonical health known.'

    $atRisk = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/execution' -Method Post -Body @{
        workItemId = 'P06'
        executionState = 'NOT_STARTED'
        lastUpdatedAt = '2026-09-28T10:01:00Z'
    }
    Assert-Condition ($atRisk.analysis.executionStatus.atRisk -eq 1) 'API execution flow did not recalculate dependent AT_RISK status.'
    $p06Risk = @($atRisk.analysis.alerts | Where-Object { $_.workItemId -eq 'P06' -and $_.alertCode -eq 'AT_RISK' })
    Assert-Condition ($p06Risk.Count -eq 1 -and $p06Risk[0].reasonWorkItemIds -contains 'P04') 'Dependent P06 AT_RISK alert did not name delayed P04.'

    $jsonResponse = Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/exports/project.json' -TimeoutSec 30
    $jsonText = [string] $jsonResponse.Content
    $contentDisposition = [string] $jsonResponse.Headers['Content-Disposition']
    Assert-Condition ($contentDisposition -match '_project\.json') 'JSON export filename must follow the <ProjectName>_project.json contract.'
    Assert-Condition ($contentDisposition -notmatch 'filename="project\.json"') 'JSON export must not use the generic project.json filename.'
    Assert-Condition (-not ($jsonText -match '"content"\s*:')) 'Persisted JSON must not leak captured source content.'
    Assert-Condition (-not $jsonText.Contains($fixture, [StringComparison]::OrdinalIgnoreCase)) 'Persisted JSON must not leak an absolute source path.'
    Assert-Condition ($jsonText.Contains('"executionOverlay"', [StringComparison]::Ordinal)) 'Persisted JSON must include the execution overlay.'

    $reopened = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/reopen' -Method Post -Body @{
        json = $jsonText
        asOfDate = '2026-09-28'
    }
    Assert-Condition ($reopened.baseline.id -eq $summary.baseline.id) 'API reopen changed the baseline identity.'
    Assert-Condition ($reopened.analysis.executionStatus.overdue -eq 1) 'API reopen did not recalculate the overdue alert.'
    Assert-Condition ($reopened.analysis.executionStatus.atRisk -eq 1) 'API reopen did not recalculate the dependent at-risk alert.'

    $temporaryFile = New-TemporaryFile
    $temporaryXlsx = $temporaryFile.FullName
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
            'xl/worksheets/sheet6.xml'
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
        Assert-Condition (([string]::Join(',', $sheetNames)) -eq '01_TASKS,02_ASSIGNMENTS,03_CHILDREN_MILESTONES,04_DEPENDENCIES,05_PROJECT_INFO,06_IMPORT_WARNINGS') 'XLSX worksheet names are not the exact six-sheet contract.'

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
    }
    finally {
        $archive.Dispose()
        $fileStream.Dispose()
    }

    $appJs = Get-Content -LiteralPath $appJsPath -Raw
    $indexHtml = Get-Content -LiteralPath $indexHtmlPath -Raw
    $stylesCss = Get-Content -LiteralPath $stylesCssPath -Raw
    Assert-Condition (-not $appJs.Contains('innerHTML', [StringComparison]::OrdinalIgnoreCase)) 'Browser UI must not use unsafe innerHTML rendering.'
    Assert-Condition ($appJs.Contains('gantt-timeline', [StringComparison]::Ordinal)) 'Gantt renderer must expose a split timeline surface.'
    Assert-Condition ($appJs.Contains('gantt-as-of-marker', [StringComparison]::Ordinal)) 'Gantt renderer must expose an explicit as-of marker.'
    Assert-Condition ($appJs.Contains('gantt-plan-bar', [StringComparison]::Ordinal)) 'Gantt renderer must render immutable PLAN bars.'
    Assert-Condition ($appJs.Contains('gantt-actual-bar', [StringComparison]::Ordinal)) 'Gantt renderer must render ACTUAL bars.'
    Assert-Condition ($appJs.Contains('gantt-alert-marker', [StringComparison]::Ordinal)) 'Gantt renderer must render ALERT markers.'
    Assert-Condition ($appJs.Contains('gantt-milestone', [StringComparison]::Ordinal)) 'Gantt renderer must render milestone markers.'
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
    Assert-Condition ($appJs.Contains('typeof value === "number"', [StringComparison]::Ordinal)) 'Gantt date formatting must accept its internal timestamp scale.'
    Assert-Condition ($appJs.Contains('minor = []', [StringComparison]::Ordinal)) 'Month zoom must not duplicate the month axis as its own detail axis.'
    Assert-Condition ($appJs.Contains('let minor = []', [StringComparison]::Ordinal)) 'Month zoom tick generation must allow an empty detail axis.'
    Assert-Condition ($appJs.Contains('filterActive', [StringComparison]::Ordinal) -and $appJs.Contains('!filterActive', [StringComparison]::Ordinal)) 'Active Gantt filters must reveal matching descendants through collapsed summaries.'
    Assert-Condition ($stylesCss.Contains('grid-auto-rows: 40px', [StringComparison]::Ordinal)) 'Gantt rows must stay within the target management density.'
    Assert-Condition (-not $appJs.Contains('draggable', [StringComparison]::OrdinalIgnoreCase)) 'PLAN bars must not be draggable.'
    Assert-Condition (-not $indexHtml.Contains('cdn.', [StringComparison]::OrdinalIgnoreCase)) 'Gantt must not add CDN assets.'
    $ganttStart = $appJs.IndexOf('function renderGantt', [StringComparison]::Ordinal)
    $ganttEnd = $appJs.IndexOf('function renderKanban', [StringComparison]::Ordinal)
    Assert-Condition ($ganttStart -ge 0 -and $ganttEnd -gt $ganttStart) 'Gantt renderer source boundary must be discoverable.'
    $ganttSource = $appJs.Substring($ganttStart, $ganttEnd - $ganttStart)
    Assert-Condition (-not $ganttSource.Contains('FORECAST', [StringComparison]::OrdinalIgnoreCase)) 'Gantt must not fabricate forecast presentation.'
    Assert-Condition ($appJs.Contains('Recorded completion', [StringComparison]::Ordinal)) 'Browser UI must label completion as recorded execution evidence.'
    Assert-Condition ($appJs.Contains('getFullYear', [StringComparison]::Ordinal) -and $appJs.Contains('getMonth', [StringComparison]::Ordinal) -and $appJs.Contains('getDate', [StringComparison]::Ordinal)) 'Browser as-of default must use the browser-local calendar date.'
    Assert-Condition ($indexHtml.Contains('id="as-of-date" type="date" required', [StringComparison]::Ordinal)) 'Browser as-of date must be visibly required.'
    Assert-Condition ($appJs.Contains('/api/reopen', [StringComparison]::Ordinal)) 'Browser UI must expose canonical JSON reopen.'
    Assert-Condition ($appJs.Contains('Captured source metadata', [StringComparison]::Ordinal)) 'Browser Source / Warnings view must expose captured source metadata.'
    Assert-Condition ($appJs.Contains('relativeFile', [StringComparison]::Ordinal) -and $appJs.Contains('documentId', [StringComparison]::Ordinal)) 'Browser source review must expose document ID and relative file metadata.'
    Assert-Condition (-not $appJs.Contains('source.Content', [StringComparison]::OrdinalIgnoreCase)) 'Browser source review must not render captured source content.'
    Assert-Condition (-not $appJs.Contains($fixture, [StringComparison]::OrdinalIgnoreCase)) 'Browser source review must not embed an absolute source path.'
    Assert-Condition ($appJs.Contains('project-control-center', [StringComparison]::Ordinal)) 'Browser UI must expose a project control center summary.'
    Assert-Condition ($appJs.Contains('Needs attention', [StringComparison]::Ordinal)) 'Browser UI must expose an actionable attention queue.'
    Assert-Condition ($appJs.Contains('NEXT BASELINE CONTROL POINT', [StringComparison]::Ordinal)) 'Browser UI must expose the next baseline control point readout.'
    Assert-Condition ($appJs.Contains('attentionOnly', [StringComparison]::Ordinal)) 'Gantt state must support a focused attention mode.'
    Assert-Condition ($appJs.Contains('Select a row to inspect', [StringComparison]::Ordinal)) 'Gantt detail flow must explain how to inspect a row.'
    Assert-Condition ($indexHtml.Contains('Project control center', [StringComparison]::OrdinalIgnoreCase)) 'Browser shell must label the project control center.'
    Assert-Condition ($stylesCss.Contains('.summary-hero', [StringComparison]::Ordinal) -and $stylesCss.Contains('.attention-queue', [StringComparison]::Ordinal)) 'Browser UI must style the control center and attention queue.'
    Assert-Condition ($indexHtml.Contains('id="source-intake-panel"', [StringComparison]::Ordinal)) 'Loaded projects must have a collapsible source-intake panel.'
    Assert-Condition ($indexHtml.Contains('id="source-intake-toggle"', [StringComparison]::Ordinal)) 'Source-intake collapse control must be keyboard-addressable.'
    Assert-Condition ($indexHtml.Contains('data-nav-group="plan"', [StringComparison]::Ordinal) -and $indexHtml.Contains('data-nav-group="execution"', [StringComparison]::Ordinal) -and $indexHtml.Contains('data-nav-group="analysis"', [StringComparison]::Ordinal)) 'Primary navigation must group plan, execution, and analysis views.'
    Assert-Condition ($appJs.Contains('No active schedule alerts', [StringComparison]::Ordinal)) 'Project health must stay scoped to derived schedule alerts rather than whole-project health.'
    Assert-Condition ($appJs.Contains('No execution evidence', [StringComparison]::Ordinal)) 'Planning-only projects must not present missing execution evidence as zero completion.'
    Assert-Condition ($appJs.Contains('health.overall', [StringComparison]::Ordinal)) 'Execution evidence display must use the canonical health indicator rather than a Gantt-lane heuristic.'
    Assert-Condition ($appJs.Contains('Dependency CPM Finish', [StringComparison]::Ordinal)) 'CPM finish must be labeled as a dependency-only calculation in the overview.'
    Assert-Condition ($appJs.Contains('gantt-inspector-column', [StringComparison]::Ordinal)) 'Gantt inspector must have a dedicated adjacent layout column.'
    Assert-Condition ($appJs.Contains('Advanced filters', [StringComparison]::Ordinal)) 'Gantt advanced filters must be progressively disclosed.'
    Assert-Condition ($stylesCss.Contains('.page > * { min-width: 0;', [StringComparison]::Ordinal)) 'Page children must be allowed to shrink without causing document-level horizontal overflow.'
    Assert-Condition ($stylesCss.Contains('grid-template-columns: minmax(0, 1fr) minmax(0, 1fr)', [StringComparison]::Ordinal)) 'Summary grids must use shrinkable columns at responsive widths.'
    Assert-Condition ($stylesCss.Contains('.gantt-workspace', [StringComparison]::Ordinal) -and $stylesCss.Contains('.gantt-inspector-column', [StringComparison]::Ordinal)) 'Gantt layout must reserve a responsive inspector column.'
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
    Assert-Condition ($appJs.Contains('CALCULATED · DEPENDENCY CPM', [StringComparison]::Ordinal)) 'Inspector must label CPM values as calculated dependency analysis.'
    Assert-Condition ($appJs.Contains('SOURCE EVIDENCE', [StringComparison]::Ordinal) -and $appJs.Contains('sourceReferences', [StringComparison]::Ordinal)) 'Inspector must expose safe item-level source evidence.'
    Assert-Condition ($appJs.Contains('through AS OF', [StringComparison]::Ordinal)) 'Open actual bars must explain the as-of endpoint as observation time.'
    Assert-Condition ($appJs.Contains('gantt-actual-finish-marker', [StringComparison]::Ordinal) -and $appJs.Contains('ACTUAL finish recorded', [StringComparison]::Ordinal)) 'Finish-only actual evidence must remain visible without fabricating a start date.'
    Assert-Condition ($appJs.Contains('node("button", null, "gantt-alert-marker"', [StringComparison]::Ordinal) -and $appJs.Contains('marker.type = "button"', [StringComparison]::Ordinal)) 'Alert markers must be keyboard-operable controls.'
    Assert-Condition ($appJs.Contains('delivery-card count', [StringComparison]::Ordinal)) 'Completion language must remain explicitly card-count based.'
    $summaryStart = $appJs.IndexOf('function renderSummary', [StringComparison]::Ordinal)
    $summaryEnd = $appJs.IndexOf('function renderTable', [StringComparison]::Ordinal)
    Assert-Condition ($summaryStart -ge 0 -and $summaryEnd -gt $summaryStart -and -not $appJs.Substring($summaryStart, $summaryEnd - $summaryStart).Contains('addEventListener', [StringComparison]::Ordinal)) 'Summary rendering must not accumulate click listeners.'
    Assert-Condition ($appJs.Contains('zoom: "day"', [StringComparison]::Ordinal)) 'Gantt must default to a readable daily planning scale.'
    Assert-Condition ($appJs.Contains('gantt-day-grid', [StringComparison]::Ordinal)) 'Gantt timeline must expose a daily grid.'
    Assert-Condition ($stylesCss.Contains('.gantt-day-grid', [StringComparison]::Ordinal)) 'Gantt styles must expose a daily grid treatment.'
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
    Assert-Condition ($appJs.Contains('EVIDENCE SCOPE', [StringComparison]::Ordinal) -and $appJs.Contains('Readiness/gate execution records are not part of the current MVP1 intake.', [StringComparison]::Ordinal)) 'Overview must disclose the current evidence boundary.'
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
    Assert-Condition ($timelineBackgroundSource.Contains('gantt-day-grid', [StringComparison]::Ordinal) -and $timelineBackgroundSource.Contains('dateAt(cursor, 1)', [StringComparison]::Ordinal)) 'Daily Gantt behavior must render one grid cell per day.'
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
        OverdueAfterExecution = $execution.analysis.executionStatus.overdue
        AtRiskAfterExecution = $atRisk.analysis.executionStatus.atRisk
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

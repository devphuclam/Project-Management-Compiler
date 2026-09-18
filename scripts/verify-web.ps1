[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '..'))
$appDll = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\ProjectManagementCompiler\bin\Debug\net10.0\ProjectManagementCompiler.dll'))
$fixture = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'tests\fixtures\ideaengineering'))
$programPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\ProjectManagementCompiler\Program.cs'))
$appJsPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\ProjectManagementCompiler\wwwroot\app.js'))

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
    Assert-Condition ($summary.project.id -eq 'idea-ddm-technical-pilot-2026') 'API compile did not return the controlled project.'
    Assert-Condition ($summary.views.dashboard.totalCards -eq 53) 'API compile did not preserve the 53 delivery cards.'

    $views = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/views' -Method Get
    Assert-Condition ($views.gantt.items.Count -eq 53) 'API views did not expose the shared Gantt projection.'
    Assert-Condition ($views.gantt.milestones.Count -eq 7) 'API views did not expose milestone markers.'

    $execution = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/execution' -Method Post -Body @{
        workItemId = 'P04-A'
        executionState = 'IN_PROGRESS'
        actualStart = '2026-09-25'
        lastUpdatedAt = '2026-09-28T10:00:00Z'
    }
    Assert-Condition ($execution.analysis.executionStatus.overdue -eq 1) 'API execution update did not recalculate overdue status.'

    $jsonResponse = Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/exports/project.json' -TimeoutSec 30
    $jsonText = [string] $jsonResponse.Content
    Assert-Condition (-not ($jsonText -match '"content"\s*:')) 'Persisted JSON must not leak captured source content.'
    Assert-Condition (-not $jsonText.Contains($fixture, [StringComparison]::OrdinalIgnoreCase)) 'Persisted JSON must not leak an absolute source path.'
    Assert-Condition ($jsonText.Contains('"executionOverlay"', [StringComparison]::Ordinal)) 'Persisted JSON must include the execution overlay.'

    $reopened = Invoke-JsonApi -Uri 'http://127.0.0.1:5050/api/reopen' -Method Post -Body @{
        json = $jsonText
        asOfDate = '2026-09-28'
    }
    Assert-Condition ($reopened.baseline.id -eq $summary.baseline.id) 'API reopen changed the baseline identity.'
    Assert-Condition ($reopened.analysis.executionStatus.overdue -eq 1) 'API reopen did not recalculate the overdue alert.'

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
    }
    finally {
        $archive.Dispose()
        $fileStream.Dispose()
    }

    $appJs = Get-Content -LiteralPath $appJsPath -Raw
    Assert-Condition (-not $appJs.Contains('innerHTML', [StringComparison]::OrdinalIgnoreCase)) 'Browser UI must not use unsafe innerHTML rendering.'
    Assert-Condition ($appJs.Contains('Delivery cards completed', [StringComparison]::Ordinal)) 'Browser UI must label completion as Delivery cards completed X/53.'
    Assert-Condition ($appJs.Contains('/api/reopen', [StringComparison]::Ordinal)) 'Browser UI must expose canonical JSON reopen.'
    $program = Get-Content -LiteralPath $programPath -Raw
    Assert-Condition ($program.Contains('http://127.0.0.1:5050', [StringComparison]::Ordinal)) 'Program must bind to the loopback address.'
    Assert-Condition (-not $program.Contains('0.0.0.0', [StringComparison]::Ordinal)) 'Program must not bind to all interfaces.'

    [PSCustomObject]@{
        Health = $health.status
        ProjectId = $summary.project.id
        Cards = $summary.views.dashboard.totalCards
        OverdueAfterExecution = $execution.analysis.executionStatus.overdue
        ReopenOverdue = $reopened.analysis.executionStatus.overdue
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

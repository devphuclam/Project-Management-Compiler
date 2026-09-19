[CmdletBinding()]
param(
    [switch] $NoBrowser,
    [Alias('SourcePath')]
    [string] $RepositoryRoot,
    [string] $ManifestPath = 'planning/project-management-compiler-manifest.json',
    [string] $SourceCommit,
    [ValidateSet('GIT_COMMIT', 'UNCOMMITTED_PREVIEW')]
    [string] $ImportMode = 'GIT_COMMIT',
    [string] $AnalysisAsOfOverride,
    [switch] $ImportManifest
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '..'))
$projectPath = Join-Path -Path $repositoryRoot -ChildPath 'src\ProjectManagementCompiler\ProjectManagementCompiler.csproj'
$appUrl = 'http://127.0.0.1:5050/'
$healthUrl = $appUrl + 'api/health'
$expectedBinding = $appUrl.TrimEnd('/')
$process = $null

try {
    if ([string]::IsNullOrWhiteSpace($RepositoryRoot) -and $ImportManifest) {
        throw 'RepositoryRoot is required when ImportManifest is specified.'
    }

    if ($ImportManifest -and $ImportMode -eq 'GIT_COMMIT' -and [string]::IsNullOrWhiteSpace($SourceCommit)) {
        throw 'SourceCommit is required for a GIT_COMMIT manifest import.'
    }

    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Project file was not found at '$projectPath'."
    }

    $dotnetCommand = Get-Command -Name 'dotnet' -CommandType Application -ErrorAction Stop
    $quotedProjectPath = '"{0}"' -f $projectPath
    Write-Host 'Starting Project Management Compiler...'
    Write-Host "Project: $projectPath"

    $process = Start-Process `
        -FilePath $dotnetCommand.Source `
        -ArgumentList @('run', '--project', $quotedProjectPath, '--no-launch-profile') `
        -WorkingDirectory $repositoryRoot `
        -PassThru

    $ready = $false
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) {
            throw "The application exited before becoming ready (exit code $($process.ExitCode))."
        }

        try {
            $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 2
            if ($health.status -eq 'ok' -and $health.binding -eq $expectedBinding -and -not $health.publicNetworkBinding) {
                $ready = $true
                break
            }
        }
        catch {
            # The server may still be building or binding its loopback listener.
        }

        Start-Sleep -Milliseconds 400
    }

    if (-not $ready) {
        throw "The application did not become ready within 60 seconds. Check the application output above."
    }

    if ($ImportManifest) {
        $body = @{
            repositoryRoot = $RepositoryRoot
            manifestPath = $ManifestPath
            mode = $ImportMode
            requestedCommit = if ([string]::IsNullOrWhiteSpace($SourceCommit)) { $null } else { $SourceCommit }
            analysisAsOfOverride = if ([string]::IsNullOrWhiteSpace($AnalysisAsOfOverride)) { $null } else { $AnalysisAsOfOverride }
        } | ConvertTo-Json -Depth 20 -Compress
        $import = Invoke-RestMethod -Uri ($appUrl + 'api/manifest-import') -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 30
        if ($null -eq $import.snapshot) {
            $codes = @($import.diagnostics | ForEach-Object { $_.code }) -join ', '
            throw "Manifest import did not produce a valid snapshot. Diagnostics: $codes"
        }

        Write-Host "Manifest imported: $($import.classification)" -ForegroundColor Green
        Write-Host "Snapshot: $($import.snapshot.metadata.snapshotId)"
    }

    if (-not $NoBrowser) {
        Start-Process -FilePath $appUrl | Out-Null
    }

    Write-Host ''
    Write-Host "Project is ready: $appUrl" -ForegroundColor Green
    Write-Host 'Keep this window open while using the project. Press Ctrl+C to stop it.'
    Wait-Process -Id $process.Id
}
catch {
    Write-Error $_
    exit 1
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}

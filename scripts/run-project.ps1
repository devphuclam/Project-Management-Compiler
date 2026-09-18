[CmdletBinding()]
param(
    [switch] $NoBrowser
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '..'))
$projectPath = Join-Path -Path $repositoryRoot -ChildPath 'src\ProjectManagementCompiler\ProjectManagementCompiler.csproj'
$appUrl = 'http://127.0.0.1:5050/'
$healthUrl = $appUrl + 'api/health'
$expectedBinding = $appUrl.TrimEnd('/')
$process = $null

try {
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Project file was not found at '$projectPath'."
    }

    $dotnetCommand = Get-Command -Name 'dotnet' -CommandType Application -ErrorAction Stop
    Write-Host 'Starting Project Management Compiler...'
    Write-Host "Project: $projectPath"

    $process = Start-Process `
        -FilePath $dotnetCommand.Source `
        -ArgumentList @('run', '--project', $projectPath, '--no-launch-profile') `
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

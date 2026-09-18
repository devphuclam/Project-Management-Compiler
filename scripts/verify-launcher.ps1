[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '..'))
$launcherPath = Join-Path -Path $repositoryRoot -ChildPath 'Run Project.cmd'
$runnerPath = Join-Path -Path $PSScriptRoot -ChildPath 'run-project.ps1'

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

Assert-Condition (Test-Path -LiteralPath $launcherPath -PathType Leaf) 'Root launcher Run Project.cmd must exist.'
Assert-Condition (Test-Path -LiteralPath $runnerPath -PathType Leaf) 'PowerShell runner scripts/run-project.ps1 must exist.'

$launcher = Get-Content -LiteralPath $launcherPath -Raw
$runner = Get-Content -LiteralPath $runnerPath -Raw

Assert-Condition ($launcher.Contains('run-project.ps1', [StringComparison]::Ordinal)) 'Root launcher must delegate to scripts/run-project.ps1.'
Assert-Condition ($launcher.Contains('ExecutionPolicy Bypass', [StringComparison]::OrdinalIgnoreCase)) 'Root launcher must run without requiring a local PowerShell policy change.'
Assert-Condition ($runner.Contains('ProjectManagementCompiler.csproj', [StringComparison]::Ordinal)) 'Runner must target the ProjectManagementCompiler project.'
Assert-Condition ($runner.Contains('127.0.0.1:5050', [StringComparison]::Ordinal)) 'Runner must use the loopback application URL.'
Assert-Condition ($runner.Contains('api/health', [StringComparison]::Ordinal)) 'Runner must wait for the application health endpoint.'
Assert-Condition ($runner.Contains('publicNetworkBinding', [StringComparison]::Ordinal)) 'Runner must verify that the health response is loopback-only.'
Assert-Condition ($runner.Contains('binding', [StringComparison]::Ordinal)) 'Runner must verify the expected health binding.'
Assert-Condition ($runner.Contains('Start-Process', [StringComparison]::Ordinal)) 'Runner must launch the application and browser.'
Assert-Condition ($runner.Contains('Wait-Process', [StringComparison]::Ordinal)) 'Runner must keep the launcher attached while the application is running.'
Assert-Condition ($runner.Contains('Stop-Process', [StringComparison]::Ordinal)) 'Runner must clean up the child application process on launcher exit.'
Assert-Condition (-not $runner.Contains('0.0.0.0', [StringComparison]::Ordinal)) 'Runner must not expose the application on all interfaces.'

'PASS LauncherContract'

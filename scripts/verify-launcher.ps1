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

Assert-Condition ($launcher.IndexOf('run-project.ps1', [StringComparison]::Ordinal) -ge 0) 'Root launcher must delegate to scripts/run-project.ps1.'
Assert-Condition ($launcher.IndexOf('ExecutionPolicy Bypass', [StringComparison]::OrdinalIgnoreCase) -ge 0) 'Root launcher must run without requiring a local PowerShell policy change.'
Assert-Condition ($runner.IndexOf('ProjectManagementCompiler.csproj', [StringComparison]::Ordinal) -ge 0) 'Runner must target the ProjectManagementCompiler project.'
Assert-Condition ($runner.IndexOf('$quotedProjectPath', [StringComparison]::Ordinal) -ge 0) 'Runner must quote the project path before passing it to Start-Process.'
Assert-Condition ($runner.IndexOf('127.0.0.1:5050', [StringComparison]::Ordinal) -ge 0) 'Runner must use the loopback application URL.'
Assert-Condition ($runner.IndexOf('api/health', [StringComparison]::Ordinal) -ge 0) 'Runner must wait for the application health endpoint.'
Assert-Condition ($runner.IndexOf('publicNetworkBinding', [StringComparison]::Ordinal) -ge 0) 'Runner must verify that the health response is loopback-only.'
Assert-Condition ($runner.IndexOf('binding', [StringComparison]::Ordinal) -ge 0) 'Runner must verify the expected health binding.'
Assert-Condition ($runner.IndexOf('Start-Process', [StringComparison]::Ordinal) -ge 0) 'Runner must launch the application and browser.'
Assert-Condition ($runner.IndexOf('Wait-Process', [StringComparison]::Ordinal) -ge 0) 'Runner must keep the launcher attached while the application is running.'
Assert-Condition ($runner.IndexOf('Stop-Process', [StringComparison]::Ordinal) -ge 0) 'Runner must clean up the child application process on launcher exit.'
Assert-Condition (-not ($runner.IndexOf('0.0.0.0', [StringComparison]::Ordinal) -ge 0)) 'Runner must not expose the application on all interfaces.'

'PASS LauncherContract'

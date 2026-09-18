[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '..'))

function Invoke-CheckedDotnet {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE."
    }
}

Push-Location -LiteralPath $repositoryRoot
try {
    Invoke-CheckedDotnet @(
        'run',
        '--project',
        '.\tests\ProjectManagementCompiler.Tests\ProjectManagementCompiler.Tests.csproj',
        '--no-restore'
    )
}
finally {
    Pop-Location
}

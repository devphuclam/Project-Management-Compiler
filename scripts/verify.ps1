[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

& (Join-Path -Path $PSScriptRoot -ChildPath 'build.ps1')
& (Join-Path -Path $PSScriptRoot -ChildPath 'test.ps1')

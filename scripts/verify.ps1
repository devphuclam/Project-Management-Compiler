[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# verify-web.ps1 uses the .NET overload-aware string APIs available in
# PowerShell 7. Keep the public verification entry point usable when a user
# invokes it from Windows PowerShell 5.1.
if ($PSEdition -eq 'Desktop') {
    $pwsh = Get-Command -Name 'pwsh' -ErrorAction SilentlyContinue
    if ($null -eq $pwsh) {
        throw 'PowerShell 7 (pwsh) is required to run the verification gate from Windows PowerShell 5.1.'
    }

    & $pwsh.Source -NoLogo -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath
    exit $LASTEXITCODE
}

& (Join-Path -Path $PSScriptRoot -ChildPath 'build.ps1')
& (Join-Path -Path $PSScriptRoot -ChildPath 'test.ps1')
& (Join-Path -Path $PSScriptRoot -ChildPath 'verify-launcher.ps1')
& (Join-Path -Path $PSScriptRoot -ChildPath 'verify-web.ps1')

[CmdletBinding()]
param(
    [ValidateSet('catalog', 'recipes', 'full')]
    [string]$Profile = 'catalog',
    [ValidateRange(1, 7200)]
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
& python (Join-Path $PSScriptRoot 'verify.py') --profile $Profile --timeout $TimeoutSeconds
exit $LASTEXITCODE

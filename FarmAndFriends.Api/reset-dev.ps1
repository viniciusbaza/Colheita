[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    # O baseline de migrations pertence ao código-fonte. Um reset de dados
    # reaplica esse histórico; nunca apaga ou regenera migrations.
    & dotnet ef database update
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef database update failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

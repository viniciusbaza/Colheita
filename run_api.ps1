$ErrorActionPreference = "Stop"

$repositoryRoot = $PSScriptRoot
$databaseDirectory = Join-Path $repositoryRoot "infra\database"
$apiDirectory = Join-Path $repositoryRoot "FarmAndFriends.Api"

Push-Location -LiteralPath $databaseDirectory
try {
    # Subir banco
    & docker compose up -d
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Push-Location -LiteralPath $apiDirectory
try {
    # Atualizar banco
    & dotnet ef database update
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef database update failed with exit code $LASTEXITCODE. The API was not started."
    }

    # Rodar API
    & dotnet run
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet run failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

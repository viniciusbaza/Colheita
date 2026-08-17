[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$databaseDirectory = $PSScriptRoot
$workspaceDirectory = (Resolve-Path (Join-Path $databaseDirectory '..\..')).Path
$apiDirectory = Join-Path $workspaceDirectory 'FarmAndFriends.Api'
$apiResetScript = Join-Path $apiDirectory 'reset-dev.ps1'
$composeFile = Join-Path $databaseDirectory 'docker-compose.yml'

if (-not (Test-Path -LiteralPath $composeFile -PathType Leaf)) {
    throw "Docker Compose file not found at '$composeFile'."
}

if (-not (Test-Path -LiteralPath $apiResetScript -PathType Leaf)) {
    throw "API reset script not found at '$apiResetScript'."
}

Push-Location $databaseDirectory
try {
    # Destrói somente os containers e volumes declarados neste compose.
    & docker compose --file $composeFile down --volumes
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose down --volumes failed with exit code $LASTEXITCODE."
    }

    & docker compose --file $composeFile up --detach
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up --detach failed with exit code $LASTEXITCODE."
    }

    $postgresReady = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        & docker compose --file $composeFile exec --no-TTY postgres `
            pg_isready --username farm_user --dbname farmandfriends *> $null
        if ($LASTEXITCODE -eq 0) {
            $postgresReady = $true
            break
        }

        Start-Sleep -Seconds 1
    }

    if (-not $postgresReady) {
        throw 'PostgreSQL did not become ready within 30 seconds.'
    }
}
finally {
    Pop-Location
}

# Reaplica o baseline versionado. Este fluxo não remove nem gera migrations.
& $apiResetScript
if ($LASTEXITCODE -ne 0) {
    throw "API database reset failed with exit code $LASTEXITCODE."
}

Write-Host 'Database volume recreated and existing migrations applied.'

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$umbrellaDir = Split-Path -Parent $scriptDir
$parentDir = Split-Path -Parent $umbrellaDir
$reposFile = Join-Path $umbrellaDir "repos.json"

Write-Host "Cloning all repositories..." -ForegroundColor Green

$repos = Get-Content $reposFile | ConvertFrom-Json

foreach ($service in $repos.services) {
    $name = $service.name
    $repo = $service.repo
    $branch = $service.branch
    $targetDir = Join-Path $parentDir $name

    if (Test-Path $targetDir) {
        Write-Host "✓ $name already exists, pulling latest..." -ForegroundColor Yellow
        Push-Location $targetDir
        git fetch
        git checkout $branch
        git pull
        Pop-Location
    } else {
        Write-Host "→ Cloning $name..." -ForegroundColor Cyan
        git clone -b $branch $repo $targetDir
    }
}

Write-Host "✅ All repositories cloned/updated" -ForegroundColor Green

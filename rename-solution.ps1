# Script para renomear a solução de BiomePampa para SaaS_BasePlatform
$rootPath = "C:\Users\Nickolas\source\repos\SaaSBasePlatform"

Write-Host "Iniciando renomeação da solução..." -ForegroundColor Green

# 1. Renomear pastas de projetos
$folders = @(
    @{Old = "BiomePampa.Application"; New = "SaaS_BasePlatform.Application"},
    @{Old = "BiomePampa.Domain"; New = "SaaS_BasePlatform.Domain"},
    @{Old = "BiomePampa.Infrastructure"; New = "SaaS_BasePlatform.Infrastructure"},
    @{Old = "BiomePampa.Tests"; New = "SaaS_BasePlatform.Tests"}
)

foreach ($folder in $folders) {
    $oldPath = Join-Path $rootPath $folder.Old
    $newPath = Join-Path $rootPath $folder.New
    if (Test-Path $oldPath) {
        Write-Host "Renomeando pasta: $($folder.Old) -> $($folder.New)"
        Rename-Item -Path $oldPath -NewName $folder.New -Force
    }
}

# 2. Renomear arquivos .csproj
$projectFiles = @(
    @{Path = "SaaS_BasePlatform.Api"; Old = "BiomePampa.Api.csproj"; New = "SaaS_BasePlatform.Api.csproj"},
    @{Path = "SaaS_BasePlatform.Application"; Old = "BiomePampa.Application.csproj"; New = "SaaS_BasePlatform.Application.csproj"},
    @{Path = "SaaS_BasePlatform.Domain"; Old = "BiomePampa.Domain.csproj"; New = "SaaS_BasePlatform.Domain.csproj"},
    @{Path = "SaaS_BasePlatform.Infrastructure"; Old = "BiomePampa.Infrastructure.csproj"; New = "SaaS_BasePlatform.Infrastructure.csproj"},
    @{Path = "SaaS_BasePlatform.Tests"; Old = "BiomePampa.Tests.csproj"; New = "SaaS_BasePlatform.Tests.csproj"}
)

foreach ($proj in $projectFiles) {
    $oldFile = Join-Path $rootPath "$($proj.Path)\$($proj.Old)"
    $newFile = Join-Path $rootPath "$($proj.Path)\$($proj.New)"
    if (Test-Path $oldFile) {
        Write-Host "Renomeando arquivo: $($proj.Old) -> $($proj.New)"
        Rename-Item -Path $oldFile -NewName $proj.New -Force
    }
}

# 3. Atualizar conteúdo de todos os arquivos .cs
Write-Host "Atualizando namespaces nos arquivos .cs..." -ForegroundColor Yellow
Get-ChildItem -Path $rootPath -Filter "*.cs" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match "BiomePampa") {
        $newContent = $content -replace "BiomePampa", "SaaS_BasePlatform"
        Set-Content -Path $_.FullName -Value $newContent -NoNewline
        Write-Host "  Atualizado: $($_.FullName)"
    }
}

# 4. Atualizar arquivos .csproj (referências de projeto)
Write-Host "Atualizando referências nos arquivos .csproj..." -ForegroundColor Yellow
Get-ChildItem -Path $rootPath -Filter "*.csproj" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match "BiomePampa") {
        $newContent = $content -replace "BiomePampa", "SaaS_BasePlatform"
        Set-Content -Path $_.FullName -Value $newContent -NoNewline
        Write-Host "  Atualizado: $($_.FullName)"
    }
}

# 5. Atualizar arquivo da solução (.slnx)
Write-Host "Atualizando arquivo da solução..." -ForegroundColor Yellow
$slnFile = Join-Path $rootPath "SaaS_BasePlatform.slnx"
if (Test-Path $slnFile) {
    $content = Get-Content $slnFile -Raw
    $newContent = $content -replace "BiomePampa", "SaaS_BasePlatform"
    Set-Content -Path $slnFile -Value $newContent -NoNewline
    Write-Host "  Atualizado: $slnFile"
}

# 6. Atualizar arquivos de configuração e outros
Write-Host "Atualizando outros arquivos de configuração..." -ForegroundColor Yellow
$otherFiles = Get-ChildItem -Path $rootPath -Include "*.json", "*.xml", "*.config", "launchSettings.json" -Recurse
foreach ($file in $otherFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($content -and $content -match "BiomePampa") {
        $newContent = $content -replace "BiomePampa", "SaaS_BasePlatform"
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        Write-Host "  Atualizado: $($file.FullName)"
    }
}

Write-Host "`nRenomeação concluída com sucesso!" -ForegroundColor Green
Write-Host "Por favor, reabra a solução no Visual Studio." -ForegroundColor Cyan

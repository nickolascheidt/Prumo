# Script para finalizar a renomeação das pastas restantes
# Execute este script APÓS fechar o Visual Studio

$rootPath = "C:\Users\Nickolas\source\repos\SaaSBasePlatform"

Write-Host "Finalizando renomeação das pastas..." -ForegroundColor Green

# Primeiro, renomear arquivos .csproj dentro das pastas antigas
Write-Host "`nEtapa 1: Renomeando arquivos .csproj..." -ForegroundColor Yellow
$projectFilesInOldFolders = @(
    @{Path = "BiomePampa.Application"; Old = "BiomePampa.Application.csproj"; New = "SaaS_BasePlatform.Application.csproj"},
    @{Path = "BiomePampa.Domain"; Old = "BiomePampa.Domain.csproj"; New = "SaaS_BasePlatform.Domain.csproj"},
    @{Path = "BiomePampa.Infrastructure"; Old = "BiomePampa.Infrastructure.csproj"; New = "SaaS_BasePlatform.Infrastructure.csproj"}
)

foreach ($proj in $projectFilesInOldFolders) {
    $oldFile = Join-Path $rootPath "$($proj.Path)\$($proj.Old)"
    if (Test-Path $oldFile) {
        Write-Host "Renomeando arquivo: $($proj.Path)\$($proj.Old) -> $($proj.New)"
        try {
            Rename-Item -Path $oldFile -NewName $proj.New -Force -ErrorAction Stop
            Write-Host "  Sucesso!" -ForegroundColor Green
        }
        catch {
            Write-Host "  Erro: $_" -ForegroundColor Red
        }
    }
}

# Depois, renomear as pastas
Write-Host "`nEtapa 2: Renomeando pastas..." -ForegroundColor Yellow
$folders = @(
    @{Old = "BiomePampa.Application"; New = "SaaS_BasePlatform.Application"},
    @{Old = "BiomePampa.Domain"; New = "SaaS_BasePlatform.Domain"},
    @{Old = "BiomePampa.Infrastructure"; New = "SaaS_BasePlatform.Infrastructure"}
)

foreach ($folder in $folders) {
    $oldPath = Join-Path $rootPath $folder.Old
    $newPath = Join-Path $rootPath $folder.New
    if (Test-Path $oldPath) {
        Write-Host "Renomeando pasta: $($folder.Old) -> $($folder.New)"
        try {
            Rename-Item -Path $oldPath -NewName $folder.New -Force -ErrorAction Stop
            Write-Host "  Sucesso!" -ForegroundColor Green
        }
        catch {
            Write-Host "  Erro: $_" -ForegroundColor Red
            Write-Host "  Certifique-se de que o Visual Studio está fechado e tente novamente." -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "Pasta $($folder.Old) não encontrada (já pode ter sido renomeada)" -ForegroundColor Yellow
    }
}

Write-Host "`nRenomeação finalizada!" -ForegroundColor Green
Write-Host "Você pode agora reabrir a solução SaaS_BasePlatform.slnx no Visual Studio." -ForegroundColor Cyan

# PowerShell Script para facilitar operações com Docker do BiomePampa

function Show-Menu {
    Write-Host "🌾 BiomePampa - Sistema de Gestão de Funcionários para Safra" -ForegroundColor Green
    Write-Host ""
    Write-Host "Escolha uma opção:"
    Write-Host "1) Iniciar aplicação (build + up)"
    Write-Host "2) Parar aplicação"
    Write-Host "3) Ver logs da API"
    Write-Host "4) Ver logs do banco de dados"
    Write-Host "5) Limpar tudo (incluindo volumes)"
    Write-Host "6) Rebuild completo"
    Write-Host "7) Verificar health checks"
    Write-Host "8) Abrir Swagger no navegador"
    Write-Host "0) Sair"
    Write-Host ""
}

function Start-App {
    Write-Host "🚀 Iniciando aplicação..." -ForegroundColor Yellow
    docker-compose up -d --build
    Write-Host "✅ Aplicação iniciada!" -ForegroundColor Green
    Write-Host "📍 API: http://localhost:8080"
    Write-Host "📍 Swagger: http://localhost:8080/scalar/v1"
    Write-Host "📍 Health: http://localhost:8080/health"
}

function Stop-App {
    Write-Host "🛑 Parando aplicação..." -ForegroundColor Yellow
    docker-compose down
    Write-Host "✅ Aplicação parada!" -ForegroundColor Green
}

function Show-ApiLogs {
    Write-Host "📋 Logs da API (Ctrl+C para sair)..." -ForegroundColor Cyan
    docker-compose logs -f api
}

function Show-DbLogs {
    Write-Host "📋 Logs do Banco de Dados (Ctrl+C para sair)..." -ForegroundColor Cyan
    docker-compose logs -f db
}

function Clean-All {
    Write-Host "🧹 Limpando tudo..." -ForegroundColor Yellow
    docker-compose down -v
    docker system prune -f
    Write-Host "✅ Limpeza concluída!" -ForegroundColor Green
}

function Rebuild-App {
    Write-Host "🔨 Rebuild completo..." -ForegroundColor Yellow
    docker-compose down
    docker-compose build --no-cache
    docker-compose up -d
    Write-Host "✅ Rebuild concluído!" -ForegroundColor Green
}

function Check-Health {
    Write-Host "🏥 Verificando health checks..." -ForegroundColor Cyan
    Write-Host ""
    Write-Host "API Health:" -ForegroundColor Yellow
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:8080/health" -Method Get
        $response | ConvertTo-Json -Depth 10
    } catch {
        Write-Host "❌ API não está respondendo" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "API Ready:" -ForegroundColor Yellow
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:8080/health/ready" -Method Get
        $response | ConvertTo-Json -Depth 10
    } catch {
        Write-Host "❌ API não está ready" -ForegroundColor Red
    }
}

function Open-Swagger {
    Write-Host "🌐 Abrindo Swagger no navegador..." -ForegroundColor Cyan
    Start-Process "http://localhost:8080/scalar/v1"
}

# Loop principal
do {
    Clear-Host
    Show-Menu
    $option = Read-Host "Opção"
    Write-Host ""
    
    switch ($option) {
        "1" { Start-App }
        "2" { Stop-App }
        "3" { Show-ApiLogs }
        "4" { Show-DbLogs }
        "5" { Clean-All }
        "6" { Rebuild-App }
        "7" { Check-Health }
        "8" { Open-Swagger }
        "0" { 
            Write-Host "👋 Até logo!" -ForegroundColor Green
            exit 
        }
        default { Write-Host "❌ Opção inválida!" -ForegroundColor Red }
    }
    
    Write-Host ""
    Read-Host "Pressione ENTER para continuar"
} while ($true)

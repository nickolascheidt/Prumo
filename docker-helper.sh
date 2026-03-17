#!/bin/bash

# Script para facilitar operações com Docker do BiomePampa

set -e

echo "🌾 BiomePampa - Sistema de Gestão de Funcionários para Safra"
echo ""

# Função para exibir menu
show_menu() {
    echo "Escolha uma opção:"
    echo "1) Iniciar aplicação (build + up)"
    echo "2) Parar aplicação"
    echo "3) Ver logs da API"
    echo "4) Ver logs do banco de dados"
    echo "5) Limpar tudo (incluindo volumes)"
    echo "6) Rebuild completo"
    echo "7) Verificar health checks"
    echo "8) Acessar bash do container da API"
    echo "0) Sair"
    echo ""
}

# Função para iniciar
start_app() {
    echo "🚀 Iniciando aplicação..."
    docker-compose up -d --build
    echo "✅ Aplicação iniciada!"
    echo "📍 API: http://localhost:8080"
    echo "📍 Swagger: http://localhost:8080/scalar/v1"
    echo "📍 Health: http://localhost:8080/health"
}

# Função para parar
stop_app() {
    echo "🛑 Parando aplicação..."
    docker-compose down
    echo "✅ Aplicação parada!"
}

# Função para ver logs da API
logs_api() {
    echo "📋 Logs da API (Ctrl+C para sair)..."
    docker-compose logs -f api
}

# Função para ver logs do DB
logs_db() {
    echo "📋 Logs do Banco de Dados (Ctrl+C para sair)..."
    docker-compose logs -f db
}

# Função para limpar tudo
clean_all() {
    echo "🧹 Limpando tudo..."
    docker-compose down -v
    docker system prune -f
    echo "✅ Limpeza concluída!"
}

# Função para rebuild
rebuild() {
    echo "🔨 Rebuild completo..."
    docker-compose down
    docker-compose build --no-cache
    docker-compose up -d
    echo "✅ Rebuild concluído!"
}

# Função para verificar health
check_health() {
    echo "🏥 Verificando health checks..."
    echo ""
    echo "API Health:"
    curl -s http://localhost:8080/health | jq '.' || echo "API não está respondendo"
    echo ""
    echo "API Ready:"
    curl -s http://localhost:8080/health/ready | jq '.' || echo "API não está ready"
}

# Função para acessar bash
access_bash() {
    echo "🐚 Acessando bash do container..."
    docker exec -it biomepampa-api /bin/bash
}

# Loop principal
while true; do
    show_menu
    read -p "Opção: " option
    echo ""
    
    case $option in
        1) start_app ;;
        2) stop_app ;;
        3) logs_api ;;
        4) logs_db ;;
        5) clean_all ;;
        6) rebuild ;;
        7) check_health ;;
        8) access_bash ;;
        0) echo "👋 Até logo!"; exit 0 ;;
        *) echo "❌ Opção inválida!" ;;
    esac
    
    echo ""
    read -p "Pressione ENTER para continuar..."
    clear
done

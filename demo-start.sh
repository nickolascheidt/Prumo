#!/usr/bin/env bash
set -euo pipefail

# ============================================================
#  BiomePampa - Script de Demo (Arch Linux)
#  Sobe SQL Server + Redis via Docker e inicia a API .NET
# ============================================================

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

COMPOSE_FILE="docker-compose.demo.yml"
API_DIR="BiomePampa.Api"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  BiomePampa - Ambiente de Demo${NC}"
echo -e "${GREEN}========================================${NC}"

# ----------------------------------------------------------
#  1. Verificar pré-requisitos
# ----------------------------------------------------------
echo -e "\n${YELLOW}[1/5] Verificando pré-requisitos...${NC}"

command -v docker >/dev/null 2>&1 || { echo -e "${RED}Docker não encontrado. Instale com: sudo pacman -S docker${NC}"; exit 1; }
command -v dotnet >/dev/null 2>&1 || { echo -e "${RED}.NET SDK não encontrado. Instale com: sudo pacman -S dotnet-sdk${NC}"; exit 1; }

# Verificar se o Docker daemon está rodando
if ! docker info >/dev/null 2>&1; then
    echo -e "${YELLOW}Docker daemon não está rodando. Iniciando...${NC}"
    sudo systemctl start docker
    sleep 2
fi

echo -e "${GREEN}  ✓ Docker: $(docker --version)${NC}"
echo -e "${GREEN}  ✓ .NET SDK: $(dotnet --version)${NC}"

# ----------------------------------------------------------
#  2. Subir containers (SQL Server + Redis)
# ----------------------------------------------------------
echo -e "\n${YELLOW}[2/5] Subindo containers Docker (SQL Server + Redis)...${NC}"
docker compose -f "$COMPOSE_FILE" up -d

echo -e "${YELLOW}  Aguardando SQL Server ficar pronto...${NC}"
RETRIES=30
SQLCMD_BIN=""
until [ $RETRIES -le 0 ]; do
    if docker exec biomepampa-sqlserver /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "BiomePampa@Demo2025" \
        -Q "SELECT 1" -b -o /dev/null -C 2>/dev/null; then
        SQLCMD_BIN="/opt/mssql-tools18/bin/sqlcmd -C"
        break
    elif docker exec biomepampa-sqlserver /opt/mssql-tools/bin/sqlcmd \
        -S localhost -U sa -P "BiomePampa@Demo2025" \
        -Q "SELECT 1" -b -o /dev/null 2>/dev/null; then
        SQLCMD_BIN="/opt/mssql-tools/bin/sqlcmd"
        break
    fi
    RETRIES=$((RETRIES - 1))
    sleep 2
    echo -e "  Aguardando... ($RETRIES tentativas restantes)"
done

if [ $RETRIES -le 0 ]; then
    echo -e "${RED}  ✗ SQL Server não respondeu a tempo. Abortando.${NC}"
    exit 1
fi

echo -e "${GREEN}  ✓ SQL Server pronto${NC}"

# Pré-criar o banco para evitar falha do Serilog na inicialização da API
echo -e "${YELLOW}  Criando banco BiomePampaDb (se não existir)...${NC}"
docker exec biomepampa-sqlserver $SQLCMD_BIN \
    -S localhost -U sa -P "BiomePampa@Demo2025" \
    -Q "IF DB_ID('BiomePampaDb') IS NULL CREATE DATABASE BiomePampaDb" -b \
    && echo -e "${GREEN}  ✓ Banco BiomePampaDb pronto${NC}" \
    || echo -e "${YELLOW}  Aviso: não foi possível pré-criar o banco (a API tentará criar automaticamente)${NC}"

echo -e "${GREEN}  ✓ Redis pronto${NC}"

# ----------------------------------------------------------
#  3. Restaurar pacotes .NET
# ----------------------------------------------------------
echo -e "\n${YELLOW}[3/5] Restaurando pacotes .NET...${NC}"
dotnet restore
echo -e "${GREEN}  ✓ Pacotes restaurados${NC}"

# ----------------------------------------------------------
#  4. Build
# ----------------------------------------------------------
echo -e "\n${YELLOW}[4/5] Compilando a API...${NC}"
dotnet build --no-restore -c Release
echo -e "${GREEN}  ✓ Build concluído${NC}"

# ----------------------------------------------------------
#  5. Executar a API
# ----------------------------------------------------------
echo -e "\n${YELLOW}[5/5] Iniciando a API...${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  API:     http://localhost:5201${NC}"
echo -e "${GREEN}  Scalar:  http://localhost:5201/scalar/v1${NC}"
echo -e "${GREEN}  Health:  http://localhost:5201/health${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "${YELLOW}  Pressione Ctrl+C para encerrar${NC}\n"

cd "$API_DIR"
ASPNETCORE_ENVIRONMENT=Demo dotnet run --no-build --no-launch-profile -c Release --urls "http://0.0.0.0:5201"

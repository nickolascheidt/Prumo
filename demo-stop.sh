#!/usr/bin/env bash
set -euo pipefail

# ============================================================
#  BiomePampa - Encerrar ambiente de Demo
# ============================================================

RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m'

echo -e "${RED}Encerrando containers da demo...${NC}"
docker compose -f docker-compose.demo.yml down

echo -e "${GREEN}✓ Ambiente de demo encerrado${NC}"
echo -e "${GREEN}  Nota: Os dados do SQL Server foram preservados no volume 'sqlserver-data'.${NC}"
echo -e "${GREEN}  Para remover tudo (incluindo dados): docker compose -f docker-compose.demo.yml down -v${NC}"

#!/bin/bash
# LUDOC OS - PIPELINE DE CI/CD (AUTOPREVENT & DIAGNOSE)

PROJECT_PATH="/mnt/c/Users/ludoc/dnp/apps/matrix"
AUDIT_LOG="/mnt/c/Users/ludoc/dnp/docs/reports/build-audit.log"
CEREBRO_URL="http://localhost:9000/journal"
DEBUGGER_PATH="/mnt/c/Users/ludoc/dnp/services/ludoc-os/src/api/handlers/autodebug.ts"

echo "[CI] Iniciando Compilação Soberana (matrix)..."
START_TIME=$(date +%s)

# 1. Executar Build via dotnet (Windows side via cmd.exe se no WSL)
if [ -f /proc/version ]; then
    # Estamos no WSL: disparar build no Windows via cmd.exe
    cmd.exe /c "cd C:\Users\ludoc\dnp\apps\matrix && dotnet build matrix.csproj -f net10.0-windows10.0.19041.0" > /tmp/build.log 2>&1
else
    # Estamos no Windows Nativo
    dotnet build $PROJECT_PATH/matrix.csproj -f net10.0-windows10.0.19041.0 > /tmp/build.log 2>&1
fi

# 2. Verificar Sucesso
if grep -q "êxito" /tmp/build.log || grep -q "Build succeeded" /tmp/build.log; then
    END_TIME=$(date +%s)
    DURATION=$((END_TIME - START_TIME))
    echo "[CI] Build bem-sucedido em ${DURATION}s! ✅"
    curl -s -X POST -H "Content-Type: application/json" -d "{
      \"text\": \"BUILD ÊXITO: matrix compilado em ${DURATION}s.\",
      \"source\": \"hako-ci-pipeline\"
    }" $CEREBRO_URL > /dev/null
    exit 0
else
    echo "[CI] Build falhou! ❌ Disparando Diagnóstico Soberano..."
    bun run $DEBUGGER_PATH --analyze
    exit 1
fi

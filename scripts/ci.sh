#!/usr/bin/env bash
# ci.sh — pipeline local primeiroApp
# Uso: bash scripts/ci.sh [--fast] [--target android|windows]
# --fast   : só build, sem tests
# --target : framework target (default: windows)
set -euo pipefail

TARGET_PLATFORM="${TARGET:-windows}"
FAST="${FAST:-false}"
PROJ="primeiroApp.csproj"

# Parse args
for arg in "$@"; do
  case "$arg" in
    --fast)   FAST=true ;;
    --target) shift; TARGET_PLATFORM="$1" ;;
    android)  TARGET_PLATFORM=android ;;
    windows)  TARGET_PLATFORM=windows ;;
  esac
done

case "$TARGET_PLATFORM" in
  windows) FRAMEWORK="net10.0-windows10.0.19041.0" ;;
  android) FRAMEWORK="net10.0-android" ;;
  *)       FRAMEWORK="net10.0-windows10.0.19041.0" ;;
esac

# ── Helpers ───────────────────────────────────────────────────────────────────
step()  { echo ""; echo "══ $1 ══════════════════════════════════════════"; }
ok()    { echo "  ✓ $*"; }
fail()  { echo "  ❌ $*" >&2; }
warn()  { echo "  ⚠  $*"; }

START=$(date +%s)

# ── 1. Build ──────────────────────────────────────────────────────────────────
step "BUILD [$FRAMEWORK]"
BUILD_LOG=$(dotnet build "$PROJ" -f "$FRAMEWORK" 2>&1)
BUILD_EXIT=$?

echo "$BUILD_LOG" | grep -E "^Build|error|Erro" | head -5 || true

if [ "$BUILD_EXIT" -ne 0 ]; then
  fail "Build falhou (exit $BUILD_EXIT)"
  echo "$BUILD_LOG" | grep "error" | head -10
  # Gravar status dirty
  echo '{"status":"failed","warnings":0,"blockers":99,"ts":"'"$(date -u +%Y-%m-%dT%H:%M:%S)"'"}' \
    > /tmp/ludoc-build-status.json 2>/dev/null || true
  exit 1
fi

# ── 2. Warning Audit ──────────────────────────────────────────────────────────
step "WARNING AUDIT"

TOTAL_WARN=$(echo "$BUILD_LOG" | grep -cE "\bwarning\b" || true)
BLOCKER_WARN=$(echo "$BUILD_LOG" | grep -cE "MVVMTK|CS9248|CS8618|TS2345|TS2304" || true)
NOISE_WARN=$(echo "$BUILD_LOG" | grep -cE "MSB3026|MSB3027|IL[0-9]{4}" || true)
UNCLASSIFIED=$(( TOTAL_WARN - BLOCKER_WARN - NOISE_WARN ))

echo "  Total : $TOTAL_WARN"
echo "  Blockers: $BLOCKER_WARN | Ruído: $NOISE_WARN | Outros: $UNCLASSIFIED"

# Classificar blockers
if [ "$BLOCKER_WARN" -gt 0 ]; then
  fail "$BLOCKER_WARN blocker(s) detectado(s):"
  echo "$BUILD_LOG" | grep -E "MVVMTK|CS9248|CS8618" | head -5
  echo '{"status":"dirty","warnings":'"$TOTAL_WARN"',"blockers":'"$BLOCKER_WARN"',"ts":"'"$(date -u +%Y-%m-%dT%H:%M:%S)"'"}' \
    > /tmp/ludoc-build-status.json 2>/dev/null || true
  exit 1
fi

if [ "$TOTAL_WARN" -gt 0 ]; then
  warn "$NOISE_WARN warning(s) de ruído ambiental (file lock, IL trimming) — aceitáveis"
fi

ok "Build limpo — sem blockers"
echo '{"status":"clean","warnings":'"$TOTAL_WARN"',"blockers":0,"ts":"'"$(date -u +%Y-%m-%dT%H:%M:%S)"'"}' \
  > /tmp/ludoc-build-status.json 2>/dev/null || true

# ── 3. Tests ──────────────────────────────────────────────────────────────────
if [ "$FAST" = "false" ]; then
  TEST_PROJ=$(ls ../primeiroApp.Tests/*.csproj 2>/dev/null | head -1 || true)
  if [ -n "$TEST_PROJ" ]; then
    step "TESTS"
    dotnet test "$TEST_PROJ" --verbosity minimal --no-build 2>&1 | tail -10
    ok "Tests passaram"
  else
    step "TESTS"
    warn "Nenhum projeto de testes encontrado (primeiroApp.Tests/)"
    warn "Criar testes: Tier 5 do plano de elevação"
  fi
fi

# ── Summary ───────────────────────────────────────────────────────────────────
ELAPSED=$(( $(date +%s) - START ))
step "DONE em ${ELAPSED}s"
echo "  Build: ✓ clean | Warnings: $TOTAL_WARN (0 blockers) | Status: /tmp/ludoc-build-status.json"

# Procedimentos CI/CD para Forks do LUDOS Matrix

## Visão Geral

Este documento descreve os procedimentos automatizados e manuais para manutenção de forks do projeto LUDOC OS, especificamente para o app `apps/matrix`.

---

## 🔄 Fluxo Automatizado (CI/CD)

### 1. Health Check Semanal
- **Quando:** Segunda-feira às 2h UTC
- **O que:** Verifica diferenças de versão entre fork e upstream
- **Ação:** Se necessário, cria PR de sincronização

### 2. Detecção de Dependências
- **Monitora:** Dependências desatualizadas
- **Automático:** Cria PR com updates
- **Alerta:** Notificação via Slack

### 3. Auditoria de Segurança
- **Frequência:** Toda sincronização
- **Critérios:** CVEs críticas/high bloqueiam merge
- **Ferramentas:** npm audit, bun audit

### 4. Sincronização Automática
```yaml
# Fluxo sync-fork.yml
upstream_repo: diegosouzapw/omniroute
target_branch: main
auto_create_pr: true
conflict_resolution: prefer-upstream
```

---

## 🛠️ Manutenção Manual

### 1. Sync Manual
```bash
# No fork local
cd apps/matrix
git remote add upstream https://github.com/diegosouzapw/omniroute.git
git fetch upstream
git merge upstream/main --no-ff -m "sync: upstream update"
```

### 2. Resolução de Conflitos
**Prioridade:** Upstream > Fork
```bash
# Conflitos simples (automático)
git checkout --theirs [file]
git add [file]

# Conflitos complexos (manual)
git mergetool
```

### 3. Validação Pós-Sync
```bash
# Testes obrigatórios
dotnet build -f net10.0-windows10.0.19041.0
dotnet test -f net10.0-windows10.0.19041.0
```

---

## 📊 Qualidade Gate

### Checklist de PR para Fork
- [ ] Código compilado (dotnet build)
- [ ] Todos testes passando
- [ ] Nenhuma CVE crítica
- [ ] Commit message seguindo .commitlint.json
- [ ] Documentação atualizada (se aplicável)

### Branch Protection
- Main branch protegida
- PRs obrigatórios
- Status checks obrigatórios:
  - CI/CD Pipeline
  - Quality Gate
  - Security Scan

---

## 🔧 Configurações Específicas

### 1. Variáveis de Ambiente
```bash
# .env.local
MATRIX_UPSTREAM_URL=https://github.com/diegosouzapw/omniroute.git
MATRIX_MAIN_BRANCH=main
```

### 2. Scripts de Manutenção
```bash
# scripts/sync-fork.sh
#!/bin/bash
git remote add upstream $MATRIX_UPSTREAM_URL
git fetch upstream
git merge upstream/$MATRIX_MAIN_BRANCH --no-ff
```

### 3. Monitoramento
- **Slack Channel:** #ci-cd
- **GitHub Issues:** Label "fork-sync"
- **Alertas:** Sync failures, security issues

---

## 🚨 Procedimentos de Emergência

### 1. Breakage Detection
Se o fork quebrar:
1. Revert sync automático imediato
2. Criar issue com label "critical"
3. Iniciar sync manual revisado

### 2. Security Breach
- Suspender automação
- Auditar todo fork
- Revalidar builds

### 3. Data Loss
Últimos 3 commits protegidos via:
```yaml
jobs:
  backup:
    - runs-on: ubuntu-latest
    - steps:
      - uses: actions/checkout@v4
      - run: git push origin main --force
```

---

## 📈 Métricas

### Sucesso
- Uptime >99%
- Sync sem conflitos >80%
- CVEs zeradas

### Alertas
- Syncs com conflitos >20%
- Build failures >5%
- Dependências desatualizadas >10

---

## 🔄 Processo de Onboarding para Novos Forks

1. Configurar `.github/workflows/sync-fork.yml`
2. Adicionar remote upstream
3. Habilitar branch protection
4. Configurar Slack notifications
5. Executar sync inicial manual

---

## 📞 Contatos

- **Maintainer:** @ludoc
- **Upstream:** @diegosouzapw
- **Emergency:** Issues com "critical" label

---

*Última atualização: 2026-04-01*
*Próxima revisão: 2026-07-01*
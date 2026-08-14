# Pass de infra: renomear os repos e verificar o DevOps

> Escrito em 2026-08-11. **Executar DEPOIS** do pacote de segurança
> (`2026-08-11-tenant-isolation-phase1.md`). Não é código: é operação em GitHub,
> Entra ID e Terraform, e a verificação é outra.

**Goal:** Os três repositórios passam a se chamar Prumo, os READMEs descrevem o
sistema pelo nome novo, e o encadeamento de deploy continua funcionando — provado,
não presumido.

**Estimativa honesta:** 30-45 min de parte mecânica, 1-2 h no total se nada
surpreender. O `terraform plan` não roda desde junho e o ambiente está derrubado;
esse passo não tem estimativa confiável, porque a função dele é justamente
descobrir problema.

**Precisa do Nickolas:** `az login` é interativo e as credenciais no Entra exigem
autenticação dele. Este pass é necessariamente a dois.

---

## O risco principal, antes de qualquer coisa

**Renomear um repo no GitHub quebra o login OIDC com a Azure.** As federated
credentials das app registrations têm subject no formato `repo:owner/nome:ref` — o
**nome do repositório faz parte da credencial**. Renomeou e não atualizou, o
Actions para de autenticar na Azure.

As três app registrations: `gh-SaaSBasePlatform`, `gh-SaaSBasePlatform-Angular`,
`gh-SaaSBasePlatform-DevOps`.

**Por isso a ordem abaixo é: renomear e corrigir credenciais primeiro, verificar em
seguida, e só então mexer em README.** Se o OIDC quebrar, você descobre com a
árvore limpa e o rollback é só renomear de volta.

## O que este pass NÃO faz

- **Não renomeia o ACR.** `acrsaasbasecore` é nome imutável — só se recria, e
  recriar muda o `ACR_LOGIN_SERVER`, que é secret nos dois repos de app. É nome
  interno, ninguém vê, e o risco não compra nada.
- **Não renomeia recursos do Azure.**
- **Nunca roda `terraform apply`.** O `deploy.yml` do repo de DevOps roda
  `terraform apply -auto-approve`, que recriaria a stack inteira e o custo junto.
  A verificação é **plan-only**, sempre.
- **Não dispara o workflow `build-and-deploy`** de nenhum dos repos, pelo mesmo
  motivo.

---

### Task 1: Renomear os três repositórios

- [ ] **Step 1: Confirmar os nomes atuais**

```bash
gh repo list nickolascheidt --limit 20
```

- [ ] **Step 2: Renomear**

```bash
gh repo rename Prumo            -R nickolascheidt/SaaSBasePlatform
gh repo rename Prumo-Angular    -R nickolascheidt/SaaSBasePlatform-Angular
gh repo rename Prumo-DevOps     -R nickolascheidt/SaaSBasePlatform-DevOps
```

O GitHub cria redirect do nome antigo, então `git clone` e remotes locais
continuam funcionando. Não confie nisso para o `repository_dispatch` — a task 3
corrige a referência explícita.

- [ ] **Step 3: Atualizar os remotes locais**

```bash
git -C ~/source/repos/SaaSBasePlatform         remote set-url origin https://github.com/nickolascheidt/Prumo.git
git -C ~/source/repos/SaaSBasePlatform-Angular remote set-url origin https://github.com/nickolascheidt/Prumo-Angular.git
git -C ~/source/repos/SaaSBasePlatform-DevOps  remote set-url origin https://github.com/nickolascheidt/Prumo-DevOps.git
```

Os diretórios locais mantêm o nome antigo por ora — renomeá-los quebraria caminhos
em `.claude/settings.local.json` e nos planos antigos, sem ganho.

- [ ] **Step 4: Confirmar**

```bash
git -C ~/source/repos/SaaSBasePlatform remote -v && git -C ~/source/repos/SaaSBasePlatform fetch
```

Expected: fetch sem erro.

---

### Task 2: Atualizar as federated credentials no Entra

**Este é o passo que exige a autenticação do Nickolas.**

- [ ] **Step 1: Login**

```bash
az login
```

- [ ] **Step 2: Listar as credenciais de cada app registration**

```bash
for APP in gh-SaaSBasePlatform gh-SaaSBasePlatform-Angular gh-SaaSBasePlatform-DevOps; do
  echo "=== $APP ==="
  APPID=$(az ad app list --display-name "$APP" --query "[0].id" -o tsv)
  az ad app federated-credential list --id "$APPID" -o json
done
```

Anote, para cada uma: `id`, `name`, `subject`, `issuer`, `audiences`. O `subject`
é o que precisa mudar — algo como
`repo:nickolascheidt/SaaSBasePlatform:ref:refs/heads/main` ou
`repo:nickolascheidt/SaaSBasePlatform:environment:dev`.

**Pode haver mais de uma credencial por app** (uma para branch, outra para
environment, outra para pull_request). Todas que citem o nome antigo precisam
mudar.

- [ ] **Step 3: Atualizar cada subject**

Para cada credencial encontrada, trocando `<APPID>`, `<CREDID>` e o subject novo:

```bash
az ad app federated-credential update \
  --id <APPID> \
  --federated-credential-id <CREDID> \
  --parameters '{"subject":"repo:nickolascheidt/Prumo:ref:refs/heads/main"}'
```

Se o `update` reclamar, o caminho alternativo é apagar e recriar:

```bash
az ad app federated-credential delete --id <APPID> --federated-credential-id <CREDID>
az ad app federated-credential create --id <APPID> --parameters '{
  "name":"<mesmo nome de antes>",
  "issuer":"https://token.actions.githubusercontent.com",
  "subject":"repo:nickolascheidt/Prumo:ref:refs/heads/main",
  "audiences":["api://AzureADTokenExchange"]
}'
```

- [ ] **Step 4: Confirmar que nenhum subject cita o nome antigo**

```bash
for APP in gh-SaaSBasePlatform gh-SaaSBasePlatform-Angular gh-SaaSBasePlatform-DevOps; do
  APPID=$(az ad app list --display-name "$APP" --query "[0].id" -o tsv)
  az ad app federated-credential list --id "$APPID" --query "[].subject" -o tsv
done
```

Expected: **nenhuma** linha contendo `SaaSBasePlatform`.

> As app registrations em si podem manter o nome `gh-SaaSBasePlatform*` — é só
> display name, não afeta nada. Renomear é cosmético e opcional.

---

### Task 3: Corrigir a referência cruzada nos workflows

- [ ] **Step 1: Atualizar os dois arquivos**

Em `~/source/repos/SaaSBasePlatform/.github/workflows/build-and-deploy.yml` e em
`~/source/repos/SaaSBasePlatform-Angular/.github/workflows/build-and-deploy.yml`,
linha 44:

```yaml
          repository: nickolascheidt/SaaSBasePlatform-DevOps
```

vira:

```yaml
          repository: nickolascheidt/Prumo-DevOps
```

- [ ] **Step 2: Conferir que não sobrou referência**

```bash
grep -rn "SaaSBasePlatform" ~/source/repos/SaaSBasePlatform/.github/ ~/source/repos/SaaSBasePlatform-Angular/.github/ ~/source/repos/SaaSBasePlatform-DevOps/.github/
```

Expected: nenhuma saída.

- [ ] **Step 3: Commitar em cada repo**

```bash
git add .github/workflows/build-and-deploy.yml
git commit -m "ci: point repository_dispatch at the renamed DevOps repo"
```

---

### Task 4: Verificar que o DevOps ainda funciona — plan-only

- [ ] **Step 1: Validar a sintaxe**

```bash
cd ~/source/repos/SaaSBasePlatform-DevOps/terraform/core
terraform init -backend=true
terraform validate
```

Expected: `Success! The configuration is valid.`

- [ ] **Step 2: Rodar o plan**

```bash
terraform plan -out=/dev/null
```

Expected: o plan **completa**. Como o ambiente está derrubado, ele vai listar
muitos `create` — isso é esperado e **não custa nada**, porque nada é aplicado.

O que este passo realmente prova: que a autenticação na Azure funcionou, ou seja,
que o OIDC sobreviveu ao rename. É essa a razão de ele existir.

- [ ] **Step 3: Se o plan falhar**

Diagnóstico por sintoma:

| Sintoma | Causa provável |
|---|---|
| Erro de autenticação / `AADSTS700213` | Federated credential com subject errado — volte à task 2 |
| Erro de acesso ao state | Storage `stsaasbasetfstate` no `rg-saasbase-tfstate`; confira se o RG ainda existe |
| Erro de versão de provider | Dois meses sem rodar; `terraform init -upgrade` |
| Recurso com atributo desconhecido | Provider novo mudou schema; anotar e tratar como item próprio |

- [ ] **Step 4: NÃO aplicar**

Não rode `terraform apply`. Não dispare `build-and-deploy` nem `deploy.yml`. A
verificação termina aqui.

---

### Task 5: READMEs

Só depois de a task 4 passar — se o OIDC estiver quebrado, é melhor descobrir com
a árvore limpa.

- [ ] **Step 1: Backend**

`~/source/repos/SaaSBasePlatform/README.md` — o título já é "Prumo ERP — Backend
API" desde o rename. Atualizar: o link para o frontend no rodapé (linha ~141),
que ainda aponta para `SaaSBasePlatform-Angular`.

- [ ] **Step 2: Frontend**

`~/source/repos/SaaSBasePlatform-Angular/README.md` — atualizar os dois links para
o backend (linhas ~20 e ~125) e o bloco de árvore em `SYSTEM_OVERVIEW.md` linha 23,
que ainda mostra `SaaSBasePlatform/` como raiz.

- [ ] **Step 3: DevOps**

`~/source/repos/SaaSBasePlatform-DevOps/README.md` e `docs/RUNBOOK.md` — trocar os
nomes de repo. **Não** trocar nomes de recurso do Azure nem do ACR: eles continuam
`acrsaasbasecore`, `rg-saasbase-dev`, `rg-saasbase-core`, `rg-saasbase-tfstate`, e
o runbook tem que continuar batendo com a realidade.

- [ ] **Step 4: Commitar nos três**

```bash
git commit -am "docs: point the README at the renamed repositories"
```

---

## Verificação final

- [ ] Nenhum subject de federated credential cita `SaaSBasePlatform`
- [ ] `grep -rn "SaaSBasePlatform" .github/` vazio nos três repos
- [ ] `terraform validate` e `terraform plan` completam
- [ ] Nenhuma execução nova em `gh run list` nos três repos — nada deve ter
      disparado
- [ ] Os READMEs citam repos com nome novo e recursos do Azure com nome antigo
      (que é o correto)

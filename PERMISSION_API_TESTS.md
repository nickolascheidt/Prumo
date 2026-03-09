# 🧪 Testes da API de Gerenciamento de Permissões

## 📌 Pré-requisito: Token de Admin

Faça login como administrador primeiro:

```http
POST https://localhost:7249/api/auth/login
Content-Type: application/json

{
  "email": "admin@biomepampa.com",
  "password": "Admin@123"
}
```

Copie o `token` da resposta e use em todos os testes abaixo como `{admin-token}`.

---

## 1️⃣ Listar Todas as Permissões Cadastradas

```http
GET https://localhost:7249/api/permissions
Authorization: Bearer {admin-token}
```

**Esperado:** Lista de todas as permissões do banco de dados

---

## 2️⃣ Ver Catálogo de Permissões Disponíveis

```http
GET https://localhost:7249/api/permissions/catalog
Authorization: Bearer {admin-token}
```

**Esperado:** Estrutura JSON com todas as constantes de permissões:
- `employees.*`, `employees.view`, `employees.create`, etc.
- `worklogs.*`, `worklogs.view`, etc.
- `products.*`, `customers.*`, `payments.*`, `stock.*`

---

## 3️⃣ Ver Permissões de uma Role Específica

### Ver permissões do Administrador
```http
GET https://localhost:7249/api/permissions/roles/Administrador
Authorization: Bearer {admin-token}
```

**Esperado:** Lista com TODAS as permissões

### Ver permissões do Funcionário
```http
GET https://localhost:7249/api/permissions/roles/Funcionario
Authorization: Bearer {admin-token}
```

**Esperado:** Lista com subset de permissões

### Ver permissões do Cliente
```http
GET https://localhost:7249/api/permissions/roles/Cliente
Authorization: Bearer {admin-token}
```

**Esperado:** Apenas `products.view`

---

## 4️⃣ Conceder Permissão a uma Role

### Conceder permissão individual
```http
POST https://localhost:7249/api/permissions/roles/Funcionario/grant
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "permissionName": "products.edit",
  "reason": "Funcionários agora podem editar produtos"
}
```

**Esperado:** Mensagem de sucesso + registro de auditoria criado

### Conceder wildcard (todas as permissões de um módulo)
```http
POST https://localhost:7249/api/permissions/roles/Funcionario/grant
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "permissionName": "customers.*",
  "reason": "Acesso completo ao módulo de clientes"
}
```

**Esperado:** 
- Funcionário agora tem acesso a: `customers.view`, `customers.create`, `customers.edit`, `customers.delete`
- Tudo com UM ÚNICO registro de permissão wildcard

---

## 5️⃣ Revogar Permissão de uma Role

```http
DELETE https://localhost:7249/api/permissions/roles/Funcionario/revoke/products.edit?reason=Removido+por+seguranca
Authorization: Bearer {admin-token}
```

**Esperado:** Mensagem de sucesso + registro de auditoria criado com action="REVOKED"

---

## 6️⃣ Ver Histórico de Auditoria

### Ver todos os logs de auditoria
```http
GET https://localhost:7249/api/permissions/audit
Authorization: Bearer {admin-token}
```

**Esperado:** Lista dos últimos 100 registros de auditoria (GRANTED/REVOKED)

### Filtrar por role específica
```http
GET https://localhost:7249/api/permissions/audit?roleName=Funcionario&take=20
Authorization: Bearer {admin-token}
```

**Esperado:** Últimas 20 mudanças de permissões da role "Funcionario"

---

## 7️⃣ Testar Permissões Hierárquicas (Wildcards)

### Passo 1: Criar usuário de teste
```http
POST https://localhost:7249/api/auth/register
Content-Type: application/json

{
  "fullName": "Teste Wildcard",
  "email": "teste@wildcard.com",
  "password": "Teste@123"
}
```

**Copie o token retornado** como `{user-token}`

### Passo 2: Verificar permissões atuais
```http
GET https://localhost:7249/api/auth/me
Authorization: Bearer {user-token}
```

**Esperado:** Permissões limitadas (role "Usuario")

### Passo 3: Admin concede wildcard ao usuário
Primeiro, promova o usuário para "Funcionario" (via banco ou endpoint custom), depois:

```http
POST https://localhost:7249/api/permissions/roles/Funcionario/grant
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "permissionName": "employees.*",
  "reason": "Teste de wildcards"
}
```

### Passo 4: Usuário faz logout e login novamente
```http
POST https://localhost:7249/api/auth/login
Content-Type: application/json

{
  "email": "teste@wildcard.com",
  "password": "Teste@123"
}
```

**Novo token agora inclui `employees.*`**

### Passo 5: Testar acesso a endpoints
```http
# Deve funcionar (coberto por employees.*)
GET https://localhost:7249/api/employees
Authorization: Bearer {novo-user-token}

# Deve funcionar (coberto por employees.*)
POST https://localhost:7249/api/employees
Authorization: Bearer {novo-user-token}
Content-Type: application/json
{...}

# Deve funcionar (coberto por employees.*)
DELETE https://localhost:7249/api/employees/{id}
Authorization: Bearer {novo-user-token}
```

**Esperado:** Todas as requisições retornam 200/201, não 403!

---

## 8️⃣ Testar Auditoria Completa

### Passo 1: Ver auditoria antes
```http
GET https://localhost:7249/api/permissions/audit?roleName=Funcionario
Authorization: Bearer {admin-token}
```

**Anote a quantidade de registros**

### Passo 2: Fazer várias mudanças
```http
# Conceder 3 permissões
POST /api/permissions/roles/Funcionario/grant
Body: { "permissionName": "payments.view", "reason": "Teste 1" }

POST /api/permissions/roles/Funcionario/grant
Body: { "permissionName": "payments.create", "reason": "Teste 2" }

POST /api/permissions/roles/Funcionario/grant
Body: { "permissionName": "stock.*", "reason": "Teste 3" }
```

### Passo 3: Revogar 1 permissão
```http
DELETE /api/permissions/roles/Funcionario/revoke/payments.view?reason=Teste+revogacao
```

### Passo 4: Ver auditoria depois
```http
GET https://localhost:7249/api/permissions/audit?roleName=Funcionario
Authorization: Bearer {admin-token}
```

**Esperado:** 
- 4 novos registros
- 3 com action="GRANTED"
- 1 com action="REVOKED"
- Todos com email do admin que executou
- Todos com timestamp recente
- Todos com reason preenchido

---

## 9️⃣ Testar Validações

### Tentar conceder permissão inexistente
```http
POST https://localhost:7249/api/permissions/roles/Funcionario/grant
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "permissionName": "nao.existe",
  "reason": "Teste de erro"
}
```

**Esperado:** 400 Bad Request - "Permissão 'nao.existe' não encontrada"

### Tentar conceder permissão já existente
```http
POST https://localhost:7249/api/permissions/roles/Funcionario/grant
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "permissionName": "employees.view",
  "reason": "Duplicado"
}
```

**Esperado:** 400 Bad Request - "Role 'Funcionario' já possui a permissão 'employees.view'"

### Tentar revogar permissão não concedida
```http
DELETE https://localhost:7249/api/permissions/roles/Funcionario/revoke/admin.super_power
Authorization: Bearer {admin-token}
```

**Esperado:** 400 Bad Request - "Role 'Funcionario' não possui a permissão 'admin.super_power'"

### Tentar acessar sem ser admin
```http
GET https://localhost:7249/api/permissions
Authorization: Bearer {user-token-nao-admin}
```

**Esperado:** 403 Forbidden

---

## 🎯 Resumo de Endpoints

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| GET | `/api/permissions` | Lista todas permissões cadastradas |
| GET | `/api/permissions/catalog` | Catálogo de constantes |
| GET | `/api/permissions/roles/{roleName}` | Permissões de uma role |
| POST | `/api/permissions/roles/{roleName}/grant` | Conceder permissão |
| DELETE | `/api/permissions/roles/{roleName}/revoke/{permissionName}` | Revogar permissão |
| GET | `/api/permissions/audit` | Histórico de auditoria |

---

## 📝 Notas

1. **Mudanças só refletem após novo login** - JWT é estático, contém permissões do momento da geração
2. **Wildcards são poderosos** - `employees.*` = todas as ações de employees
3. **Auditoria é automática** - Não precisa fazer nada extra, já está registrando tudo
4. **Backend sempre valida** - Mesmo com permissão no token, backend verifica novamente

---

## ✅ Checklist de Testes

- [ ] Login como admin funciona
- [ ] Listar permissões retorna dados
- [ ] Ver catálogo mostra todas as constantes
- [ ] Ver permissões de role funciona para Admin/Funcionario/Cliente
- [ ] Conceder permissão individual funciona
- [ ] Conceder wildcard funciona
- [ ] Revogar permissão funciona
- [ ] Auditoria registra GRANTED
- [ ] Auditoria registra REVOKED
- [ ] Auditoria inclui email do admin
- [ ] Filtro de auditoria por role funciona
- [ ] Validações de permissão inexistente funcionam
- [ ] Validações de permissão duplicada funcionam
- [ ] 403 Forbidden para não-admin funciona
- [ ] Wildcard cobre permissões específicas (testar com logout/login)

# 👥 Gerenciamento de Roles de Usuários - API

## 📌 Endpoints Implementados

### 1. Atribuir Role a um Usuário

**Endpoint:** `POST /api/auth/users/{userId}/roles`

**Descrição:** Concede uma role a um usuário existente (apenas administradores)

**Autenticação:** Bearer Token (role: Administrador)

**Request:**
```http
POST https://localhost:7249/api/auth/users/{userId}/roles
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "roleName": "Funcionario"
}
```

**Responses:**

✅ **200 OK** - Role atribuída com sucesso
```json
{
  "message": "Role 'Funcionario' atribuída ao usuário com sucesso"
}
```

❌ **400 Bad Request** - Usuário já possui a role
```json
{
  "message": "Usuário já possui a role 'Funcionario'"
}
```

❌ **404 Not Found** - Usuário não encontrado
```json
{
  "message": "Usuário com ID '{guid}' não encontrado"
}
```

---

### 2. Remover Role de um Usuário

**Endpoint:** `DELETE /api/auth/users/{userId}/roles/{roleName}`

**Descrição:** Remove uma role de um usuário (apenas administradores)

**Autenticação:** Bearer Token (role: Administrador)

**Request:**
```http
DELETE https://localhost:7249/api/auth/users/{userId}/roles/Funcionario
Authorization: Bearer {admin-token}
```

**Responses:**

✅ **200 OK** - Role removida com sucesso
```json
{
  "message": "Role 'Funcionario' removida do usuário com sucesso"
}
```

❌ **400 Bad Request** - Usuário não possui a role
```json
{
  "message": "Usuário não possui a role 'Funcionario'"
}
```

❌ **404 Not Found** - Usuário não encontrado
```json
{
  "message": "Usuário com ID '{guid}' não encontrado"
}
```

---

### 3. Obter Roles de um Usuário

**Endpoint:** `GET /api/auth/users/{userId}/roles`

**Descrição:** Lista todas as roles de um usuário específico (apenas administradores)

**Autenticação:** Bearer Token (role: Administrador)

**Request:**
```http
GET https://localhost:7249/api/auth/users/{userId}/roles
Authorization: Bearer {admin-token}
```

**Responses:**

✅ **200 OK**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "email": "usuario@exemplo.com",
  "fullName": "João da Silva",
  "roles": [
    "Usuario",
    "Funcionario"
  ]
}
```

❌ **404 Not Found** - Usuário não encontrado
```json
{
  "message": "Usuário com ID '{guid}' não encontrado"
}
```

---

## 🧪 Exemplos de Uso Completo

### Cenário 1: Promover Usuário a Funcionário

```http
# 1. Login como admin
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@biomepampa.com",
  "password": "Admin@123"
}

# Copiar o token da resposta

# 2. Listar todos os usuários para pegar o ID
GET /api/auth/users
Authorization: Bearer {admin-token}

# 3. Atribuir role "Funcionario" ao usuário
POST /api/auth/users/123e4567-e89b-12d3-a456-426614174000/roles
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "roleName": "Funcionario"
}

# 4. Verificar as roles do usuário
GET /api/auth/users/123e4567-e89b-12d3-a456-426614174000/roles
Authorization: Bearer {admin-token}

# 5. Usuário faz logout e login novamente para pegar novas permissões
POST /api/auth/login
Content-Type: application/json

{
  "email": "usuario@exemplo.com",
  "password": "senha123"
}

# Token agora contém role "Funcionario" e permissões associadas
```

---

### Cenário 2: Rebaixar Funcionário para Usuário

```http
# 1. Login como admin
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@biomepampa.com",
  "password": "Admin@123"
}

# 2. Remover role "Funcionario"
DELETE /api/auth/users/123e4567-e89b-12d3-a456-426614174000/roles/Funcionario
Authorization: Bearer {admin-token}

# 3. Verificar que a role foi removida
GET /api/auth/users/123e4567-e89b-12d3-a456-426614174000/roles
Authorization: Bearer {admin-token}

# Resposta: apenas "Usuario"
```

---

### Cenário 3: Workflow Completo de Gerenciamento

```bash
# Admin gerencia usuário
POST /api/auth/users/{userId}/roles
Body: { "roleName": "Funcionario" }
→ Sucesso

# Verificar roles atuais
GET /api/auth/users/{userId}/roles
→ ["Usuario", "Funcionario"]

# Verificar permissões da role Funcionario
GET /api/permissions/roles/Funcionario
→ Lista de permissões (employees.view, worklogs.*, etc.)

# Conceder permissão adicional à role
POST /api/permissions/roles/Funcionario/grant
Body: { "permissionName": "products.edit", "reason": "Funcionários vendem produtos" }

# Usuário faz logout/login
POST /api/auth/login
→ Novo token com permissões atualizadas

# Verificar permissões do usuário
GET /api/auth/me
→ Lista completa de permissões (incluindo products.edit)
```

---

## 📋 Roles Disponíveis

| Role | Descrição | Permissões Padrão |
|------|-----------|-------------------|
| **Administrador** | Acesso total ao sistema | Todas as permissões |
| **Funcionario** | Funcionário da empresa | Subset de permissões (employees.view, worklogs.*, customers.*, etc.) |
| **Usuario** | Usuário padrão | Permissões básicas |
| **Cliente** | Cliente externo | Apenas products.view |

---

## 🔐 Segurança

### Apenas Administradores

Todos os endpoints de gerenciamento de roles requerem:
```csharp
[Authorize(Roles = "Administrador")]
```

### Validações Implementadas

1. ✅ **Usuário existe** - Valida se o ID é válido
2. ✅ **Role existe** - ASP.NET Identity valida automaticamente
3. ✅ **Duplicação** - Impede atribuir role já existente
4. ✅ **Remoção inválida** - Impede remover role não atribuída
5. ✅ **Autenticação** - Apenas admin pode gerenciar roles

---

## ⚠️ Importante

### Mudanças refletem no próximo login

As roles são incluídas no JWT no momento da autenticação. Para que as mudanças sejam refletidas:

1. Admin atribui/remove role
2. Usuário **faz logout**
3. Usuário **faz novo login**
4. Novo token contém roles atualizadas
5. Permissões das roles são aplicadas

### Exemplo:

```
Usuário tem role "Usuario" → Token tem permissões básicas
↓
Admin atribui role "Funcionario"
↓
Token AINDA tem permissões básicas (JWT é estático)
↓
Usuário faz logout/login
↓
Token AGORA tem permissões de Usuario + Funcionario
```

---

## 🎯 Resumo de Endpoints

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| POST | `/api/auth/users/{userId}/roles` | Atribuir role |
| DELETE | `/api/auth/users/{userId}/roles/{roleName}` | Remover role |
| GET | `/api/auth/users/{userId}/roles` | Listar roles do usuário |
| GET | `/api/auth/users` | Listar todos usuários (já existia) |
| GET | `/api/auth/me` | Ver dados do usuário atual (já existia) |

---

## 🔄 Diferença entre Roles e Permissions

### Roles (Papéis)
- Atribuídas **diretamente ao usuário**
- Gerenciadas via `/api/auth/users/{userId}/roles`
- Usuário pode ter **múltiplas roles**
- Ex: ["Usuario", "Funcionario"]

### Permissions (Permissões)
- Atribuídas **à role** (não ao usuário diretamente)
- Gerenciadas via `/api/permissions/roles/{roleName}/grant`
- Usuário **herda** permissões de todas as suas roles
- Ex: Funcionario tem `employees.view`, `worklogs.*`, etc.

### Hierarquia:

```
Usuário
  ↓ possui
Roles (Usuario, Funcionario)
  ↓ cada role possui
Permissions (employees.view, worklogs.*, etc.)
  ↓ validadas em
Endpoints ([Authorize(Policy = "employees.view")])
```

---

## ✅ Checklist de Testes

- [ ] Admin consegue atribuir role
- [ ] Admin consegue remover role
- [ ] Admin consegue ver roles de usuário
- [ ] Validação: usuário não existe (404)
- [ ] Validação: role duplicada (400)
- [ ] Validação: remover role inexistente (400)
- [ ] Não-admin recebe 403 Forbidden
- [ ] Mudanças refletem após logout/login
- [ ] Token antigo mantém roles antigas
- [ ] Token novo contém roles atualizadas

---

## 🚀 Próximos Passos Sugeridos

1. Adicionar auditoria de mudanças de roles (quem atribuiu/removeu, quando)
2. Endpoint para atribuir múltiplas roles de uma vez
3. Endpoint para substituir todas as roles de um usuário
4. Webhook/notificação quando roles mudam
5. Dashboard visual para gerenciar usuários e roles

---

✅ **Sistema completo de gerenciamento de roles implementado!**

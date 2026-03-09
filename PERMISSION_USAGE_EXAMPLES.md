# 💡 Exemplos de Uso - Sistema de Permissões v2.0

## 📚 Índice
1. [Usar Wildcards em Roles](#1-usar-wildcards-em-roles)
2. [Verificar Permissões no Código](#2-verificar-permissões-no-código)
3. [Criar Políticas com Wildcards](#3-criar-políticas-com-wildcards)
4. [Adicionar Nova Permissão](#4-adicionar-nova-permissão)
5. [Auditoria Programática](#5-auditoria-programática)
6. [Frontend - Verificar Wildcards](#6-frontend---verificar-wildcards)

---

## 1. Usar Wildcards em Roles

### Scenario: Dar acesso total a um módulo

**Antes (sem wildcards):**
```csharp
// DbInitializer.cs - tinha que adicionar TODAS as permissões
await AssignPermissionsToRole(funcionarioRole, new[]
{
    Permissions.Employees.View,
    Permissions.Employees.Create,
    Permissions.Employees.Edit,
    Permissions.Employees.Delete,
    Permissions.Employees.ManagePayments
});
```

**Depois (com wildcards):**
```csharp
// DbInitializer.cs - uma linha = todas as permissões
await AssignPermissionsToRole(funcionarioRole, new[]
{
    Permissions.Employees.All  // employees.*
});
```

---

## 2. Verificar Permissões no Código

### Scenario: Service precisa verificar se usuário pode fazer algo

```csharp
using BiomePampa.Domain.Authorization;

public class EmployeeService
{
    private readonly IPermissionService _permissionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Método helper para pegar permissões do usuário atual
    private async Task<IReadOnlyCollection<string>> GetCurrentUserPermissions()
    {
        var userId = Guid.Parse(_httpContextAccessor.HttpContext.User.FindFirst("nameid").Value);
        return await _permissionService.GetUserPermissionsAsync(userId);
    }

    public async Task<bool> CanDeleteEmployee(Guid employeeId)
    {
        var userPermissions = await GetCurrentUserPermissions();
        
        // Verifica se tem permissão específica OU wildcard
        return Permissions.HasPermission(userPermissions, Permissions.Employees.Delete);
        
        // Se usuário tem "employees.*", retorna true
        // Se usuário tem "employees.delete", retorna true
        // Senão, retorna false
    }

    public async Task DeleteEmployee(Guid employeeId)
    {
        if (!await CanDeleteEmployee(employeeId))
            throw new UnauthorizedAccessException("Você não tem permissão para excluir funcionários");

        // ... lógica de exclusão
    }
}
```

---

## 3. Criar Políticas com Wildcards

### Scenario: Endpoint protegido por policy

Os wildcards funcionam **automaticamente** nos controllers! Não precisa mudar nada:

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    // Usuário com "products.*" PODE acessar este endpoint
    // Usuário com "products.view" PODE acessar este endpoint
    // Usuário sem nenhuma das duas NÃO PODE
    [HttpGet]
    [Authorize(Policy = Permissions.Products.View)]
    public async Task<ActionResult<List<ProductDto>>> GetAll()
    {
        // ...
    }

    // Usuário com "products.*" PODE acessar
    // Usuário com "products.create" PODE acessar
    // Outros NÃO PODEM
    [HttpPost]
    [Authorize(Policy = Permissions.Products.Create)]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductDto dto)
    {
        // ...
    }
}
```

**Como funciona:**
O `PermissionAuthorizationHandler` usa `Permissions.HasPermission()` que verifica wildcards automaticamente.

---

## 4. Adicionar Nova Permissão

### Scenario: Novo módulo "Sales" (Vendas)

#### Passo 1: Adicionar constantes no `Permissions.cs`

```csharp
// BiomePampa.Domain/Authorization/Permissions.cs

public static class Sales
{
    public const string All = "sales.*";
    public const string View = "sales.view";
    public const string Create = "sales.create";
    public const string Edit = "sales.edit";
    public const string Delete = "sales.delete";
    public const string Approve = "sales.approve";
    public const string ViewReports = "sales.view_reports";
}
```

#### Passo 2: Adicionar em `GetAllPermissions()`

```csharp
public static IReadOnlyCollection<string> GetAllPermissions()
{
    return new[]
    {
        // ... existentes

        // Sales
        Sales.View,
        Sales.Create,
        Sales.Edit,
        Sales.Delete,
        Sales.Approve,
        Sales.ViewReports
    };
}
```

#### Passo 3: Adicionar nas roles padrão (opcional)

```csharp
public static IReadOnlyCollection<string> Funcionario => new[]
{
    // ... existentes
    
    // Sales - funcionários podem criar e visualizar vendas
    Sales.View,
    Sales.Create
};

public static IReadOnlyCollection<string> Admin => GetAllPermissions(); // já inclui automaticamente
```

#### Passo 4: Rodar aplicação

O seed do `DbInitializer` criará as novas permissões automaticamente!

#### Passo 5: Usar no controller

```csharp
[HttpGet]
[Authorize(Policy = Permissions.Sales.View)]
public async Task<ActionResult<List<SaleDto>>> GetSales()
{
    // ...
}

[HttpPost("{id}/approve")]
[Authorize(Policy = Permissions.Sales.Approve)]
public async Task<ActionResult> ApproveSale(Guid id)
{
    // ...
}
```

---

## 5. Auditoria Programática

### Scenario: Conceder permissão via código (não API)

```csharp
public class CustomPermissionManager
{
    private readonly IPermissionService _permissionService;

    public async Task PromoteUserToManager(Guid userId, string userEmail)
    {
        // Concede wildcard de employees
        await _permissionService.GrantPermissionToRoleAsync(
            roleName: "Funcionario",
            permissionName: Permissions.Employees.All,
            performedByUserId: userId,
            performedByUserEmail: userEmail,
            reason: "Promoção a gerente - acesso completo a funcionários",
            cancellationToken: default
        );
        
        // Auditoria foi registrada automaticamente!
    }

    public async Task DemoteUser(Guid userId, string userEmail)
    {
        // Revoga wildcard
        await _permissionService.RevokePermissionFromRoleAsync(
            roleName: "Funcionario",
            permissionName: Permissions.Employees.All,
            performedByUserId: userId,
            performedByUserEmail: userEmail,
            reason: "Rebaixamento - removido acesso de gerente",
            cancellationToken: default
        );
        
        // Auditoria registrou: REVOKED
    }
}
```

### Verificar auditoria depois

```csharp
public async Task<List<PermissionAuditDto>> GetUserActions(string userEmail)
{
    var allLogs = await _permissionService.GetAuditLogsAsync(take: 1000);
    
    return allLogs
        .Where(log => log.PerformedByUserEmail == userEmail)
        .OrderByDescending(log => log.PerformedAt)
        .ToList();
}
```

---

## 6. Frontend - Verificar Wildcards

### Scenario: Angular - Mostrar botões condicionalmente

```typescript
// permission.service.ts
import { Injectable } from '@angular/core';
import jwt_decode from 'jwt-decode';

interface TokenPayload {
  permission: string[];
  // ... outros campos
}

@Injectable({ providedIn: 'root' })
export class PermissionService {
  
  getPermissions(): string[] {
    const token = localStorage.getItem('token');
    if (!token) return [];
    
    const decoded = jwt_decode<TokenPayload>(token);
    return decoded.permission || [];
  }

  // Verifica wildcard (mesma lógica do backend)
  private matchesWildcard(userPermission: string, requiredPermission: string): boolean {
    if (userPermission === requiredPermission) return true;
    
    if (!userPermission.endsWith('.*')) return false;
    
    const module = userPermission.slice(0, -2); // Remove ".*"
    return requiredPermission.startsWith(module + '.');
  }

  // Método principal
  hasPermission(requiredPermission: string): boolean {
    const userPermissions = this.getPermissions();
    return userPermissions.some(up => this.matchesWildcard(up, requiredPermission));
  }

  // Helper para múltiplas permissões (OR)
  hasAnyPermission(...requiredPermissions: string[]): boolean {
    return requiredPermissions.some(p => this.hasPermission(p));
  }

  // Helper para múltiplas permissões (AND)
  hasAllPermissions(...requiredPermissions: string[]): boolean {
    return requiredPermissions.every(p => this.hasPermission(p));
  }
}
```

### Uso no componente

```typescript
// employee-list.component.ts
export class EmployeeListComponent {
  constructor(private permissionService: PermissionService) {}

  // Propriedades computed
  get canViewEmployees(): boolean {
    return this.permissionService.hasPermission('employees.view');
  }

  get canCreateEmployee(): boolean {
    return this.permissionService.hasPermission('employees.create');
  }

  get canEditEmployee(): boolean {
    return this.permissionService.hasPermission('employees.edit');
  }

  get canDeleteEmployee(): boolean {
    return this.permissionService.hasPermission('employees.delete');
  }

  // Ou verificar wildcard diretamente
  get hasFullEmployeeAccess(): boolean {
    return this.permissionService.hasPermission('employees.*');
  }
}
```

### Template

```html
<!-- employee-list.component.html -->

<!-- Botão criar - aparece se tem employees.create OU employees.* -->
<button 
  *ngIf="canCreateEmployee"
  (click)="openCreateModal()">
  Novo Funcionário
</button>

<!-- Tabela -->
<table>
  <tr *ngFor="let employee of employees">
    <td>{{ employee.name }}</td>
    <td>
      <!-- Botão editar -->
      <button 
        *ngIf="canEditEmployee"
        (click)="edit(employee)">
        Editar
      </button>

      <!-- Botão excluir -->
      <button 
        *ngIf="canDeleteEmployee"
        (click)="delete(employee)">
        Excluir
      </button>
    </td>
  </tr>
</table>

<!-- Badge de acesso completo -->
<div *ngIf="hasFullEmployeeAccess" class="badge">
  🔓 Acesso Completo ao Módulo
</div>
```

### Diretiva customizada

```typescript
// has-permission.directive.ts
import { Directive, Input, TemplateRef, ViewContainerRef, OnInit } from '@angular/core';
import { PermissionService } from './permission.service';

@Directive({
  selector: '[hasPermission]'
})
export class HasPermissionDirective implements OnInit {
  @Input() hasPermission!: string;

  constructor(
    private templateRef: TemplateRef<any>,
    private viewContainer: ViewContainerRef,
    private permissionService: PermissionService
  ) {}

  ngOnInit() {
    if (this.permissionService.hasPermission(this.hasPermission)) {
      this.viewContainer.createEmbeddedView(this.templateRef);
    } else {
      this.viewContainer.clear();
    }
  }
}
```

**Uso:**
```html
<button *hasPermission="'employees.create'">Criar</button>
<button *hasPermission="'employees.*'">Admin</button>
```

---

## 7. Exemplo Completo: Novo Módulo "Reports"

### Backend

```csharp
// 1. Permissions.cs
public static class Reports
{
    public const string All = "reports.*";
    public const string View = "reports.view";
    public const string Create = "reports.create";
    public const string Export = "reports.export";
    public const string Schedule = "reports.schedule";
}

// 2. GetAllPermissions()
Reports.View,
Reports.Create,
Reports.Export,
Reports.Schedule

// 3. Controller
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.Reports.View)]
    public async Task<ActionResult<List<ReportDto>>> GetAll() { }

    [HttpPost]
    [Authorize(Policy = Permissions.Reports.Create)]
    public async Task<ActionResult<ReportDto>> Create([FromBody] CreateReportDto dto) { }

    [HttpPost("{id}/export")]
    [Authorize(Policy = Permissions.Reports.Export)]
    public async Task<ActionResult<byte[]>> Export(Guid id) { }

    [HttpPost("{id}/schedule")]
    [Authorize(Policy = Permissions.Reports.Schedule)]
    public async Task<ActionResult> Schedule(Guid id, [FromBody] ScheduleDto dto) { }
}
```

### Frontend

```typescript
// reports.service.ts
export class ReportsService {
  canViewReports = this.permissionService.hasPermission('reports.view');
  canCreateReports = this.permissionService.hasPermission('reports.create');
  canExport = this.permissionService.hasPermission('reports.export');
  canSchedule = this.permissionService.hasPermission('reports.schedule');
  
  hasFullReportAccess = this.permissionService.hasPermission('reports.*');
}

// reports.component.html
<div class="reports-container">
  <!-- Menu lateral -->
  <nav>
    <a routerLink="/reports" *hasPermission="'reports.view'">
      📊 Visualizar Relatórios
    </a>
    <a routerLink="/reports/create" *hasPermission="'reports.create'">
      ➕ Criar Relatório
    </a>
    <a routerLink="/reports/scheduled" *hasPermission="'reports.schedule'">
      📅 Agendamentos
    </a>
  </nav>

  <!-- Toolbar de ações -->
  <div class="toolbar">
    <button 
      *hasPermission="'reports.export'"
      (click)="exportReport()">
      📥 Exportar
    </button>
    
    <button 
      *hasPermission="'reports.schedule'"
      (click)="scheduleReport()">
      ⏰ Agendar
    </button>
  </div>
</div>
```

---

## 📝 Resumo de Boas Práticas

### ✅ DO (Faça)

1. **Use wildcards para acesso total a módulos**
   ```csharp
   await AssignPermission("employees.*"); // Melhor que 5 permissões individuais
   ```

2. **Sempre forneça `reason` ao conceder/revogar**
   ```csharp
   await GrantPermission(..., reason: "Promoção a supervisor");
   ```

3. **Verifique permissões com `Permissions.HasPermission()`**
   ```csharp
   if (Permissions.HasPermission(userPermissions, Permissions.Employees.Delete))
   ```

4. **Use constantes de `Permissions` class**
   ```csharp
   [Authorize(Policy = Permissions.Employees.View)] // Correto
   ```

5. **Frontend: Oculte E valide no backend**
   ```typescript
   *hasPermission="'employees.delete'" // UI
   [Authorize(Policy = "employees.delete")] // Backend
   ```

### ❌ DON'T (Não Faça)

1. **Não use strings hardcoded**
   ```csharp
   [Authorize(Policy = "employees.view")] // Errado! Use Permissions.Employees.View
   ```

2. **Não confie apenas no frontend**
   ```typescript
   // Frontend esconde botão, mas backend DEVE validar
   ```

3. **Não esqueça de chamar `GetAllPermissions()` após adicionar nova permissão**
   ```csharp
   // Se adicionar Sales.View, adicione também em GetAllPermissions()
   ```

4. **Não conceda permissões sem auditoria**
   ```csharp
   // Sempre use IPermissionService, não manipule RolePermissions diretamente
   ```

5. **Não crie wildcard sem criar permissões específicas**
   ```csharp
   // Se criar "sales.*", DEVE criar sales.view, sales.create, etc.
   ```

---

## 🎓 Casos de Uso Avançados

### 1. Permissões Condicionais

```csharp
public async Task<bool> CanEditEmployee(Guid employeeId, Guid currentUserId)
{
    var userPermissions = await GetUserPermissions(currentUserId);
    
    // Tem wildcard ou permissão específica?
    if (!Permissions.HasPermission(userPermissions, Permissions.Employees.Edit))
        return false;
    
    // Regra de negócio adicional: só pode editar se não for outro admin
    var employee = await _context.Employees.FindAsync(employeeId);
    var roles = await _userManager.GetRolesAsync(employee.User);
    
    if (roles.Contains("Administrador"))
        return false; // Admin não pode editar outro admin
    
    return true;
}
```

### 2. Herdar Permissões de Grupos

```csharp
// Futuro: Implementar grupos
public async Task<IReadOnlyCollection<string>> GetUserPermissionsWithGroups(Guid userId)
{
    var directPermissions = await _permissionService.GetUserPermissionsAsync(userId);
    var groupPermissions = await GetPermissionsFromUserGroups(userId);
    
    return directPermissions.Union(groupPermissions).ToList();
}
```

### 3. Permissões Temporárias

```csharp
// Adicionar campo ExpiresAt em RolePermission
public class RolePermission
{
    // ... campos existentes
    public DateTime? ExpiresAt { get; set; }
}

// Verificar expiração
public async Task<IReadOnlyCollection<string>> GetActivePermissions(Guid userId)
{
    var allPermissions = await _permissionService.GetUserPermissionsAsync(userId);
    
    var activePermissions = await _context.RolePermissions
        .Where(rp => rp.Role.Users.Any(u => u.Id == userId))
        .Where(rp => rp.ExpiresAt == null || rp.ExpiresAt > DateTime.UtcNow)
        .Select(rp => rp.Permission.Name)
        .ToListAsync();
    
    return activePermissions;
}
```

---

✅ **Sistema pronto para produção!**

# Tenant User Creation

**Date:** 2026-05-08  
**Status:** Approved

## Problem

A tenant admin can add existing users to their tenant but has no way to create new users. The only user creation paths are public self-registration (`POST /api/auth/register`) and system-admin registration (`POST /api/auth/register/admin`), neither of which is accessible from the tenant management UI.

## Goal

Allow a tenant Owner or Admin to create a new platform user and immediately add them to the current tenant in a single action, without leaving the tenant management screen.

## Approach

Extend the existing `AddMemberDialogComponent` with an inline "create" mode that activates when an email lookup finds no existing user. A single new backend endpoint handles creation + tenant membership in a single request.

---

## Backend

### New DTO

File: `SaaS_BasePlatform.Application/DTOs/Tenant/CreateTenantUserDto.cs`

```csharp
public record CreateTenantUserDto(
    string Email,
    string Password,
    string FullName,
    string? PhoneNumber,
    TenantRole Role
);
```

### New service method

`ITenantService` and `TenantService`:

```csharp
Task<TenantMemberDto> CreateAndAddMemberAsync(
    Guid tenantId, CreateTenantUserDto dto, CancellationToken cancellationToken = default);
```

Implementation steps:
1. Check that no `ApplicationUser` with `dto.Email` already exists — throw `InvalidOperationException` if found.
2. Create `ApplicationUser` via `UserManager.CreateAsync(user, dto.Password)` — surface Identity errors as `InvalidOperationException` on failure.
3. Assign the "Usuario" system role via `UserManager.AddToRoleAsync` (same as public registration).
4. Call existing `AddMemberAsync(tenantId, user.Id, dto.Role, ct)`.
5. Return a `TenantMemberDto` with the new user's info and join timestamp.

No explicit transaction is needed: if `AddMemberAsync` fails after `CreateAsync` succeeds, the user exists but is not a tenant member. The caller receives a 500 and can retry the add-member flow separately. This is acceptable — orphaned users are harmless and can be added to tenants later.

### New endpoint

`TenantsController`:

```
POST /api/tenants/{tenantId}/users
Body: CreateTenantUserDto
Authorization: JWT bearer; caller must be Owner or Admin of the tenant (same guard as the existing add-member endpoint)
Response 201: TenantMemberDto
Response 400: Identity or validation errors
Response 403: Caller is not Owner/Admin
```

---

## Frontend (SaaSBasePlatform-Angular)

### New model

`src/app/core/models/index.ts` — add:

```typescript
export interface CreateTenantUserRequest {
  email: string;
  password: string;
  fullName: string;
  phoneNumber?: string;
  role: TenantRole;
}
```

### ApiService

Add method to `src/app/core/services/api.service.ts`:

```typescript
createTenantUser(tenantId: string, data: CreateTenantUserRequest): Observable<TenantMember>
// POST /api/tenants/{tenantId}/users
```

### AddMemberDialogComponent

File: `src/app/modules/admin/tenant-members/add-member-dialog.component.ts`

Add a `mode: 'lookup' | 'create'` state. Existing lookup flow is unchanged.

**State 1 — lookup (existing behavior):**
- Admin enters email and clicks "Look Up User".
- If user found → existing flow (show name, pick role, add).
- If user not found → show "No user found" message and a **"+ Create new user with this email"** button.
- Clicking that button switches to `mode = 'create'`.

**State 2 — create (new):**
- Email field is locked/pre-filled from State 1.
- Fields: Full Name (required), Password (required), Phone (optional), Tenant Role (required; Member or Admin only — Owner excluded).
- **"Create & Add"** button calls `ApiService.createTenantUser()`.
- On success: dialog closes, parent `TenantMembersComponent` refreshes the member list.
- **"Cancel"** returns to State 1 (lookup mode), preserving the email.

---

## Error Handling

| Scenario | Behavior |
|---|---|
| Email already exists (race) | Backend 400 → inline error: "A user with this email already exists" |
| Weak/invalid password (Identity) | Backend 400 with error list → shown under password field |
| `AddMemberAsync` fails after user created | Backend 500 → generic error toast in Angular |
| Network/timeout | Angular error interceptor → error toast |

---

## Out of Scope

- Email invite flow (user sets their own password) — no email infrastructure exists.
- Force password change on first login.
- Creating users outside of a tenant context.
- Editing user profile after creation.

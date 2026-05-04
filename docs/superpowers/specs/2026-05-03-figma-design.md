# Figma Design Mapping Spec
**Date:** 2026-05-03
**Status:** Approved — ready for implementation

## Goal

Map the existing Angular 18 frontend (`SaaSBasePlatform-Angular`) into a Figma design file so that new screens can be designed in Figma first, then handed off to the Angular codebase.

## Angular Frontend Location

`C:\Users\Nickolas\source\repos\SaaSBasePlatform-Angular`

- Framework: Angular 18 (standalone components)
- Styling: Angular Material 18 (Deep Purple Amber theme) + SCSS + CSS custom properties
- Font: Roboto
- Icons: Material Icons
- No Tailwind

## Chosen Approach: Design Tokens + Key Screens (A)

1. Import all CSS variables from `src/styles.scss` as Figma variables
2. Extract the app shell (toolbar + sidenav) as reusable Figma components
3. Recreate all 10 main screens as 1440×900 hi-fi frames

## Figma File Structure

```
SaaSBasePlatform Design
├── 🎨 Variables
│   ├── Colors
│   ├── Gradients
│   ├── Spacing
│   ├── Radii
│   └── Shadows
├── 🧩 Components
│   ├── App Shell (Toolbar + Sidenav)
│   ├── Nav Item (default / hover / active states)
│   └── User Avatar + Trigger
└── 📱 Screens (1440×900)
    ├── Auth / Login
    ├── Auth / Tenant Selection
    ├── Dashboard
    ├── Accounts Payable / List
    ├── Accounts Payable / Form
    ├── Finance / Chart of Accounts
    ├── Finance / General Ledger
    ├── Admin / Permissions
    ├── Admin / Users & Roles
    └── Admin / Tenant Management
```

## Design Tokens (from `src/styles.scss`)

### Colors
| Token | Value |
|-------|-------|
| color-primary | `#673ab7` |
| color-primary-600 | `#5e35b1` |
| color-primary-700 | `#512da8` |
| color-accent | `#f5576c` |
| color-warn | `#ff6f00` |
| color-success | `#16a34a` |
| color-info | `#0284c7` |
| color-bg | `#f4f6fb` |
| color-surface | `#ffffff` |
| color-surface-alt | `#f8fafc` |
| color-border | `#e6ebf2` |
| color-border-strong | `#cfd6e2` |
| color-text | `#1f2937` |
| color-text-muted | `#5b6577` |
| color-text-subtle | `#8a93a4` |

### Gradients
| Token | Value |
|-------|-------|
| gradient-primary | `linear-gradient(135deg, #667eea 0%, #764ba2 100%)` |
| gradient-accent | `linear-gradient(135deg, #f093fb 0%, #f5576c 100%)` |
| gradient-surface | `linear-gradient(180deg, #ffffff 0%, #fafbff 100%)` |

### Spacing
| Token | Value |
|-------|-------|
| space-1 | `4px` |
| space-2 | `8px` |
| space-3 | `12px` |
| space-4 | `16px` |
| space-5 | `20px` |
| space-6 | `24px` |
| space-8 | `32px` |

### Radii
| Token | Value |
|-------|-------|
| radius-sm | `6px` |
| radius-md | `10px` |
| radius-lg | `16px` |

### Shadows
| Token | Value |
|-------|-------|
| shadow-sm | `0 1px 2px rgba(15,23,42,0.05)` |
| shadow-md | `0 4px 12px rgba(15,23,42,0.08)` |
| shadow-lg | `0 12px 32px rgba(15,23,42,0.12)` |

## App Shell Details (from `layout.component`)

### Toolbar (fixed, 64px height)
- Background: `gradient-primary` (`135deg #5e35b1 → #764ba2`)
- Shadow: `0 2px 12px rgba(15,23,42,0.15)`
- Left: hamburger toggle → logo icon (hub, 34×34px, `rgba(255,255,255,0.18)` bg, radius 9px) → "SaaS_BasePlatform" text (17px, 600 weight, white)
- Right: User trigger pill (44px height, `rgba(255,255,255,0.1)` bg, radius 999px) — avatar (34px circle, gradient-accent) + name/email + chevron

### Sidenav (260px wide, `color-surface` bg, `color-border` right border)
- Padding: `12px 8px`
- Section headers: 11px, uppercase, 600 weight, `color-text-subtle`, letter-spacing 0.6px
- Nav items: 44px height, radius 10px, `color-text-muted`
  - Hover: `color-surface-alt` bg, `color-text` text
  - Active: `rgba(102,126,234,0.12) → rgba(118,75,162,0.12)` gradient bg, `color-primary-700` text, icon `color-primary`
- Content area: `margin-top: 64px`, `color-bg` background

### Menu Sections & Items
| Section | Items |
|---------|-------|
| Principal | Dashboard (dashboard) |
| Contas a Pagar | Lançamentos (receipt_long), Nova Entrada (add_circle_outline) |
| Financeiro | Plano de Contas (account_tree), Razão Geral (menu_book) |
| Administração | Permissões (admin_panel_settings), Usuários e Papéis (manage_accounts), Tenant (business) |

## Screens to Recreate

### 1. Auth / Login (`/auth/login`)
- Full-page centered card on gradient bg with decorative orbs
- Card: logo icon + "SaaS_BasePlatform ERP" h1 + subtitle
- Form fields: Email (mail_outline prefix), Senha (lock_outline prefix), Tenant slug (business prefix, optional)
- Primary flat button "Entrar" with loading spinner state

### 2. Auth / Tenant Selection (`/auth/select-tenant`)
- Post-login screen for selecting which tenant to enter

### 3. Dashboard (`/dashboard`)
- Summary metric cards + recent activity

### 4. Accounts Payable / List (`/accounts-payable`)
- Data table with pagination, filters, action buttons

### 5. Accounts Payable / Form (`/accounts-payable/new` and `/:id/edit`)
- Form with AP entry fields + category picker

### 6. Finance / Chart of Accounts (`/finance/chart-of-accounts`)
- Tree/hierarchy view of GL accounts

### 7. Finance / General Ledger (`/finance/general-ledger`)
- Journal entries list + account statement view

### 8. Admin / Permissions (`/admin/permissions`)
- Permission catalog + role assignment

### 9. Admin / Users & Roles (`/admin/users-roles`)
- User list + role assignment table

### 10. Admin / Tenant Management (`/admin/tenant`)
- Tenant info, members, API keys

## Implementation Steps (next session)

1. Fetch schema for Figma MCP tools (`use_figma`, `create_new_file`, `generate_diagram`)
2. Create new Figma design file named "SaaSBasePlatform Design"
3. Set up Figma variable library from the token table above
4. Read each component's `.html` + `.scss` to reconstruct the screen layout
5. Create frames for all 10 screens
6. Verify with user and iterate

## Source Files to Read Per Screen

| Screen | HTML | SCSS |
|--------|------|------|
| Login | `src/app/modules/auth/login/login.component.html` | `login.component.scss` |
| Tenant Selection | `src/app/modules/auth/tenant-selection/tenant-selection.component.html` | `tenant-selection.component.scss` |
| Dashboard | `src/app/modules/dashboard/dashboard.component.html` | `dashboard.component.scss` |
| AP List | `src/app/modules/accounts-payable/list/accounts-payable-list.component.html` | `accounts-payable-list.component.scss` |
| AP Form | `src/app/modules/accounts-payable/form/accounts-payable-form.component.html` | `accounts-payable-form.component.scss` |
| Chart of Accounts | `src/app/modules/finance/chart-of-accounts/chart-of-accounts.component.html` | `chart-of-accounts.component.scss` |
| General Ledger | `src/app/modules/finance/general-ledger/general-ledger.component.html` | `general-ledger.component.scss` |
| Permissions | `src/app/modules/admin/permissions-management.component.html` | `permissions-management.component.scss` |
| Users & Roles | `src/app/modules/admin/users-roles-management.component.html` | `users-roles-management.component.scss` |
| Tenant Mgmt | `src/app/modules/admin/tenant-management.component.html` | `tenant-management.component.scss` |

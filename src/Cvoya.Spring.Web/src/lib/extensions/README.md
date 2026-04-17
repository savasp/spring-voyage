# Portal extensions

> Tracking: [#440](https://github.com/cvoya-com/spring-voyage/issues/440).

The Spring Voyage portal is the **open-source core**. A private
repository extends it via DI-style registration and git submodule to
add multi-tenancy, OAuth/SSO, billing, RBAC, and premium surfaces.

This package is the seam between the two. OSS imports **nothing** from
the private repo; the private repo imports `@/lib/extensions` and
supplies a typed bundle at app startup. The sidebar and command
palette consume the merged view — there is no hard-coded route list
or hard-coded auth assumption anywhere in the OSS shell.

## What extensions can provide

```ts
import { registerExtension } from "@/lib/extensions";
import { Building2, UserPlus } from "lucide-react";

registerExtension({
  id: "spring-voyage-cloud",
  routes: [
    {
      path: "/tenants",
      label: "Tenants",
      icon: Building2,
      navSection: "settings",
      permission: "tenants.read",
      orderHint: 10,
      keywords: ["workspace", "organization"],
      description: "Switch between tenants and view billing.",
    },
  ],
  actions: [
    {
      id: "tenant.invite",
      label: "Invite teammate",
      icon: UserPlus,
      section: "hosted",
      permission: "tenants.manage",
      href: "/tenants/invite",
    },
  ],
  auth: myAuthAdapter,
  decorators: [myTenantHeaderDecorator],
  slots: {
    topBarRight: <TenantSwitcher />,
  },
});
```

### Route manifest (`RouteEntry[]`)

Every navigable surface the portal exposes — core or extension —
appears here. The sidebar groups entries by `navSection` and sorts
within each section by `orderHint`. The palette indexes every entry
plus its `keywords` for fuzzy search.

Fields:

- `path` — concrete App Router path (`/units`, not `/units/[id]`).
- `label` — short human-readable name.
- `icon` — any `ComponentType<{ className?: string }>`.
- `navSection` — `"primary"` or `"settings"` (extendable).
- `permission` — optional capability gate; omit for OSS-always-on.
- `orderHint` — lower numbers sort first.
- `keywords`, `description` — palette metadata.

### Palette actions (`PaletteAction[]`)

Command-palette-only entries. Use these for verbs that don't deserve
a sidebar slot ("Rotate secret", "Send message"). Each action either
navigates via `href` or invokes `onSelect()`.

### Auth adapter (`IAuthContext`)

OSS's default reports
`{ id: "local", displayName: "local" }` and returns `true` for every
`hasPermission(...)` call. Hosted replaces the adapter with an
OAuth-backed implementation. **At most one extension may set `auth`**
— the registry throws if a second one tries.

### API-client decorators (`ClientDecorator[]`)

A decorator wraps a fetch-like callable and returns a wrapped one.
Use them to attach bearer tokens, tenant headers, CSRF tokens, etc.

```ts
import { withDecorators, authHeadersDecorator } from "@/lib/extensions";

const decorated = withDecorators(fetch, [authHeadersDecorator(auth)]);
const res = await decorated("/api/v1/units");
```

Decorators compose right-to-left (outermost wrapper first).

### Shell slots (`ShellSlot`)

Named React nodes rendered into pre-defined holes in the portal
shell. v1 exposes `topBarLeft`, `topBarRight`, `sidebarFooter`,
`settingsNav`, `unitDetailHeader`. OSS renders `null` for each; the
hosted build fills them in.

## Contract rules

1. **Nothing in OSS may reference `tenantId`.** Anything tenant-aware
   lives in the hosted extension or behind the auth adapter.
2. **Append, don't reorder.** New `NavSection` values, new `ShellSlot`
   values, and new fields on `RouteEntry` / `PaletteAction` are
   additive. Renaming or removing existing ones is a breaking change.
3. **At most one auth adapter.** Multiple extensions may add routes,
   actions, decorators, and slots — only one may claim `auth`.
4. **Registration is side-effecting but idempotent by id.** Calling
   `registerExtension({ id: "x", ... })` twice replaces the first
   registration (so HMR and re-renders stay sane). Tests can use
   `__resetExtensionsForTesting()` from `./registry`.
5. **Every extension point must have a sensible OSS default.** Don't
   expose a slot the shell wouldn't render without the hosted build.

## Consumer surfaces

- `@/components/sidebar` consumes `useRoutes()` to render the nav.
- `@/components/command-palette` consumes both `useRoutes()` and
  `usePaletteActions()` to build its index.
- API call sites wrap the shared client via `withDecorators(fetch,
  useExtensions().decorators)` where they need per-request header
  injection.

## Testing

Tests that need a known registry state should wrap the component
under test in `<ExtensionProvider override={{ ... }}>` or call
`__resetExtensionsForTesting()` in a `beforeEach` and then
`registerExtension({ id: "test", ... })` before rendering.

## Related

- Portal plan: [`docs/design/portal-exploration.md`](../../../../../docs/design/portal-exploration.md) § 4.
- Command palette issue: [#439](https://github.com/cvoya-com/spring-voyage/issues/439).
- Extensibility rules: `AGENTS.md` § "Open-Source Platform & Extensibility".

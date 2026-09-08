# OAuth 2.1 Authentication (PolarionRemoteMcpServer)

`PolarionRemoteMcpServer` can require a bearer token issued by an external OAuth 2.1 authorization
server, instead of accepting anonymous `POST /{alias}/mcp` calls. It's opt-in (`McpAuth:Enabled`,
default `false`) — with it unset, the server's behavior is unchanged from every prior release.

This page covers the `PolarionRemoteMcpServer` (Docker / HTTP) deployment only. The stdio
`PolarionMcpServer` console app has no concept of a caller and is unaffected by anything here.

## Before you start

### What this server does and does not do

- It validates every bearer token against the authorization server (AS) you configure — checking
  signature, issuer, audience, expiry, and (by default) a `polarion:read` scope — before a tool
  call is allowed to run.
- It does not run its own authorization server, issue tokens, or store credentials for your users.
  You bring an existing AS (Okta, Auth0, Keycloak, an internal OIDC provider, …).
- It does not change *whose* Polarion credentials a tool call uses — see
  [Credentials](#upstream-polarion-credentials) below; by default every call still authenticates
  upstream as the project's configured service account. To authorize each call against the
  *calling* user's own Polarion project membership instead, see [Per-Caller RBAC](rbac.md), a
  separate, additional opt-in.
- Once enabled, it publishes [RFC 9728](https://www.rfc-editor.org/rfc/rfc9728) protected-resource
  metadata **per served project alias**, at `/.well-known/oauth-protected-resource/{alias}/mcp`, so
  a conforming MCP client can discover where to authenticate without being told out of band.

### What your authorization server must support

Not every authorization server can mint a per-resource audience or grant a custom scope. Answer
these three questions before configuring anything — they decide which quick start below applies:

| Can your AS… | Yes | No |
|---|---|---|
| mint an access token whose `aud` is *this server's per-alias resource*? | leave `ValidateAudience` at its default (`true`) | `ValidateAudience=false`, **plus** `AllowedClientIds` |
| grant a custom scope (`polarion:read`)? | leave `RequireScope`/`ScopesSupported` at their defaults | `RequireScope=false`, and set `ScopesSupported` to what it *can* grant, or `AdvertiseScopes=false` |
| put the caller's email on the access token? | (only relevant if you also enable RBAC) `Rbac:IdentitySource=Claim` | `Rbac:IdentitySource=UserInfo` — see [rbac.md](rbac.md) |

An Okta **Custom** Authorization Server answers "yes" to both rows; an Okta **org** authorization
server answers "no" to both, because it always stamps `aud` with its own issuer and cannot grant a
custom scope. If you don't know which you have, start with the first quick start below and let the
startup validator tell you if it's wrong.

### One server, several project aliases, one resource each

`McpAuth:ResourceUri` is the deployment's **base URL only** — no alias, no trailing `/mcp` (e.g.
`https://polarion-mcp.example.invalid`). The server derives each served alias's actual RFC 9728
`resource` value and JWT `aud` as `{ResourceUri}/{alias}/mcp`. A `ResourceUri` that already ends in
`/mcp` **fails startup** — it would double up into `.../mcp/{alias}/mcp` once the alias is appended.

Adding a new project alias to `PolarionProjects` silently widens the set of audiences the server
will accept: a token minted for `starlight` also passes `JwtBearer`'s audience check on
`octopus`'s `/mcp` endpoint, because `ValidAudiences` is every configured alias's resource, not one
fixed value. **Alias isolation comes from `Rbac`, not from the audience.** If you need one alias's
tokens to be unusable against another, enable [RBAC](rbac.md).

## Quick start A: a spec-conforming authorization server

Assumes your AS can mint a token with `aud` set to this server's per-alias resource and can grant
`polarion:read`.

```json
{
  "McpAuth": {
    "Enabled": true,
    "Issuer": "https://issuer.example.invalid/oauth2/<asid>",
    "ResourceUri": "https://polarion-mcp.example.invalid"
  }
}
```

Every other `McpAuth` key stays at its default: `ScopesSupported=["polarion:read"]`,
`AdvertiseScopes=true`, `ValidateAudience=true`, `RequireScope=true`.

AS obligations for this shape:

- Tokens signed `RS256`.
- `aud` equal to `{ResourceUri}/{alias}/mcp` for every alias you serve — e.g.
  `https://polarion-mcp.example.invalid/starlight/mcp`.
- Able to grant `polarion:read`.
- OIDC discovery and JWKS reachable under `Issuer`.

### Verify

```bash
curl -s https://polarion-mcp.example.invalid/.well-known/oauth-protected-resource/starlight/mcp
```

Confirm `resource` is `https://polarion-mcp.example.invalid/starlight/mcp`, `authorization_servers`
contains `Issuer`, and `scopes_supported` is `["polarion:read"]`. A `POST /starlight/mcp` with no
`Authorization` header should now get a `401` carrying a `WWW-Authenticate` header that points at
this metadata URL, instead of succeeding.

## Quick start B: an org-style authorization server

Assumes your AS always stamps `aud` with its own issuer (never a per-resource value) and cannot
grant a custom scope — the shape of an Okta **org** authorization server. This is also the shape
[RBAC's `/userinfo` identity source](rbac.md#quick-start-rbac-with-identity-from-userinfo) builds
on, so it advertises the OIDC scopes the AS *can* grant (`openid email profile offline_access`)
rather than advertising nothing:

```json
{
  "McpAuth": {
    "Enabled": true,
    "Issuer": "https://issuer.example.invalid",
    "ResourceUri": "https://polarion-mcp.example.invalid",
    "ValidateAudience": false,
    "AllowedClientIds": [ "0oaEXAMPLECLIENTID" ],
    "RequireScope": false,
    "ScopesSupported": [ "openid", "email", "profile", "offline_access" ]
  }
}
```

> [!WARNING]
> **`ValidateAudience=false`** accepts any token your AS issued to an allowlisted client, for any
> purpose — it does not prove the token was minted *for this server*. `AllowedClientIds` is the
> required compensating control, and the server refuses to start if it's empty while
> `ValidateAudience` is `false`.

> [!WARNING]
> **`RequireScope=false`** means a valid token proves *who* the caller is, not *what* they may do —
> scope no longer limits anything. Pair this with [RBAC](rbac.md), or every authenticated caller
> gets the full reach of whatever upstream Polarion credential the call resolves to.

`ScopesSupported` here **replaces** the default (see
[`ScopesSupported` replaces, not appends](#scopessupported-replaces-not-appends) below), so a
client following discovery requests exactly these four OIDC scopes — never the unreachable
`polarion:read`.

### `AdvertiseScopes=false` is incompatible with `Rbac:IdentitySource=UserInfo`

If your AS grants **no** OIDC scopes at all — not even `openid`/`email` — set `AdvertiseScopes` to
`false` instead of the `ScopesSupported` list above, so discovery advertises nothing a client could
request. But do not combine this with [`Rbac:IdentitySource=UserInfo`](rbac.md#identitysourceuserinfo):
an OIDC `/userinfo` endpoint only returns an `email` claim for a token whose grant actually included
the `openid` and `email` scopes. A client following `AdvertiseScopes=false` discovery requests no
scope at all, so the resulting token has neither — every `/userinfo` call then returns no usable
email, and RBAC denies every caller. If you need `IdentitySource=UserInfo`, your AS must be able to
grant at least `openid` and `email`, in which case use the `ScopesSupported` shape above, not
`AdvertiseScopes=false`.

## Configuration reference: `McpAuth`

This table, not the checked-in `appsettings.json`, is the complete key list — that file is a
non-secret starting point, not an exhaustive reference.

| Key | Type | Default |
|---|---|---|
| `Enabled` | bool | `false` |
| `Issuer` | string | `""` |
| `MetadataAddress` | string? | `null` |
| `ResourceUri` | string | `""` (base URL; **no alias, no `/mcp`**) |
| `ScopesSupported` | string list | `["polarion:read"]` (**replaces**, never appends) |
| `AdvertiseScopes` | bool | `true` |
| `ClockSkewSeconds` | int | `30` (range 0–300) |
| `ValidateAudience` | bool | `true` |
| `AllowedClientIds` | string list | `[]` (matched against the token's `cid` claim, **case-sensitive**) |
| `RequireScope` | bool | `true` |

### Non-configurable, worth knowing

- Only `RS256`-signed tokens are accepted — pinned to block alg-confusion attacks and `alg: none`.
- `MapInboundClaims=false`, so the `sub` claim stays `sub` instead of being remapped to ASP.NET's
  default `ClaimTypes.NameIdentifier`.
- `RequireHttpsMetadata` is derived from `Issuer`'s own URI scheme, not from the hosting
  environment — an `http://` issuer is only accepted at all in the Development environment.
- The metadata path (`/.well-known/oauth-protected-resource/{alias}/mcp`) is fixed; there is no key
  to change it.
- `jwks_uri` is deliberately omitted from the published metadata — RFC 9728's `jwks_uri` means
  *resource-response* signing, not token signing.

### `ScopesSupported` replaces, not appends

Configuring `McpAuth:ScopesSupported` **replaces** the default list rather than adding to it —
ordinary .NET `ConfigurationBinder` semantics would otherwise append to the pre-populated
`["polarion:read"]` default, so setting `McpAuth__ScopesSupported__0=openid` would advertise
`["polarion:read", "openid"]` instead of `["openid"]`. This server corrects that: a configured list
is advertised verbatim.

An environment variable also cannot express an empty array — there's no way to write
`McpAuth__ScopesSupported=[]`. That's why `AdvertiseScopes` exists as its own flag: set it to
`false` to publish nothing, rather than trying to configure a blank scope entry (which is dropped
anyway, since a blank string is never a scope a client could legitimately request).

### Setting these as environment variables

`McpAuth:Key` becomes `McpAuth__Key` (double underscore), standard ASP.NET Core configuration
binding. List entries are indexed: `McpAuth__AllowedClientIds__0`, `McpAuth__AllowedClientIds__1`,
`McpAuth__ScopesSupported__0`, …

## Startup validation errors

`McpAuth` is validated at startup, but only when `McpAuth:Enabled=true` — a malformed section can
never break a server that has auth off. Every failure below is accumulated and reported together,
not one at a time.

| Message | Cause | Fix |
|---|---|---|
| `Issuer must be an absolute URI.` | `Issuer` blank or not a full URI | Set `McpAuth:Issuer` to your AS's full issuer URL |
| `Issuer must use https (http is only allowed in the Development environment).` | `Issuer` is `http://` outside Development | Use `https://`, or set `ASPNETCORE_ENVIRONMENT=Development` for local testing only |
| `Issuer must not contain a fragment.` | `Issuer` has a `#` | Remove the fragment (a path, e.g. `/oauth2/<asid>`, is fine) |
| `ResourceUri must be an absolute URI.` | `ResourceUri` blank or not a full URI | Set `McpAuth:ResourceUri` to this server's own base URL |
| `ResourceUri must not contain a fragment.` | `ResourceUri` has a `#` | Remove the fragment |
| `ResourceUri must be a base URL with no alias and no trailing /mcp — the per-alias resource (…/{alias}/mcp) is derived automatically.` | `ResourceUri` ends in `/mcp` | Drop the trailing `/mcp` (and any alias segment) |
| `AllowedClientIds must not be empty when ValidateAudience is false — otherwise a token issued to any client for any resource would be accepted.` | `ValidateAudience=false` with an empty `AllowedClientIds` | Add at least one client id, or leave `ValidateAudience` at `true` |
| `AllowedClientIds must not contain blank entries.` | a blank/whitespace entry in `AllowedClientIds` | Remove the blank entry |
| `MetadataAddress must be an absolute URI.` | `MetadataAddress` is set but not a full URI | Fix the URL, or unset the key to use the default discovery location |
| `MetadataAddress must use https (http is only allowed in the Development environment).` | `MetadataAddress` is `http://` outside Development | Use `https://`, or set `ASPNETCORE_ENVIRONMENT=Development` |
| `ClockSkewSeconds must be between 0 and 300.` | value outside `0`–`300` | Pick a value in range |

## 401 vs 403

- **401 Unauthorized** — an *authentication* failure: no token, or a token that fails signature,
  issuer, audience, or lifetime validation. The response carries a `WWW-Authenticate: Bearer
  resource_metadata="…"` header pointing at this alias's RFC 9728 metadata, so a conforming client
  can start an authorization flow.
- **403 Forbidden** — an *authorization* failure: the token is valid, but is missing the required
  scope, or its `cid` claim isn't on `AllowedClientIds`. Deliberately not a 401 — re-authenticating
  with the same client cannot fix either problem.

## Known gotcha: alias case-sensitivity is inconsistent

Routing and upstream-credential resolution match a project alias with
`StringComparison.OrdinalIgnoreCase`, so `/Starlight/mcp` and `/starlight/mcp` both reach the same
project. The RFC 9728 discovery-metadata handler matches the alias with plain
`StringComparison.Ordinal`, so `/.well-known/oauth-protected-resource/Starlight/mcp` 404s even
though the MCP endpoint itself answers fine at that casing. This is a known inconsistency, flagged
here rather than fixed — treat project aliases as case-sensitive when constructing a discovery URL.

## Upstream Polarion credentials

`Credentials:Mode` controls whose Polarion credential a call actually uses upstream — a separate
question from who is allowed to call the server at all.

| Mode | Behavior |
|---|---|
| `Shared` (default) | Every caller authenticates to Polarion as the project's configured service account — today's behavior, unchanged. |
| `HttpBroker` | Requires `McpAuth:Enabled=true`. Resolves a per-user Polarion credential from the caller's validated `sub` claim by calling an external broker (`Credentials:BrokerUrl`, `Credentials:BrokerApiKey`, `Credentials:BrokerTimeoutSeconds`, range 1–120, default `10`). The broker itself — enrollment UI, credential vault, identity binding — is a separate component **not included in this repository**; only the generic HTTP client for it exists here. |

These are the only two accepted values, matched case-insensitively. **Any other value silently
falls back to `Shared`** rather than failing startup validation — the code's own XML documentation
claims otherwise, but the validator and the resolver-selection logic both only special-case
`"HttpBroker"`; everything else, including a typo like `"HttpBrocker"`, boots clean in `Shared`
mode. Double-check the value if you expect broker mode and calls are still using the shared service
account.

Only `Password`-kind credentials are usable today — the upstream `Polarion.PolarionClientConfiguration`
type has no field to apply an access-token credential to, even though the broker wire contract
already has room for one.

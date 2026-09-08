# Per-Caller RBAC (PolarionRemoteMcpServer)

`Rbac:Enabled` (default `false`) authorizes each `tools/call` against the *calling* user's own
Polarion project membership, instead of every authenticated caller sharing the full reach of
whatever upstream credential the call resolves to. It requires
[OAuth authentication](authentication.md) — `Rbac:Enabled=true` with `McpAuth:Enabled=false` is
refused at startup, because without a validated caller identity there is nothing to authorize.

> [!IMPORTANT]
> RBAC fails closed, and that is not configurable. `FailClosed` is not a settable key — it is
> always true. An unresolved identity, an unmapped alias, or an upstream Polarion error all deny.
> A misconfiguration shows up as every call being denied, not as open access — read
> [Rolling it out](#rolling-it-out) before enabling this in production.

## What RBAC checks

Every `tools/call` is gated uniformly: the server checks whether the resolved caller identity is
an explicit member of the Polarion project mapped to the route alias (`/{alias}/mcp`), via
`ProjectWebService.getProjectUsers`, keyed on the resolved Polarion **username** (`u.id`). There is
no per-tool permission map and no result-filtering — unlike a cross-project tool surface, every
Polarion MCP tool is already scoped to one project by its route, so the resource being checked is
always the route alias, never anything read from the call's own arguments. `tools/list` is not
filtered — every caller sees the same tool list; RBAC only gates whether a `tools/call` runs.

## The ordered decision contract

Read top to bottom. There is no catch-all allow branch:

1. **Gate disabled** (`Rbac:Enabled=false`) → allow, reason `rbac_gate_not_configured`.
2. **Identity did not resolve** → deny, reason is whatever the identity source reported (see the
   [`DecisionReason` table](#decisionreason-values) below) or `identity_unresolved`.
3. **Route alias is blank** → deny, reason `project_alias_missing`.
4. **Otherwise** → the real per-project membership check runs and returns `Allow` or a `Deny` with
   one of the reasons below.

## What the caller sees on deny

```
ERROR: The requested resource was not found.
```

No reason code, no mention of RBAC — a denial is indistinguishable from a genuine 404. All
diagnosis lives in the [audit log](#audit-records), never in the response the caller receives.

## Caller identity

RBAC needs an identity value to join against a Polarion user before it can check project
membership. Two modes, set via `Rbac:IdentitySource`:

### `IdentitySource=Claim` (default)

Reads `Rbac:IdentityClaim` (default `"email"`) directly off the caller's validated access token.
This is the pre-existing, unchanged behavior — no normalization, no guardrails, byte-identical to
how identity resolution has always worked on this path.

### `IdentitySource=UserInfo`

For an authorization server whose access tokens carry no usable identity claim at all (an Okta
**org** authorization server, for example) — without this, identity resolution returns nothing for
every caller and every gated call is denied while the server still reports healthy.

Calls `GET {McpAuth:Issuer}/oauth2/v1/userinfo` with the **caller's own** bearer token, so the
server gains no standing credential against the identity provider and can only ever read the
profile of whoever is calling it. This path is derived from `McpAuth:Issuer`, **not read from OIDC
discovery, and not overridable by any config key** — an authorization server whose `/userinfo`
lives at a different path will boot healthy under this mode and deny every caller.

Guardrails applied only on this path (the `Claim` path is left byte-identical to the original
behavior, deliberately):

- `email_verified` must be `true` in the `/userinfo` response — an unverified address is treated as
  an unauthenticated assertion.
- The address is lowercased before lookup, so one person can't occupy two cache entries or two
  audit identities.
- Plus-addressed (`alice+work@example.invalid`) and other alias forms are **rejected outright, not
  normalized** — resolving `alice+work@` to `alice@`'s Polarion user would grant one person another
  person's authority on a guess.

### Deployment assumption: one Polarion server for identity lookup

Resolving an identity claim value to a Polarion username always logs in using the **default**
project's configured service account, because the underlying `getUsers()` call is server-wide with
no project scope. This assumes every configured project alias's users live on the same Polarion
server as the default alias. Deployments spanning multiple Polarion servers are not supported by
identity resolution today.

## Quick start: RBAC with identity from a token claim

Builds on the [spec-conforming authentication quick start](authentication.md#quick-start-a-a-spec-conforming-authorization-server) — assumes your AS puts an `email` claim on its access tokens.

```json
{
  "Rbac": {
    "Enabled": true
  }
}
```

`Rbac:IdentityClaim` defaults to `"email"` and `Rbac:IdentitySource` defaults to `Claim` — nothing
else to set for this path.

## Quick start: RBAC with identity from `/userinfo`

Builds on the [org-style authentication quick start](authentication.md#quick-start-b-an-org-style-authorization-server) — assumes your AS puts no identity claim on its access tokens.

```json
{
  "Rbac": {
    "Enabled": true,
    "IdentitySource": "UserInfo"
  }
}
```

## Rolling it out

### Stage 1 — `Rbac:AuditOnly=true`

> [!WARNING]
> Audit-only does not enforce anything: every check still runs and is still recorded, but a deny
> never blocks the call.

### Stage 2 — read the access-audit records

See [Audit records](#audit-records) below. Confirm the `Decision`/`DecisionReason` you'd expect
before moving on.

### Stage 3 — enforce

Remove `Rbac:AuditOnly` (or set it to `false`). Denials now block the call.

## Audit records

One record is emitted per `tools/call` while `Rbac:Enabled` is true — Serilog `Information`, fixed
`EventId 90210` / event name `McpAccessAudit`. Fields: `sub` (`OAuthSubject`), `client_id` falling
back to the `cid` claim (`OAuthClientId`), `jti`, the raw identity claim value
(`IdentityClaimValue`), the resolved Polarion username (`PolarionUsername`), `ToolName`,
`ProjectAlias`, `Decision` (`Allow`/`Deny`), `DecisionReason`, `Blocked`, and `ElapsedMilliseconds`
(gate latency only — not the downstream tool body's own latency).

**`Decision` and `DecisionReason` always carry the gate's true verdict, regardless of
`Rbac:AuditOnly`.** `Blocked` is the separate field recording whether the call was actually
stopped. Without this split, audit-only shadow mode would log every would-be deny as an Allow,
producing none of the data it exists to produce.

Privacy note: the subject and identity claim value (typically an email address) are logged by
design — that is the feature's stated purpose (attribution) — but a token, PAT, or `Authorization`
header value is never logged.

## `DecisionReason` values

Denies:

| Reason | Meaning |
|---|---|
| `identity_unresolved` | The claim (or the `/userinfo` email field) simply wasn't present. |
| `identity_no_bearer_token` | `IdentitySource=UserInfo`, but no captured bearer token was available to call `/userinfo` with. |
| `identity_email_unverified` | `/userinfo` returned an email, but `email_verified` was not `true`. |
| `identity_email_rejected_form` | The email failed the guardrails (plus-addressed, malformed, or otherwise not a canonical address) — rejected, not normalized. |
| `identity_userinfo_rate_limited` | **The alertable one.** The authorization server returned HTTP 429 on `/userinfo` — an org-wide shared quota, not a statement about this caller. Logged at `Error`, and denies every RBAC call while it persists. |
| `identity_userinfo_unavailable` | `/userinfo` failed for any other transport, HTTP status, or parsing reason. |
| `project_alias_missing` | The route carried no project alias. |
| `unmapped_project_alias` | The route alias does not match any configured `PolarionProjects` entry. |
| `project_id_missing` | The matched project has no `SessionConfig.ProjectId` configured. |
| `not_a_project_member` | The resolved Polarion username is not in the project's member list. |
| `membership_check_failed` | The upstream `getProjectUsers` call itself failed (login failure, API error, etc.). |

Allows: `rbac_gate_not_configured` (RBAC disabled) or `null` (a real membership match).

## Configuration reference: `Rbac`

This table, not the checked-in `appsettings.json`, is the complete key list.

| Key | Type | Default |
|---|---|---|
| `Enabled` | bool | `false` |
| `AuditOnly` | bool | `false` |
| `IdentityClaim` | string | `"email"` |
| `IdentitySource` | enum (`Claim` \| `UserInfo`) | `Claim` |
| `UserInfoCacheTtlSeconds` | int | `300` |
| `IdentityCacheTtlSeconds` | int | `300` |
| `MembershipCacheTtlSeconds` | int | `120` |
| `MaxCacheEntries` | int | `20000` |

`FailClosed` is not a key — it is always true and cannot be relaxed. `AuditOnly` is the only lever
that changes what happens after a deny is computed.

## Caches, TTLs, and the revocation window

Three `TtlCache` instances share `MaxCacheEntries` as a capacity bound:

| Cache | Key | TTL |
|---|---|---|
| `/userinfo` (`OktaUserInfoEmailSource`) | SHA-256 of the raw token — **never the token itself** | `min(remaining token lifetime, UserInfoCacheTtlSeconds)`, so an entry can never outlive the token it was fetched with |
| Identity (`CachingIdentityResolver`) | the raw identity claim value | `IdentityCacheTtlSeconds` (default `300`) on a positive or ambiguous result; a **fixed 30s** on a not-found result — not configurable, deliberately: without a short negative TTL, an agentic client retrying a denial becomes an unbounded request amplifier against production Polarion |
| Membership (`PolarionProjectUsersGate`) | the real Polarion project id (never the alias) | `MembershipCacheTtlSeconds` (default `120`) — **this is how long a project member removed from Polarion keeps MCP access after removal** |

Not configurable: a 30-second janitor sweep evicts expired entries across all three caches; a
60-second rate limit caps how often a cache-saturation warning is logged; the `/userinfo` HTTP call
itself times out after 10 seconds, so a slow identity provider fails fast into a denial rather than
holding the caller's request open. **Saturation serves the request uncached rather than denying
it** — a full cache must never itself cause a denial.

## Startup validation errors

`Rbac` is validated at startup, but only when `Rbac:Enabled=true`.

| Message | Cause | Fix |
|---|---|---|
| `Rbac:Enabled requires McpAuth:Enabled — without authentication there is no caller identity to check, so every gated call would fail closed for every caller. Enable McpAuth first, or leave Rbac disabled.` | RBAC on, authentication off | Enable `McpAuth` first, or leave `Rbac` disabled |
| `Rbac:IdentityClaim must not be blank.` | `IdentityClaim` is empty | Set it to a real claim name (default `"email"`) — validated even under `IdentitySource=UserInfo`, where it is never actually read |
| `Rbac:IdentityCacheTtlSeconds must be between 1 and 86400.` | value out of range | Pick a value in range |
| `Rbac:IdentitySource=UserInfo requires McpAuth:Issuer — the /userinfo endpoint is derived from it.` | `IdentitySource=UserInfo` with a blank `McpAuth:Issuer` | Set `McpAuth:Issuer` |
| `Rbac:UserInfoCacheTtlSeconds must be between 1 and 86400.` | value out of range | Pick a value in range |
| `Rbac:MembershipCacheTtlSeconds must be between 1 and 86400.` | value out of range | Pick a value in range |
| `Rbac:MaxCacheEntries must be between 1 and 10000000.` | value out of range | Pick a value in range |

## Troubleshooting

**Every tool call is denied, or results come back empty** — RBAC fails closed. Set
`Rbac:AuditOnly=true` and read the access-audit records' `DecisionReason` field; it names exactly
which of these applies:

1. `unmapped_project_alias` / `project_id_missing` — the route alias isn't correctly mapped to a
   configured Polarion project.
2. `identity_unresolved` / `identity_no_bearer_token` / `identity_email_unverified` /
   `identity_email_rejected_form` — the caller's identity never resolved to a value; check the
   token claim or `/userinfo` response.
3. `identity_userinfo_rate_limited` — the authorization server's `/userinfo` quota is being
   throttled; this denies *every* caller until it clears, and is logged at `Error`.
4. `not_a_project_member` — the resolved Polarion username genuinely isn't a member of the target
   project.
5. `membership_check_failed` — the upstream `getProjectUsers` call itself is failing; check
   Polarion server connectivity and the project's `SessionConfig`.

**Server refuses to start with a `Rbac` validation message** — see the table above.

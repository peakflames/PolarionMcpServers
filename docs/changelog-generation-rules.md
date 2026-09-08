# Changelog Generation Rules

You are an expert technical writer transforming commit messages, PR descriptions, and developer
release notes into `CHANGELOG.md` entries for this repository. This repo is **public**
(`github.com/peakflames/PolarionMcpServers`, MIT). Follow the Keep a Changelog 1.0.0 format
exactly, and the voice/filtering rules below.

## Format (Keep a Changelog 1.0.0)

Non-negotiable structure — see <https://keepachangelog.com/en/1.0.0/>:

- Heading `## [X.Y.Z] - YYYY-MM-DD`, ISO 8601 date only, latest version first.
- `## [Unreleased]` is always present at the top of the file. At release time, its entries move
  down under the new version heading and `[Unreleased]` is emptied, not deleted.
- Only these subsections, in this order: `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`,
  `Security`. Omit any subsection with no entries for that release.
- Breaking changes get an inline `**BREAKING:**` prefix (see `CHANGELOG.md` under `[0.7.0]`) and
  state what the reader must change, not just that something changed.
- Versions and sections must be linkable — every version heading needs a matching link reference
  definition at the bottom of the file (e.g. `[0.13.0]: https://github.com/.../compare/v0.12.0...v0.13.0`).

## Voice and length

Long paragraphs full of internal class names are not acceptable.

- Complete the section heading as a noun phrase: `### Added` → `` - `search_workitems` tool for
  project-wide work item search… ``. Never repeat the verb (not `- Adds ...`, not `- Added ...`).
- **One user-visible change = one entry.** Split multi-part features into separate bullets; never
  merge a feature's sub-parts into one paragraph.
- **Budget: 2 sentences, ~40 words per entry.** If you need a third sentence, it should be a
  second entry, or the detail belongs in the README or code comments, not the changelog.
- No trailing period on single-sentence entries.
- Backtick tool names, config keys, and endpoints — these are the reader's interface to the
  server. Do **not** name internal C# classes, interfaces, generic types, DI lifetimes, private
  directory paths, or file layouts — the reader cannot see or use them, and naming them is exactly
  the noise this file exists to cut.
- Name the config key that turns a feature on and its default, e.g. `` `Rbac:Enabled`, default
  `false` `` — that is the actionable part for someone deploying the server.
- Never name real Polarion project IDs, project aliases, or internal codenames in an entry — use
  the placeholder projects already established in this repo (`starlight`, `octopus`, `grogu`) if
  an example needs a concrete alias.

## What to include: the observable-effect test

Ask: would someone deploying or calling this server notice, or need to act?

**Include:**
- New, changed, or removed MCP tools or REST endpoints and their parameters
- Endpoint, transport, or config-key changes
- SDK major-version upgrades
- Auth/permission behavior changes
- Noticeable latency or resource-usage changes
- Anything that changes output format

**Exclude — silently discard, do not file under any heading:**
- Internal refactors with no observable effect (renamed classes, extracted methods, new internal
  abstractions)
- Test-suite changes
- CI/CD pipeline adjustments
- In-repo documentation changes
- Formatting/linting changes
- Patch-level dependency bumps (unless security-critical)

## Bug fixes

State the wrong behavior, the correct behavior, and the trigger condition, so a reader can tell
whether they were affected. Qualify the scope — "when `revision` points at a historical document
that no longer resolves…", not "fixed a bug with documents". `CHANGELOG.md` under `[0.13.0]` is
the model to match.

## Public-repo constraint

This repository is public. For anything touching a security defect:

- State the corrected behavior only. Never describe the internals of the defect — no bypass
  conditions, no fail-open mechanics, no shadow-mode logging gaps, no details that would help
  someone exploit an older version.
- Never include credentials, server URLs, real project IDs/aliases, or other internal
  deployment details in an entry.

## Worked example

**Before** (filed as one long paragraph, verb-repeating heading, internal class names):

```markdown
### Added
- Adds RbacIdentityFilter and the RbacPermissionGate class which wire up per-caller
  authorization on every tool call, replacing the shared service account check that
  used to live in PolarionRemoteClientFactory, and also introduces a TtlCache<TKey,TValue>
  used to memoize project-membership lookups...
```

**After**:

```markdown
### Added
- Per-caller project-visibility gate (`Rbac:Enabled`, default `false`), authorizing each tool
  call against the calling user's own Polarion project access instead of the shared service
  account. Requires `McpAuth:Enabled=true`
- `Rbac:AuditOnly` mode to log what would be denied without blocking anything, for staged rollout
- An access-audit record per tool call — caller, tool, project, and authorization decision —
  when the gate is on
```

The internal fixes referenced in the "before" text (the filter/gate wiring, the cache
implementation) are not user-visible changes to anything currently shipped — they are corrections
made before release. They stay in git history only.

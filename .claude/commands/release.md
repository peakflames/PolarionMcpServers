---
description: Release current develop version to main with proper tagging and version bumping
---

# Release Process

You are performing a release from develop to main. Follow these steps exactly:

## Step 1: Verify Clean State
- Check out `develop` and fast-forward it to `origin/develop`
- Ensure there are no uncommitted changes (except appsettings.Development.json)
- Ensure `gh auth status` shows the account with push rights to `peakflames/PolarionMcpServers` as active

## Step 2: Get Current Version
- Read the `<Version>` tag from `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj`
- This is the version being released (e.g., `0.12.0`)
- Confirm `PolarionMcpServer/PolarionMcpServer.csproj` `<Version>` and the Remote project's `<ContainerImageTag>` match it

## Step 3: Verify the Build
Per the project CLAUDE.md verification rule:
```bash
python build.py start
python build.py mcp ping
python build.py mcp tools
python build.py log --level error
python build.py stop
```

## Step 4: Promote the CHANGELOG
Follow `docs/changelog-generation-rules.md`:
- Review `## [Unreleased]` against `git diff v{PREVIOUS_VERSION}..develop`. Entries must describe the net change since the last release only — drop or rewrite any entry that describes an intermediate state introduced and changed again within the same release cycle
- Insert `## [{VERSION}] - YYYY-MM-DD` directly below `## [Unreleased]`, leaving `[Unreleased]` empty (never delete it)
- Update the link references at the bottom of the file:
  ```
  [Unreleased]: https://github.com/peakflames/PolarionMcpServers/compare/v{VERSION}...HEAD
  [{VERSION}]: https://github.com/peakflames/PolarionMcpServers/compare/v{PREVIOUS_VERSION}...v{VERSION}
  ```

```bash
git add CHANGELOG.md
git commit -m "chore(release): version {VERSION} and promote CHANGELOG Unreleased entries"
git push origin develop
```

## Step 5: Merge to Main
```bash
git checkout main
git pull origin main
git merge develop --no-ff -m "Merge branch 'develop' into main for v{VERSION} release"
```

## Step 6: Tag the Release
```bash
git tag -a v{VERSION} -m "Release v{VERSION}"
```

## Step 7: Push Main and Tag
```bash
git push origin main
git push origin v{VERSION}
```
Pushing the tag triggers `.github/workflows/build.yml`, which builds the linux-x64 binary, creates the GitHub Release, and publishes `peakflames/polarion-remote-mcp-server:{VERSION}` and `:latest` to Docker Hub. Watch it with `gh run watch` and confirm with `gh release view v{VERSION}`.

## Step 8: Prepare Develop for Next Version
- Checkout develop
- Calculate next version by incrementing minor version (e.g., `0.12.0` → `0.13.0`)
- Update `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj`:
  - Set `<Version>` to next version
  - Set `<ContainerImageTag>` to next version
- Update `PolarionMcpServer/PolarionMcpServer.csproj`:
  - Set `<Version>` to next version
- Leave `CHANGELOG.md` `## [Unreleased]` empty — do not add a version heading for the in-development version

```bash
git add PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj PolarionMcpServer/PolarionMcpServer.csproj
git commit -m "chore: bump version to {NEXT_VERSION} for development"
git push origin develop
```

## Important Notes
- NEVER commit `appsettings.Development.json`
- Use `--no-ff` for merge to preserve commit history
- Tag format is `v{VERSION}` (e.g., `v0.12.0`)
- Always confirm version numbers with the user before proceeding

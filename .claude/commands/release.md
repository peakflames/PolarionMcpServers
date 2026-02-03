---
description: Release current develop version to main with proper tagging and version bumping
---

# Release Process

You are performing a release from develop to main. Follow these steps exactly:

## Step 1: Verify Clean State
- Check git status on develop branch
- Ensure there are no uncommitted changes (except appsettings.Development.json)
- Ensure develop is pushed to origin

## Step 2: Get Current Version
- Read the `<Version>` tag from `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj`
- This is the version being released (e.g., `0.12.0`)

## Step 3: Merge to Main
```bash
git checkout main
git pull origin main
git merge develop --no-ff -m "Merge branch 'develop' into main for v{VERSION} release"
```

## Step 4: Tag the Release
```bash
git tag -a v{VERSION} -m "Release v{VERSION}"
```

## Step 5: Push Main and Tag
```bash
git push origin main
git push origin v{VERSION}
```
Note: GitHub Actions will automatically create the release and publish artifacts when the tag is pushed.

## Step 6: Prepare Develop for Next Version
- Checkout develop
- Calculate next version by incrementing minor version (e.g., `0.12.0` → `0.13.0`)
- Update `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj`:
  - Set `<Version>` to next version
  - Set `<ContainerImageTag>` to next version
- Update `CHANGELOG.md`:
  - Add new section at top: `## {NEXT_VERSION} (In Development)\n\nChanges TBD\n\n`

## Step 7: Commit and Push Develop
```bash
git add PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj CHANGELOG.md
git commit -m "chore: bump version to {NEXT_VERSION} for development

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
git push origin develop
```

## Important Notes
- NEVER commit `appsettings.Development.json`
- Use `--no-ff` for merge to preserve commit history
- Tag format is `v{VERSION}` (e.g., `v0.12.0`)
- Always confirm version numbers with the user before proceeding

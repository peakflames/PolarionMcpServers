# Publish Release Workflow

This workflow handles the complete release process for the PolarionMcpServers project, including version updates, git operations, tagging, and triggering automated builds for GitHub releases and Docker images.

## ⚠️ CRITICAL WARNING

**NEVER commit, add, reset, checkout, discard, or modify `PolarionRemoteMcpServer/appsettings.json`** - this file contains sensitive credentials that must be protected at all costs. This file should never be touched in any way during the release process.

## Prerequisites

- You should be on the `develop` branch
- The `develop` branch should contain all the changes you want to release

## Step 1: Pre-flight Checks

Use the 'execute_command' tool to check git status:
```bash
git status
```

If there are uncommitted changes (excluding `PolarionRemoteMcpServer/appsettings.json`), use the 'ask_followup_question' tool to ask the user if they should be committed before proceeding with the release.

## Step 2: Update Versions and Changelog

Use the 'read_file' tool to check the current versions in both project files:
- `PolarionMcpServer/PolarionMcpServer.csproj` - Look for the `<Version>` element
- `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj` - Look for the `<Version>` and `<ContainerImageTag>` elements

Use the 'replace_in_file' tool to update the version in `PolarionMcpServer/PolarionMcpServer.csproj` (replace X.X.X with the new version):
```xml
<Version>X.X.X</Version>
```

Use the 'replace_in_file' tool to update the version and container tag in `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj` (replace X.X.X with the new version):
```xml
<Version>X.X.X</Version>
<ContainerImageTag>X.X.X</ContainerImageTag>
```

Use the 'replace_in_file' tool to add a new version entry at the top of `CHANGELOG.md`:
```markdown
## X.X.X

- [List of changes]
```

## Step 3: Build Validation

Use the 'execute_command' tool to clean the solution:
```bash
dotnet clean PolarionMcpServers.sln
```

Use the 'execute_command' tool to build the solution:
```bash
dotnet build PolarionMcpServers.sln
```

Verify the build succeeds before proceeding.

## Step 4: Commit Version Updates

Use the 'execute_command' tool to stage the version and changelog changes (NEVER stage appsettings.json):
```bash
git add PolarionMcpServer/PolarionMcpServer.csproj PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj CHANGELOG.md
```

Use the 'execute_command' tool to commit the changes:
```bash
git commit -m "chore: release vX.X.X"
```

Use the 'execute_command' tool to push to develop:
```bash
git push origin develop
```

## Step 5: Merge to Main

Use the 'execute_command' tool to switch to main branch:
```bash
git switch main
```

Use the 'execute_command' tool to merge develop with no fast-forward:
```bash
git merge develop --no-ff -m "Release vX.X.X: [brief description of changes]"
```

Use the 'execute_command' tool to push to remote:
```bash
git push origin main
```

## Step 6: Create and Push Tag

Use the 'execute_command' tool to create an annotated tag:
```bash
git tag -a vX.X.X -m "Release vX.X.X: [brief description of changes]"
```

Use the 'execute_command' tool to push the tag to remote:
```bash
git push origin vX.X.X
```

**Note:** Pushing the tag will automatically trigger GitHub Actions to:
- Build executables for Windows, Linux, and macOS
- Create a GitHub release with the executables
- Build and publish Docker image to Docker Hub
- Tag Docker image as `latest`

## Step 7: Return to Develop and Bump Version

Use the 'execute_command' tool to return to develop branch:
```bash
git switch develop
```

Use the 'replace_in_file' tool to bump the version to the next minor version in `PolarionMcpServer/PolarionMcpServer.csproj`:
```xml
<Version>X.X.X</Version>
```

Use the 'replace_in_file' tool to bump the version and container tag in `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj`:
```xml
<Version>X.X.X</Version>
<ContainerImageTag>X.X.X</ContainerImageTag>
```

Use the 'replace_in_file' tool to add a placeholder for the next version at the top of `CHANGELOG.md`:
```markdown
## X.X.X

- TBD
```

Use the 'execute_command' tool to stage the changes (NEVER stage appsettings.json):
```bash
git add PolarionMcpServer/PolarionMcpServer.csproj PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj CHANGELOG.md
```

Use the 'execute_command' tool to commit the version bump:
```bash
git commit -m "chore: bump version to vX.X.X (develop)"
```

Use the 'execute_command' tool to push to remote:
```bash
git push origin develop
```

## Step 8: Verification

- Verify the tag appears on GitHub
- Monitor GitHub Actions workflow status at: https://github.com/peakflames/PolarionMcpServers/actions
- The GitHub release should be created with executables for all platforms
- The Docker image should be available at: https://hub.docker.com/r/peakflames/polarion-remote-mcp-server

## Example Usage

For version 0.5.0 with new MCP tools:
- Update versions to `0.5.0`
- Changelog entry: `- Add new MCP tools for enhanced Polarion integration`
- Merge message: `"Release v0.5.0: Add new MCP tools for enhanced Polarion integration"`
- Tag: `v0.5.0`
- Next develop version: `0.5.1`

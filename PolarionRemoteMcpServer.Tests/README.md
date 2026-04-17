# PolarionRemoteMcpServer.Tests

Automated test suite for PolarionRemoteMcpServer REST API endpoints.

## Overview

This test project provides comprehensive testing for the document/module endpoints, verifying that work items are correctly retrieved from Polarion documents at various revisions. Tests use `WebApplicationFactory` to spin up an in-process server — no separately running instance of PolarionRemoteMcpServer is required.

## Test Types

### Integration Tests (`Integration/DocumentsEndpointsIntegrationTests.cs`)
- Test real REST API endpoints with actual Polarion server calls
- Require valid credentials in `testsettings.json`
- Verify 4 specific test scenarios: non-branched/branched × latest/historical revision
- Assert specific expected work item IDs are present in the response

### Snapshot Tests (`Integration/DocumentsEndpointsSnapshotTests.cs`)
- Capture the full JSON response for each scenario into `.verified.txt` baseline files
- On subsequent runs, diff the current response against the baseline
- Used to detect unintended changes to the REST API response, whether from updates to `PolarionApiClient` or to the wrapper logic in this repo
- Snapshot files are gitignored (contain company-specific Polarion data)

### Unit Tests (`Unit/`)
- Test configuration loading and validation
- Test expected work items data structures
- No external dependencies (no network calls, no credentials required)
- Run in under a second

## Setup Instructions

### 1. Create Test Configuration

Copy the template file:
```bash
cp testsettings.template.json testsettings.json
```

### 2. Configure Credentials

Edit `testsettings.json` with your actual credentials and test scenarios:

```json
{
  "TestSettings": {
    "ApiBaseUrl": "http://localhost:5090",
    "ApiKey": "YOUR_ACTUAL_API_KEY_HERE",
    "TestProjects": [
      {
        "ProjectId": "YourProjectId",
        "ServerUrl": "https://your-polarion-server.com",
        "Username": "YOUR_USERNAME",
        "Password": "YOUR_PASSWORD"
      },
      {
        "ProjectId": "YourBranchedProjectId",
        "ServerUrl": "https://your-polarion-server.com",
        "Username": "YOUR_USERNAME",
        "Password": "YOUR_PASSWORD"
      }
    ],
    "TestScenarios": [
      {
        "Name": "NonBranchedLatest",
        "ProjectId": "YourProjectId",
        "SpaceId": "YourSpaceId",
        "DocumentId": "your_document_id",
        "Revision": null,
        "ExpectedWorkItemIds": ["PROJ-123", "PROJ-456", "PROJ-789"]
      },
      {
        "Name": "BranchedLatest",
        "ProjectId": "YourBranchedProjectId",
        "SpaceId": "YourSpaceId",
        "DocumentId": "your_document_id",
        "Revision": null,
        "ExpectedWorkItemIds": ["BRANCH-123", "BRANCH-456", "PROJ-789"]
      },
      {
        "Name": "NonBranchedHistoricRevision",
        "ProjectId": "YourProjectId",
        "SpaceId": "YourSpaceId",
        "DocumentId": "your_document_id",
        "Revision": "123456",
        "ExpectedWorkItemIds": ["PROJ-123", "PROJ-456"]
      },
      {
        "Name": "BranchedHistoricRevision",
        "ProjectId": "YourBranchedProjectId",
        "SpaceId": "YourSpaceId",
        "DocumentId": "your_document_id",
        "Revision": "123456",
        "ExpectedWorkItemIds": ["BRANCH-123", "PROJ-789"]
      }
    ]
  }
}
```

**IMPORTANT**: `testsettings.json` is gitignored and contains sensitive credentials. Never commit this file.

## Running Tests

No server needs to be started manually — `WebApplicationFactory` launches an in-process server automatically for each test run, using the configuration from `testsettings.json`.

### Run All Tests
```bash
dotnet test PolarionRemoteMcpServer.Tests/PolarionRemoteMcpServer.Tests.csproj
```

### Run Only Unit Tests (no credentials required)
```bash
dotnet test --filter "FullyQualifiedName~Unit"
```

### Run Only Integration Tests
```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

### Run Only Snapshot Tests
```bash
dotnet test --filter "FullyQualifiedName~Snapshot"
```

### Run Specific Scenarios
```bash
dotnet test --filter "Scenario1"
dotnet test --filter "Branched"
dotnet test --filter "Historical"
```

### Via build.py
```bash
python build.py test
python build.py test --filter Unit
python build.py test --filter Snapshot
```

## Test Scenarios

The integration and snapshot tests cover 4 scenarios configured in `testsettings.json`:

### Scenario 1: Non-Branched Module (Latest Revision)
- **Scenario Name**: `NonBranchedLatest`
- **Verifies**: Latest work items from a non-branched project

### Scenario 2: Branched Module (Latest Revision)
- **Scenario Name**: `BranchedLatest`
- **Verifies**: Latest work items from a project branched from another
- **Note**: May contain work items from both the current project and the original

### Scenario 3: Non-Branched Module (Historical Revision)
- **Scenario Name**: `NonBranchedHistoricRevision`
- **Verifies**: Work items from a specific historical document revision
- **Note**: Tests `isHistorical` and `revision` metadata fields in the response

### Scenario 4: Branched Module (Historical Revision)
- **Scenario Name**: `BranchedHistoricRevision`
- **Verifies**: Historical work items from a branched project at a specific revision
- **Note**: Tests both historical revision handling and branched project work items

## Snapshot Testing Workflow

Snapshot tests capture the full JSON API response and compare it to a stored baseline. Use them to detect unintended behavioural changes after modifying `PolarionApiClient` or the MCP tool wrapper logic in this repo.

### Establish a Baseline

Run the snapshot tests before making any changes. On the first run (no `.verified.txt` files yet), Verify creates baseline files and the tests pass automatically:

```bash
dotnet test --filter "FullyQualifiedName~Snapshot"
```

Baseline files are written to `Integration/Snapshots/`.

### Detect Changes After Modifying PolarionApiClient or Wrapper Logic

After making changes, re-run the snapshot tests. Any differences between the new response and the baseline cause the test to fail, writing a `.received.txt` file alongside the `.verified.txt`:

```bash
dotnet test --filter "FullyQualifiedName~Snapshot"
# Failing test writes: DocumentsEndpointsSnapshotTests.<TestName>.received.txt
# Compare against:     DocumentsEndpointsSnapshotTests.<TestName>.verified.txt
```

### Accept Intentional Changes

If the change is correct, promote the received file to the new baseline:

```bash
# Accept a single test
copy Integration\Snapshots\DocumentsEndpointsSnapshotTests.<TestName>.received.txt ^
     Integration\Snapshots\DocumentsEndpointsSnapshotTests.<TestName>.verified.txt

# Or accept all at once (PowerShell)
Get-ChildItem Integration/Snapshots -Filter *.received.txt | ForEach-Object {
    Copy-Item $_.FullName ($_.FullName -replace '\.received\.', '.verified.')
}
```

Snapshot files (`*.verified.txt`, `*.received.txt`) are gitignored because they contain company-specific Polarion work item data.

## Project Structure

```
PolarionRemoteMcpServer.Tests/
├── GlobalUsings.cs                              # Global using directives
├── PolarionRemoteMcpServer.Tests.csproj         # Project file
├── testsettings.template.json                   # Template (checked into git)
├── testsettings.json                            # Your local config (gitignored)
├── TestConfiguration.cs                         # Configuration loader
├── VerifyConfig.cs                              # Global Verify snapshot settings
├── Fixtures/
│   └── TestWebApplicationFactory.cs             # WebApplicationFactory setup
├── TestData/
│   └── ExpectedWorkItems.cs                     # Scenario data accessors
├── Integration/
│   ├── DocumentsEndpointsIntegrationTests.cs    # Assertion-based integration tests
│   ├── DocumentsEndpointsSnapshotTests.cs       # Verify snapshot tests
│   └── Snapshots/                               # Baseline files (gitignored)
│       └── *.verified.txt
└── Unit/
    ├── TestConfigurationTests.cs                # Configuration validation tests
    └── ExpectedWorkItemsTests.cs                # Test data validation tests
```

## Troubleshooting

### Tests fail with "API key not configured"
Create and configure `testsettings.json` from the template.

### Tests fail with authentication errors
Verify credentials in `testsettings.json` are correct and have access to the configured Polarion projects.

### Work item IDs don't match
Expected work item IDs represent a snapshot of the document at a point in time. If Polarion data has since changed:
1. Verify the document hasn't been significantly modified
2. Update `ExpectedWorkItemIds` in your local `testsettings.json` for the affected scenario

### Integration test times out
The default HTTP client timeout is 10 minutes. Scenario 4 (branched historical revision) makes many sequential Polarion API calls and is the slowest. If it still times out, the `CreateAuthenticatedClient()` timeout in `TestWebApplicationFactory.cs` can be increased.

### Snapshot test fails on first run
This should not happen — Verify creates the baseline automatically on the first run. If it does fail, check that the `Integration/Snapshots/` directory is writable.

### Snapshot test fails after PolarionApiClient or wrapper logic changes
This is expected behaviour. Review the diff between `.received.txt` and `.verified.txt` in `Integration/Snapshots/`. If the change is correct, copy `.received.txt` over `.verified.txt` to accept the new baseline.

## Adding New Tests

### Adding a New Integration + Snapshot Test Scenario

1. Add the scenario to your local `testsettings.json`
2. Add an assertion-based test method to `DocumentsEndpointsIntegrationTests.cs`
3. Add a corresponding snapshot test method to `DocumentsEndpointsSnapshotTests.cs`
4. Run the snapshot test once to establish the baseline
5. Optionally add the scenario (with placeholder values) to `testsettings.template.json`

### Adding Tests for Other Endpoints

Create new test files following the existing patterns:
```
Integration/WorkItemsEndpointsIntegrationTests.cs
Integration/WorkItemsEndpointsSnapshotTests.cs
```

## CI/CD Integration

Integration and snapshot tests require live Polarion credentials and are best run on-demand or in nightly builds rather than on every PR. Unit tests have no external dependencies and are suitable for every CI run.

```yaml
- name: Unit Tests
  run: dotnet test PolarionRemoteMcpServer.Tests/PolarionRemoteMcpServer.Tests.csproj --filter "FullyQualifiedName~Unit"
```

For integration/snapshot tests in CI, configure credentials via environment variables rather than `testsettings.json`.

## Dependencies

- **xunit.v3** (3.2.2): Test framework
- **xunit.runner.visualstudio** (3.1.5): Visual Studio / dotnet test integration
- **FluentAssertions** (6.12.2): Readable assertions
- **Verify.XunitV3** (31.15.0): Snapshot/approval testing
- **Microsoft.AspNetCore.Mvc.Testing** (9.0.0): WebApplicationFactory for integration tests
- **Moq** (4.20.72): Mocking framework
- **Microsoft.NET.Test.Sdk** (17.12.0): Test SDK
- **coverlet.collector** (6.0.2): Code coverage

## Security Notes

- **NEVER** commit `testsettings.json` — it contains credentials
- **NEVER** commit `*.verified.txt` or `*.received.txt` — they contain company-specific Polarion data
- Only commit `testsettings.template.json` with placeholder values
- Use environment variables for CI/CD credentials

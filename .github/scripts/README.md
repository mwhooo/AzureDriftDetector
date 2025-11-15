# GitHub Actions Scripts

This directory contains utility scripts used by the GitHub Actions workflows for the Azure Drift Detector project.

## Scripts

### `check-drift.sh`

**Purpose**: Executes the Azure Drift Detector and extracts JSON drift reports

**Usage**:
```bash
./check-drift.sh <template-file> <resource-group> [output-file] [ignore-config]
```

**Parameters**:
- `template-file`: Path to the Bicep template file to check
- `resource-group`: Azure resource group name to monitor
- `output-file`: Output JSON file name (default: `drift-report.json`)
- `ignore-config`: Optional path to drift ignore configuration file

**Behavior**:
- Runs the .NET drift detector with JSON output
- Extracts valid JSON from the console output
- Sets GitHub Actions output variable `drift=true/false`
- Creates a standardized JSON report file

**Used by**: `.github/workflows/drift-monitoring.yml`

### `create-drift-issue.js`

**Purpose**: Creates detailed GitHub issues when configuration drift is detected

**Usage**:
```javascript
const { createDriftIssue } = require('./create-drift-issue.js');
const issueData = createDriftIssue(environment, githubContext);
```

**Parameters**:
- `environment`: Environment name (dev, staging, prod)
- `githubContext`: GitHub Actions context object with repo/run information

**Returns**:
- Issue object with `title`, `body`, and `labels` properties

**Features**:
- Parses drift report JSON for detailed analysis
- Creates formatted issue with emoji indicators
- Includes remediation suggestions
- Links back to workflow run
- Handles parsing errors gracefully

**Used by**: `.github/workflows/drift-monitoring.yml`

## Development Notes

- Both scripts include error handling and logging
- The bash script uses `set -e` for fail-fast behavior
- The JavaScript module can be used both as a CLI tool and imported module
- Output formatting follows consistent emoji conventions throughout the project
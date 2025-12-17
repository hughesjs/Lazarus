#!/bin/bash
# Test the CI pipeline locally with act

set -e

echo "Running CI Pipeline..."

# Get GitHub token from gh CLI
GITHUB_TOKEN=$(gh auth token)

# Get current branch name
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)

# Create event payload for pull_request to ensure github.head_ref is set
cat > /tmp/pr-event.json <<EOF
{
  "pull_request": {
    "head": {
      "ref": "$CURRENT_BRANCH"
    },
    "base": {
      "ref": "master"
    }
  }
}
EOF

act pull_request \
  -W ../.github/workflows/ci-pipeline.yml \
  -P ubuntu-latest=catthehacker/ubuntu:full-latest \
  --artifact-server-path ./artifacts-ci \
  --env DOTNET_NOLOGO=true \
  --env DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  --eventpath /tmp/pr-event.json \
  -s GITHUB_TOKEN="$GITHUB_TOKEN"

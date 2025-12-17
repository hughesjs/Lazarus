#!/bin/bash
# Test the CD pipeline locally with act

set -e

echo "Running CD Pipeline..."

# Get GitHub token from gh CLI
GITHUB_TOKEN=$(gh auth token)

act push \
  -W ../.github/workflows/cd-pipeline.yml \
  -P ubuntu-latest=catthehacker/ubuntu:full-latest \
  --artifact-server-path ./artifacts-cd \
  --env DOTNET_NOLOGO=true \
  --env DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -s GITHUB_TOKEN="$GITHUB_TOKEN"

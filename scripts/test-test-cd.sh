#!/bin/bash
# Tests for test-cd.sh

set -euo pipefail

# Colours for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly NC='\033[0m' # No Colour

# Track test results
TESTS_RUN=0
TESTS_PASSED=0
TESTS_FAILED=0

# Script location
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
readonly TARGET_SCRIPT="${SCRIPT_DIR}/test-cd.sh"

# Test helper functions
assert_equals() {
    local expected="$1"
    local actual="$2"
    local message="${3:-}"

    TESTS_RUN=$((TESTS_RUN + 1))

    if [[ "$expected" == "$actual" ]]; then
        TESTS_PASSED=$((TESTS_PASSED + 1))
        echo -e "${GREEN}✓${NC} ${message:-Test passed}"
    else
        TESTS_FAILED=$((TESTS_FAILED + 1))
        echo -e "${RED}✗${NC} ${message:-Test failed}"
        echo "  Expected: $expected"
        echo "  Got:      $actual"
    fi
}

assert_success() {
    local exit_code=$?
    local message="${1:-Command should succeed}"

    TESTS_RUN=$((TESTS_RUN + 1))

    if [[ $exit_code -eq 0 ]]; then
        TESTS_PASSED=$((TESTS_PASSED + 1))
        echo -e "${GREEN}✓${NC} ${message}"
    else
        TESTS_FAILED=$((TESTS_FAILED + 1))
        echo -e "${RED}✗${NC} ${message}"
        echo "  Exit code: $exit_code"
    fi
}

assert_failure() {
    local exit_code=$?
    local message="${1:-Command should fail}"

    TESTS_RUN=$((TESTS_RUN + 1))

    if [[ $exit_code -ne 0 ]]; then
        TESTS_PASSED=$((TESTS_PASSED + 1))
        echo -e "${GREEN}✓${NC} ${message}"
    else
        TESTS_FAILED=$((TESTS_FAILED + 1))
        echo -e "${RED}✗${NC} ${message}"
        echo "  Expected non-zero exit code but got 0"
    fi
}

assert_file_contains() {
    local file="$1"
    local pattern="$2"
    local message="${3:-File should contain pattern}"

    TESTS_RUN=$((TESTS_RUN + 1))

    if grep -q "$pattern" "$file" 2>/dev/null; then
        TESTS_PASSED=$((TESTS_PASSED + 1))
        echo -e "${GREEN}✓${NC} ${message}"
    else
        TESTS_FAILED=$((TESTS_FAILED + 1))
        echo -e "${RED}✗${NC} ${message}"
        echo "  Pattern not found: $pattern"
    fi
}

assert_file_not_contains() {
    local file="$1"
    local pattern="$2"
    local message="${3:-File should not contain pattern}"

    TESTS_RUN=$((TESTS_RUN + 1))

    if ! grep -q "$pattern" "$file" 2>/dev/null; then
        TESTS_PASSED=$((TESTS_PASSED + 1))
        echo -e "${GREEN}✓${NC} ${message}"
    else
        TESTS_FAILED=$((TESTS_FAILED + 1))
        echo -e "${RED}✗${NC} ${message}"
        echo "  Pattern found but shouldn't exist: $pattern"
    fi
}

# Setup
setup() {
    # Create temporary test directory
    TEST_DIR="$(mktemp -d)"
    trap 'rm -rf "$TEST_DIR"' EXIT
}

# Teardown
teardown() {
    # Cleanup happens in trap
    :
}

# Individual test functions
test_script_exists() {
    [[ -f "$TARGET_SCRIPT" ]]
    assert_success "Script file exists"
}

test_script_executable() {
    [[ -x "$TARGET_SCRIPT" ]]
    assert_success "Script is executable"
}

test_script_has_shebang() {
    local first_line
    first_line="$(head -n 1 "$TARGET_SCRIPT")"
    [[ "$first_line" =~ ^#!/bin/bash ]]
    assert_success "Script has correct shebang"
}

test_script_uses_error_handling() {
    assert_file_contains "$TARGET_SCRIPT" "set -e" "Script uses error handling (set -e)"
}

test_required_tool_gh_available() {
    command -v gh >/dev/null 2>&1
    local result=$?
    TESTS_RUN=$((TESTS_RUN + 1))

    if [[ $result -eq 0 ]]; then
        TESTS_PASSED=$((TESTS_PASSED + 1))
        echo -e "${GREEN}✓${NC} Required tool 'gh' is available"
    else
        TESTS_FAILED=$((TESTS_FAILED + 1))
        echo -e "${YELLOW}!${NC} Required tool 'gh' is NOT available (install with: sudo pacman -S github-cli)"
    fi
}

test_required_tool_git_available() {
    command -v git >/dev/null 2>&1
    assert_success "Required tool 'git' is available"
}

test_required_tool_act_available() {
    command -v act >/dev/null 2>&1
    local result=$?
    TESTS_RUN=$((TESTS_RUN + 1))

    if [[ $result -eq 0 ]]; then
        TESTS_PASSED=$((TESTS_PASSED + 1))
        echo -e "${GREEN}✓${NC} Required tool 'act' is available"
    else
        TESTS_FAILED=$((TESTS_FAILED + 1))
        echo -e "${YELLOW}!${NC} Required tool 'act' is NOT available (install from: https://github.com/nektos/act)"
    fi
}

test_workflow_file_exists() {
    local workflow_file="${SCRIPT_DIR}/.github/workflows/cd-pipeline.yml"
    [[ -f "$workflow_file" ]]
    assert_success "CD workflow file exists at .github/workflows/cd-pipeline.yml"
}

test_script_references_correct_workflow() {
    assert_file_contains "$TARGET_SCRIPT" ".github/workflows/cd-pipeline.yml" \
        "Script references correct workflow file"
}

test_script_uses_push_event() {
    assert_file_contains "$TARGET_SCRIPT" "act push" \
        "Script uses push event"
}

test_script_does_not_use_pull_request_event() {
    assert_file_not_contains "$TARGET_SCRIPT" "pull_request" \
        "Script does not use pull_request event (CD uses push)"
}

test_script_does_not_create_event_payload() {
    assert_file_not_contains "$TARGET_SCRIPT" "pr-event.json" \
        "Script does not create custom event payload (push event uses default)"
}

test_script_gets_github_token() {
    assert_file_contains "$TARGET_SCRIPT" "gh auth token" \
        "Script retrieves GitHub token from gh CLI"
}

test_script_uses_correct_runner_image() {
    assert_file_contains "$TARGET_SCRIPT" "catthehacker/ubuntu:full-latest" \
        "Script uses correct runner image"
}

test_script_sets_artifact_path() {
    assert_file_contains "$TARGET_SCRIPT" "artifacts-cd" \
        "Script sets artifact server path"
}

test_script_passes_github_token() {
    assert_file_contains "$TARGET_SCRIPT" "GITHUB_TOKEN=\"\$GITHUB_TOKEN\"" \
        "Script passes GitHub token to act"
}

test_script_dotnet_environment_vars() {
    assert_file_contains "$TARGET_SCRIPT" "DOTNET_NOLOGO=true" \
        "Script sets DOTNET_NOLOGO environment variable"
    assert_file_contains "$TARGET_SCRIPT" "DOTNET_CLI_TELEMETRY_OPTOUT=1" \
        "Script sets DOTNET_CLI_TELEMETRY_OPTOUT environment variable"
}

test_gh_auth_status() {
    if command -v gh >/dev/null 2>&1; then
        gh auth status >/dev/null 2>&1
        local result=$?
        TESTS_RUN=$((TESTS_RUN + 1))

        if [[ $result -eq 0 ]]; then
            TESTS_PASSED=$((TESTS_PASSED + 1))
            echo -e "${GREEN}✓${NC} GitHub CLI is authenticated"
        else
            TESTS_FAILED=$((TESTS_FAILED + 1))
            echo -e "${YELLOW}!${NC} GitHub CLI is NOT authenticated (run: gh auth login)"
        fi
    else
        TESTS_RUN=$((TESTS_RUN + 1))
        echo -e "${YELLOW}!${NC} Skipping gh auth test (gh not available)"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    fi
}

test_git_repository() {
    cd "$SCRIPT_DIR"
    git rev-parse --git-dir >/dev/null 2>&1
    assert_success "Script is in a git repository"
}

test_cd_vs_ci_differences() {
    # Verify this is truly the CD script and not CI
    local ci_script="${SCRIPT_DIR}/test-ci.sh"

    if [[ -f "$ci_script" ]]; then
        # CD should use push event, CI should use pull_request
        grep -q "act push" "$TARGET_SCRIPT" 2>/dev/null
        local cd_has_push=$?

        grep -q "act pull_request" "$ci_script" 2>/dev/null
        local ci_has_pr=$?

        TESTS_RUN=$((TESTS_RUN + 1))
        if [[ $cd_has_push -eq 0 ]] && [[ $ci_has_pr -eq 0 ]]; then
            TESTS_PASSED=$((TESTS_PASSED + 1))
            echo -e "${GREEN}✓${NC} CD script correctly uses push event (CI uses pull_request)"
        else
            TESTS_FAILED=$((TESTS_FAILED + 1))
            echo -e "${RED}✗${NC} CD/CI event mismatch detected"
        fi
    else
        TESTS_RUN=$((TESTS_RUN + 1))
        echo -e "${YELLOW}!${NC} Skipping CD vs CI comparison (test-ci.sh not found)"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    fi
}

test_artifact_directory_different_from_ci() {
    # Verify CD uses different artifact directory than CI
    assert_file_contains "$TARGET_SCRIPT" "artifacts-cd" \
        "CD uses separate artifact directory from CI"
}

# Print summary
print_summary() {
    echo ""
    echo "======================================"
    echo "Test Summary for test-cd.sh"
    echo "======================================"
    echo "Tests run:    $TESTS_RUN"
    echo -e "${GREEN}Tests passed: $TESTS_PASSED${NC}"
    if [[ $TESTS_FAILED -gt 0 ]]; then
        echo -e "${RED}Tests failed: $TESTS_FAILED${NC}"
    else
        echo "Tests failed: $TESTS_FAILED"
    fi
    echo "======================================"

    [[ $TESTS_FAILED -eq 0 ]]
}

# Main test execution
main() {
    setup

    echo "Running tests for test-cd.sh..."
    echo ""

    # Basic script tests
    test_script_exists
    test_script_executable
    test_script_has_shebang
    test_script_uses_error_handling

    # Required tools tests
    test_required_tool_gh_available
    test_required_tool_git_available
    test_required_tool_act_available

    # Workflow and configuration tests
    test_workflow_file_exists
    test_script_references_correct_workflow
    test_script_uses_push_event
    test_script_does_not_use_pull_request_event
    test_script_does_not_create_event_payload
    test_script_gets_github_token
    test_script_uses_correct_runner_image
    test_script_sets_artifact_path
    test_script_passes_github_token
    test_script_dotnet_environment_vars
    test_artifact_directory_different_from_ci

    # CD-specific tests
    test_cd_vs_ci_differences

    # Runtime environment tests
    test_gh_auth_status
    test_git_repository

    teardown
    print_summary
}

main "$@"

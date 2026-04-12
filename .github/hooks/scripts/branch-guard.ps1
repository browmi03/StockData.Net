# branch-guard.ps1 - PreToolUse hook
# Blocks ALL file-editing tools when the current branch is 'main'.
# The feature branch must be created BEFORE any work begins - including
# documentation, feature specs, and source code. Everything lands on the
# branch and is reviewed together via PR.

$ErrorActionPreference = 'SilentlyContinue'

# Read hook input from stdin
$inputJson = $null
try {
    $inputJson = [Console]::In.ReadToEnd() | ConvertFrom-Json
} catch {
    # If we can't read input, allow the tool to proceed
    exit 0
}

$toolName = $inputJson.tool_name
if (-not $toolName) {
    exit 0
}

# Tools that modify files in the workspace
$editTools = @(
    'create_file',
    'replace_string_in_file',
    'multi_replace_string_in_file',
    'edit_notebook_file',
    'editFiles'
)

# Only guard file-editing tools - read-only tools always pass through
if ($toolName -notin $editTools) {
    exit 0
}

# Check current branch
$branch = git rev-parse --abbrev-ref HEAD 2>$null
if (-not $branch) {
    # Not a git repo — allow
    exit 0
}

if ($branch -ne 'main') {
    # Already on a feature branch - allow all edits
    exit 0
}

# On main - DENY all file edits (code, docs, config - everything)
$result = @{
    hookSpecificOutput = @{
        hookEventName           = 'PreToolUse'
        permissionDecision      = 'deny'
        permissionDecisionReason = "BLOCKED: You are on the 'main' branch. Create a feature branch FIRST before editing any files (including documentation). Use: git checkout -b feat/issue-<N>-<description>"
        additionalContext        = "Branch policy: The entire feature - specs, architecture docs, code, tests - must be created on a feature branch. Create the branch BEFORE starting any work. Merge to main only via PR after QA PASS."
    }
} | ConvertTo-Json -Depth 4

Write-Output $result
exit 0

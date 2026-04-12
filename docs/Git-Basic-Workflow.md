# Git Workflow for Developers (GitHub Style)

## Mandatory Pull Request Policy

**All changes to `main` (or the default branch) MUST go through a Pull Request (PR).** Direct pushes to `main` are forbidden.

- **No exceptions.** Every change — features, bug fixes, hotfixes, documentation updates — requires a PR.
- **Every PR requires at least one code review approval** before merging.
- **Branch protection rules** should be enabled on `main` to enforce this at the GitHub level (require PR reviews, require status checks to pass, disable force pushes).
- **Squash and merge** is the preferred merge strategy for clean history.
- **Delete the feature branch** after merging to keep the repository clean.

When agents perform git operations, the Orchestration agent must request QA pre-approval before any commit, push, PR creation, or merge. See the orchestration and lead-developer agent definitions for the full workflow.

---

## For Developers Coming from TFS

This guide is for developers working on **existing projects** who need to make changes, get code reviews, and commit using the **GitHub Pull Request workflow**.

### Key Differences from TFS

| TFS | Git + GitHub |
| --- | ------------ |
| Check out files | No need - just edit |
| Shelvesets | Git stash or feature branches |
| Check in directly | Create Pull Request for review |
| Centralized | Distributed (work offline) |
| Single branch workspace | Easy branch switching |

---

## Quick Start - First Time Setup

### One-Time Configuration

```powershell
# Set your identity
git config --global user.name "Your Name"
git config --global user.email "your.email@company.com"

# Optional: Set VS Code as editor
git config --global core.editor "code --wait"
```

### Clone the Repository (First Time Only)

```powershell
# Clone your team's repository
git clone https://github.com/your-org/your-project.git
cd your-project
```

---

## Daily Workflow - The GitHub Way

### Core Concepts

- **main/master**: The primary branch (like TFS main branch)
- **Feature Branch**: Your working branch (like having a workspace)
- **Commit**: Local save point (lightweight, done often)
- **Push**: Upload your commits to GitHub
- **Pull Request (PR)**: Request for code review and merge (like TFS Code Review + Check-in combined)

### Branching Strategies for Multiple Developers

#### Option 1: Individual Developer Branches (Recommended for Complex Features)

```text
main
  └─ feature/user-management (shared feature branch)
       ├─ dev/john/user-api
       ├─ dev/sarah/user-ui
       └─ dev/mike/user-tests
```

- Each developer creates their own branch off the feature branch
- Developers create PRs to merge into the **feature branch**
- Once feature is complete, create one PR from **feature branch → main**
- **Use when:** Large feature, multiple devs, work can be divided

#### Option 2: Shared Feature Branch (Recommended for Small Teams/Features)

```text
main
  └─ feature/add-login (all devs work here)
```

- All developers work directly on the same feature branch
- Pull frequently to get others' changes: `git pull origin feature/add-login`
- Create one PR from **feature branch → main** when complete
- **Use when:** Small feature, 2-3 devs, closely related work

#### Option 3: Individual Feature Branches (Most Common - Single Developer per Feature)

```text
main
  ├─ feature/john-user-api
  ├─ feature/sarah-login-ui
  └─ feature/mike-unit-tests
```

- Each developer works on their own feature independently
- Each creates their own PR to **main**
- **Use when:** Features are independent, one dev per feature

---

## Step-by-Step: Making Changes and Getting Code Review

### Single Developer Workflow (Most Common)

This is the basic workflow when you're working on a feature by yourself.

### Step 1: Get Latest Code

```powershell
# Navigate to your project
cd C:\Projects\your-project

# Switch to main branch
git checkout main

# Get latest changes (like TFS "Get Latest")
git pull origin main
```

### Step 2: Create Your Feature Branch

```powershell
# Create and switch to a new branch for your work
# Branch naming: feature/description or bugfix/ticket-number
git checkout -b feature/add-login-page

# Verify you're on the new branch
git branch
```

**Note for TFS users:** No need to "check out" files. Just start editing!

---

## Multi-Developer Workflows

### Scenario A: Multiple Devs on Same Feature (Shared Branch)

**Initial Setup (First Developer):**

```powershell
# First dev creates the feature branch
git checkout main
git pull origin main
git checkout -b feature/user-management
git push -u origin feature/user-management
```

**Other Developers Join:**

```powershell
# Other devs checkout the existing feature branch
git checkout main
git pull origin main
git checkout -b feature/user-management origin/feature/user-management
```

**Daily Work on Shared Branch:**

```powershell
# ALWAYS pull before starting work
git checkout feature/user-management
git pull origin feature/user-management

# Make your changes
# ... edit files ...

# Commit and push
git add .
git commit -m "Add user validation logic"
git pull origin feature/user-management  # Get any new changes
git push origin feature/user-management
```

**When Feature is Complete:**

- One person creates PR: `feature/user-management` → `main`
- Team reviews together
- Merge to main

### Scenario B: Developer Branches to Feature Branch

**Setup:**

```powershell
# Lead creates feature branch
git checkout main
git pull origin main
git checkout -b feature/user-management
git push -u origin feature/user-management

# Each developer creates their own branch FROM feature branch
git checkout feature/user-management
git checkout -b dev/john/user-api
```

**Individual Developer Work:**

```powershell
# Work on your developer branch
git add .
git commit -m "Implement user API endpoints"
git push -u origin dev/john/user-api

# Create PR: dev/john/user-api → feature/user-management
# After PR approved and merged, delete your dev branch
```

**Keep Your Dev Branch Updated:**

```powershell
# Get latest from feature branch
git checkout feature/user-management
git pull origin feature/user-management
git checkout dev/john/user-api
git merge feature/user-management
```

**Final Integration:**

- After all dev branches merged to feature branch
- Create PR: `feature/user-management` → `main`
- Team reviews
- Merge to main

### Scenario C: Independent Features (Separate Branches)

This is the most common - each developer works independently.

```powershell
# John works on API
git checkout -b feature/john-user-api
# ... work, commit, push ...
# PR: feature/john-user-api → main

# Sarah works on UI (independent)
git checkout main
git checkout -b feature/sarah-login-ui
# ... work, commit, push ...
# PR: feature/sarah-login-ui → main
```

Each developer creates their own PR directly to main.

---

## Choosing the Right Strategy

| Scenario | Strategy | Branch Structure |
| -------- | -------- | ---------------- |
| Single dev, single feature | Individual feature branch | `feature/name` → `main` |
| 2-3 devs, small feature, tight collaboration | Shared feature branch | All work on `feature/name` → `main` |
| 3+ devs, large feature, divisible work | Developer branches | `dev/name/task` → `feature/name` → `main` |
| Multiple independent features | Individual feature branches | Each dev: `feature/their-work` → `main` |

**Most teams use a mix:** Individual branches for most work, shared branches for pair programming or small features.

---

## Working with Others - Best Practices

### Communication

1. **Announce your work** - Tell team what branch you're working on
2. **Coordinate on shared branches** - Use Slack/Teams to avoid conflicts
3. **Small, frequent commits** - Easier for others to integrate
4. **Pull often** - Stay synced with team changes

### Avoiding Conflicts

```powershell
# On shared branches - pull before every work session
git pull origin feature/shared-branch

# Before pushing
git pull origin feature/shared-branch
git push origin feature/shared-branch
```

### Handling Conflicts When Multiple Devs Push

```powershell
# You try to push but someone else pushed first
git push
# Error: Updates were rejected

# Pull their changes
git pull origin feature/branch-name
# If conflicts, edit files and resolve

# After resolving
git add .
git commit -m "Merge changes from teammate"
git push origin feature/branch-name
```

---

## Original Single Developer Workflow

### Step 3: Make Your Changes

```powershell
# Edit your files in VS Code, Visual Studio, etc.
# You can edit any file without checking them out

# Check what files you've changed
git status

# See the actual changes you made
git diff
```

### Step 4: Stage and Commit Your Changes

```powershell
# Stage specific files
git add src/login.js
git add src/login.css

# Or stage all changes
git add .

# Commit with a descriptive message
git commit -m "Add login page with email validation"

# Continue working - you can make multiple commits
# Edit more files...
git add .
git commit -m "Add password strength indicator"
```

**Commit Often:** Unlike TFS, commits are local and cheap. Commit after each logical change!

### Step 5: Push Your Branch to GitHub

```powershell
# First time pushing this branch
git push -u origin feature/add-login-page

# This uploads your branch to GitHub for code review
```

### Step 6: Create Pull Request (Code Review)

1. Go to your repository on **GitHub.com**
2. You'll see a banner: **"Compare & pull request"** - click it
3. Fill in the PR details:
   - **Title**: Clear description (e.g., "Add login page functionality")
   - **Description**:
     - What changed
     - Why it changed
     - How to test it
     - Link to work item: "Fixes #123"
4. **Assign reviewers** (your team members)
5. Click **"Create Pull Request"**

**This is like TFS Code Review!** Your code is now waiting for approval.

### Step 7: Address Code Review Feedback

```powershell
# Reviewers request changes? Just make them locally:
# Edit your files based on feedback

# Stage and commit the changes
git add .
git commit -m "Address review feedback: improve error handling"

# Push the updates
git push

# The Pull Request automatically updates! No need to create a new one.
```

### Step 8: PR Approved - Merge It

Once approved:

1. Go to your PR on GitHub
2. Click **"Merge pull request"**
3. Choose merge strategy (usually **"Squash and merge"**)
4. Click **"Confirm merge"**
5. Click **"Delete branch"** on GitHub

**This is the actual "check-in" moment!** Your code is now in main.

### Step 9: Clean Up Locally

```powershell
# Switch back to main
git checkout main

# Get the latest (including your merged changes)
git pull origin main

# Delete your local feature branch
git branch -d feature/add-login-page

# Done! Ready for next task.
```

---

## Quick Reference - Daily Commands

**Starting new work:**

```powershell
git checkout main
git pull origin main
git checkout -b feature/my-feature
```

**During development:**

```powershell
git status                    # What changed?
git diff                      # Show my changes
git add .                     # Stage changes
git commit -m "Description"   # Save changes locally
```

**Before lunch/end of day (save your work):**

```powershell
git push -u origin feature/my-feature    # First time
git push                                 # After that
```

**After PR merged:**

```powershell
git checkout main
git pull origin main
git branch -d feature/my-feature
```

---

## Common Scenarios for TFS Users

### "I need to switch to work on something else urgently"

**TFS Way:** Shelve your changes

**Git Way:** Commit to your branch or use stash

```powershell
# Option 1: Just commit your work in progress
git add .
git commit -m "WIP: In progress work"
git push

# Then switch to main and create new branch
git checkout main
git pull origin main
git checkout -b hotfix/urgent-bug

# Later, come back and continue
git checkout feature/my-feature
```

Or use stash (similar to shelvesets):

```powershell
# Save your changes temporarily
git stash save "Work in progress on login"

# Do your urgent work...
git checkout main
git checkout -b hotfix/urgent-fix

# Later, restore your changes
git checkout feature/my-feature
git stash pop
```

### "Someone else changed the same file - conflicts!"

**When merging main into your branch:**

```powershell
# Update your branch with latest main
git checkout main
git pull origin main
git checkout feature/my-feature
git merge main

# If conflicts occur, Git will tell you which files
# Edit the files - look for conflict markers:
# <<<<<<< HEAD
# your changes
# =======
# their changes
# >>>>>>> main

# After fixing conflicts
git add conflicted-file.js
git commit -m "Merge main and resolve conflicts"
```

### "I made a mistake in my last commit"

**Before pushing:**

```powershell
# Fix the files
# Then amend the commit
git add .
git commit --amend --no-edit
```

**After pushing:**

```powershell
# Make the fix
git add .
git commit -m "Fix issue from previous commit"
git push
```

### "I need to see what changed between my branch and main"

```powershell
# See all changes
git diff main

# See list of files changed
git diff --name-only main

# See commit history
git log main..HEAD --oneline
```

### "My branch is old and behind main"

```powershell
# Get latest main
git checkout main
git pull origin main

# Update your feature branch
git checkout feature/my-feature
git merge main

# If there are conflicts, resolve them (see above)
```

### "I want to undo my local changes"

```powershell
# Discard changes in one file
git checkout -- filename.js

# Or with newer syntax
git restore filename.js

# Discard ALL local changes (careful!)
git restore .
```

---

## Essential Commands Reference

### Most Used Commands

```powershell
git status              # What's changed?
git diff                # Show my changes
git add .               # Stage all changes
git commit -m "msg"     # Commit changes
git push                # Upload to GitHub
git pull                # Download latest
git checkout branch     # Switch branches
git log --oneline       # View history
```

### Checking Status

```powershell
git status              # Current changes
git log                 # Commit history
git log --oneline       # Compact history
git diff                # Unstaged changes
git diff --staged       # Staged changes
```

### Branching

```powershell
git branch                      # List branches
git checkout -b feature/name    # Create and switch
git checkout main               # Switch to main
git branch -d feature/name      # Delete branch (after merged)
```

### Syncing

```powershell
git pull origin main            # Get latest from main
git push                        # Push commits
git push -u origin branch-name  # First push of new branch

# Delete branch
git branch -d feature-name      # Safe delete (only if merged)
git branch -D feature-name      # Force delete
```

---

## Best Practices for Code Quality

### Write Good Commit Messages

```text
Add user authentication with OAuth 2.0

- Implement login/logout functionality
- Add password reset workflow
- Integrate with Azure AD
- Fixes #123
```

**Format:**

- First line: Brief summary (50 chars or less)
- Blank line
- Detailed description if needed
- Reference work items

### Examples

- ✅ `Add email validation to registration form`
- ✅ `Fix null reference exception in payment processor`
- ✅ `Refactor user service to use async/await`
- ❌ `Fixed stuff`
- ❌ `Update`
- ❌ `Changes`

### Pull Request Best Practices

**Good PR Description:**

```markdown
## What Changed
Added user profile page with avatar upload

## Why
Users need to be able to update their profile information

## How to Test
1. Log in as test user
2. Navigate to /profile
3. Upload an image
4. Verify it displays correctly

## Screenshots
[Attach screenshots]

Closes #456
```

### General Tips

1. **Commit frequently** - Small, logical commits are easier to review
2. **Pull before you push** - Stay in sync with your team
3. **Test before PR** - Make sure your code works
4. **Keep PRs focused** - One feature/fix per PR
5. **Respond to reviews quickly** - Don't let PRs go stale
6. **Be respectful in reviews** - Constructive feedback only

---

## Quick Command Reference

| Task | Command |
| ---- | ------- |
| Get latest code | `git checkout main` then `git pull origin main` |
| Start new work | `git checkout -b feature/task-name` |
| See my changes | `git status` or `git diff` |
| Save changes | `git add .` then `git commit -m "message"` |
| Upload for review | `git push -u origin feature/task-name` |
| Update my branch | `git merge main` (while on feature branch) |
| After PR merged | `git checkout main`, `git pull`, `git branch -d feature/name` |
| Undo local changes | `git restore filename` or `git restore .` |
| Save work temporarily | `git stash` (restore with `git stash pop`) |
| View history | `git log --oneline` |

---

## Helpful Resources

- [GitHub Flow Guide](https://guides.github.com/introduction/flow/)
- [Git Documentation](https://git-scm.com/doc)
- [GitHub Skills](https://skills.github.com/) - Interactive tutorials
- [Oh Shit, Git!?!](https://ohshitgit.com/) - Fix common mistakes

---

## Summary - The GitHub Pull Request Workflow

```text
1. Get latest     → git checkout main && git pull origin main
2. Create branch  → git checkout -b feature/my-work
3. Make changes   → Edit files (no checkout needed!)
4. Commit often   → git add . && git commit -m "message"
5. Push branch    → git push -u origin feature/my-work
6. Create PR      → On GitHub.com
7. Code Review    → Team reviews, you address feedback
8. Approved?      → Merge PR on GitHub
9. Clean up       → git checkout main && git pull && git branch -d feature/my-work
10. Repeat        → Start next task
```

**Welcome to the GitHub workflow! You've got this!** 🚀

---

**Last Updated:** February 2026

```bash
# Add remote repository
git remote add origin https://github.com/username/repo.git

# View remotes
git remote -v

# Fetch changes from remote (doesn't merge)
git fetch origin

# Pull changes from remote (fetch + merge)
git pull origin main

# Push changes to remote
git push origin branch-name

# Push and set upstream
git push -u origin branch-name

# Push all branches
git push --all origin
```

### Merging

```bash
# Merge branch into current branch
git merge feature-branch

# Abort merge if conflicts arise
git merge --abort
```

### Undoing Changes

```bash
# Discard changes in working directory
git checkout -- filename.txt
# or (newer syntax)
git restore filename.txt

# Unstage file (keep changes)
git reset HEAD filename.txt
# or (newer syntax)
git restore --staged filename.txt

# Discard all local changes
git reset --hard HEAD

# Undo last commit (keep changes)
git reset --soft HEAD~1

# Undo last commit (discard changes)
git reset --hard HEAD~1

# Revert a commit (creates new commit)
git revert commit-hash
```

---

## Branching Strategy

### Common Branching Models

#### 1. **Git Flow** (For Release-Based Projects)

- **main**: Production-ready code
- **develop**: Integration branch for features
- **feature/**: New features (`feature/user-authentication`)
- **release/**: Release preparation (`release/v1.2.0`)
- **hotfix/**: Emergency production fixes (`hotfix/security-patch`)

#### 2. **GitHub Flow** (For Continuous Deployment)

- **main**: Always deployable
- **feature branches**: All work done in branches from main
- Use Pull Requests for code review
- Merge to main = deploy

#### 3. **Trunk-Based Development**

- **main**: Single source of truth
- Short-lived feature branches (1-2 days max)
- Frequent integration to main

---

## Collaboration Workflow

### Standard Team Workflow

1. **Pull Latest Changes**

   ```bash
   git checkout main
   git pull origin main
   ```

2. **Create Feature Branch**

   ```bash
   git checkout -b feature/new-feature
   ```

3. **Work and Commit**

   ```bash
   # Make changes
   git add .
   git commit -m "Add new feature functionality"
   ```

4. **Keep Branch Updated**

   ```bash
   # Regularly sync with main
   git checkout main
   git pull origin main
   git checkout feature/new-feature
   git merge main
   # or use rebase for cleaner history
   git rebase main
   ```

5. **Push Feature Branch**

   ```bash
   git push -u origin feature/new-feature
   ```

6. **Create Pull Request**
   - Go to GitHub/GitLab/Bitbucket
   - Create PR from feature branch to main
   - Request code review
   - Address feedback

7. **Merge and Cleanup**

   ```bash
   # After PR is merged
   git checkout main
   git pull origin main
   git branch -d feature/new-feature
   ```

---

## Best Practices

### Commit Messages

**Good commit message format:**

```text
Short summary (50 chars or less)

More detailed explanation if needed (wrap at 72 chars).
- Bullet points are okay
- Use present tense: "Add feature" not "Added feature"
- Reference issue numbers: "Fixes #123"

Include context about why the change was made.
```

**Examples:**

- ✅ `Add user authentication with JWT tokens`
- ✅ `Fix memory leak in image processor`
- ✅ `Refactor database connection logic`
- ❌ `Fixed stuff`
- ❌ `Update`
- ❌ `asdfgh`

### General Best Practices

1. **Commit Often**: Make small, logical commits
2. **Write Descriptive Messages**: Future you will thank you
3. **Pull Before Push**: Always pull latest changes before pushing
4. **Review Before Committing**: Use `git diff` to review your changes
5. **Don't Commit Sensitive Data**: Use `.gitignore` for secrets, credentials
6. **Keep Branches Short-Lived**: Merge or delete within days
7. **Use `.gitignore`**: Exclude build artifacts, dependencies, IDE files
8. **Test Before Committing**: Ensure code works before committing
9. **One Feature Per Branch**: Keep changes focused and reviewable
10. **Rebase Carefully**: Never rebase public/shared branches

### .gitignore Example

```gitignore
# Dependencies
node_modules/
vendor/

# Build outputs
dist/
build/
*.exe
*.dll

# Environment files
.env
.env.local

# IDE files
.vscode/
.idea/
*.swp

# OS files
.DS_Store
Thumbs.db

# Logs
*.log
logs/
```

---

## Common Scenarios

### Scenario 1: Resolve Merge Conflicts

```bash
# Start merge
git merge feature-branch

# Conflicts occur
# Edit conflicted files manually, looking for:
# <<<<<<< HEAD
# your changes
# =======
# incoming changes
# >>>>>>> feature-branch

# After resolving conflicts
git add resolved-file.txt
git commit -m "Merge feature-branch and resolve conflicts"
```

### Scenario 2: Accidentally Committed to Main

```bash
# Create a branch from current state
git branch feature-branch

# Reset main to previous commit
git reset --hard origin/main

# Switch to feature branch
git checkout feature-branch
```

### Scenario 3: Need to Switch Branches with Uncommitted Changes

```bash
# Option 1: Stash changes
git stash save "Work in progress"
git checkout other-branch
# Later, return and apply stash
git checkout original-branch
git stash pop

# Option 2: Commit changes
git commit -m "WIP: Temporary commit"
# Later, amend or reset as needed
```

### Scenario 4: Forgot to Create Feature Branch

```bash
# If you haven't committed yet
git stash
git checkout -b feature-branch
git stash pop

# If you already committed
git branch feature-branch
git reset --hard origin/main
git checkout feature-branch
```

### Scenario 5: Undo Published Commits

```bash
# Use revert (safe for shared branches)
git revert commit-hash

# This creates a new commit that undoes the changes
```

### Scenario 6: Update Commit Message of Last Commit

```bash
# If not pushed yet
git commit --amend -m "New commit message"

# If already pushed (avoid if others have pulled)
git commit --amend -m "New commit message"
git push --force-with-lease
```

### Scenario 7: Cherry-Pick Specific Commits

```bash
# Apply a specific commit from another branch
git cherry-pick commit-hash
```

---

## Quick Reference Cheat Sheet

| Command | Description |
| ------- | ----------- |
| `git init` | Initialize new repository |
| `git clone <url>` | Clone remote repository |
| `git status` | Show working tree status |
| `git add <file>` | Stage file |
| `git commit -m "<msg>"` | Commit staged changes |
| `git push` | Push commits to remote |
| `git pull` | Fetch and merge remote changes |
| `git branch` | List branches |
| `git checkout <branch>` | Switch branches |
| `git checkout -b <branch>` | Create and switch to new branch |
| `git merge <branch>` | Merge branch into current |
| `git log` | View commit history |
| `git diff` | Show unstaged changes |
| `git stash` | Temporarily save changes |
| `git reset --hard` | Discard all local changes |

---

## Additional Resources

- [Official Git Documentation](https://git-scm.com/doc)
- [GitHub Git Guides](https://github.com/git-guides)
- [Atlassian Git Tutorials](https://www.atlassian.com/git/tutorials)
- [Learn Git Branching (Interactive)](https://learngitbranching.js.org/)
- [Oh Shit, Git!?! (Fix Common Mistakes)](https://ohshitgit.com/)

---

## Conclusion

Mastering Git takes practice, but understanding this basic workflow will make you a more effective developer. Start with the essentials, and gradually incorporate more advanced techniques as you gain confidence.

Remember: **Commit early, commit often, and write good commit messages!**

---

**Last Updated:** February 2026

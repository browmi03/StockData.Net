# GitHub Secrets Validation

This document defines the CI/CD secret validation flow for multi-provider deployment.

## Required Repository Secrets

- `FINNHUB_API_KEY`
- `POLYGON_API_KEY`
- `ALPHAVANTAGE_API_KEY`

These values are set manually by a repository maintainer in GitHub:

1. Open repository `Settings`.
2. Select `Secrets and variables` -> `Actions`.
3. Add each required secret.
4. Save and rerun workflow.

## Recommended Workflow Validation Step

Add this step before integration tests to fail fast when a secret is missing.

```yaml
- name: Validate required GitHub secrets in secure contexts
  if: github.event_name == 'push' || (github.event_name == 'pull_request' && github.event.pull_request.head.repo.fork == false)
  shell: bash
  run: |
    missing=()

    [[ -z "${{ secrets.FINNHUB_API_KEY }}" ]] && missing+=("FINNHUB_API_KEY")
    [[ -z "${{ secrets.POLYGON_API_KEY }}" ]] && missing+=("POLYGON_API_KEY")
    [[ -z "${{ secrets.ALPHAVANTAGE_API_KEY }}" ]] && missing+=("ALPHAVANTAGE_API_KEY")

    if [ ${#missing[@]} -gt 0 ]; then
      echo "Required GitHub secrets are missing for secure-context integration tests: ${missing[*]}"
      exit 1
    fi
```

## Secret Materialization Pattern

Use an ephemeral file for runtime configuration and remove it after tests.

```yaml
- name: Mask secret values
  shell: bash
  env:
    FINNHUB_API_KEY: ${{ secrets.FINNHUB_API_KEY }}
    POLYGON_API_KEY: ${{ secrets.POLYGON_API_KEY }}
    ALPHAVANTAGE_API_KEY: ${{ secrets.ALPHAVANTAGE_API_KEY }}
  run: |
    for value in "$FINNHUB_API_KEY" "$POLYGON_API_KEY" "$ALPHAVANTAGE_API_KEY"; do
      if [ -n "$value" ]; then
        echo "::add-mask::$value"
      fi
    done

- name: Materialize secrets.json for integration tests
  shell: bash
  env:
    FINNHUB_API_KEY: ${{ secrets.FINNHUB_API_KEY }}
    POLYGON_API_KEY: ${{ secrets.POLYGON_API_KEY }}
    ALPHAVANTAGE_API_KEY: ${{ secrets.ALPHAVANTAGE_API_KEY }}
  run: |
    FINNHUB_VALUE="${FINNHUB_API_KEY:-<missing-from-github-secrets>}"
    POLYGON_VALUE="${POLYGON_API_KEY:-<missing-from-github-secrets>}"
    ALPHAVANTAGE_VALUE="${ALPHAVANTAGE_API_KEY:-<missing-from-github-secrets>}"

    cat > secrets.json <<JSON
    {
      "Providers": {
        "Finnhub": { "ApiKey": "$FINNHUB_VALUE" },
        "Polygon": { "ApiKey": "$POLYGON_VALUE" },
        "AlphaVantage": { "ApiKey": "$ALPHAVANTAGE_VALUE" }
      }
    }
    JSON

- name: Remove ephemeral secrets file
  if: always()
  shell: bash
  run: rm -f secrets.json
```

## Secret Scanning in CI

CI runs gitleaks on push and pull request in `test.yml` and fails the workflow if a secret-like value is detected in the repository history or working tree.

## Developer Provisioning Checklist

- Request provider API keys from an authorized owner.
- Configure provider API keys in local deployment `appsettings.json` only.
- Keep `appsettings.json` placeholders unchanged in source control.
- Never print, log, or commit key values.

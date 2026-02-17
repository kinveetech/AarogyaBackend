# Main Branch Guardrails

This repository includes CI and PR guardrail workflows that are intended to be required on `main`.

## SonarQube Setup

To enable SonarQube analysis workflow (`.github/workflows/sonarqube.yml`), configure:
- Repository secret: `SONAR_HOST_URL`
- Repository secret: `SONAR_TOKEN`
- Optional repository variable: `SONAR_PROJECT_KEY` (default: `kinveetech_AarogyaBackend`)
- Optional repository variable: `SONAR_PROJECT_NAME` (default: `AarogyaBackend`)

The workflow blocks on quality gate (`sonar.qualitygate.wait=true`) and should be required for merges to `main`.

## Ruleset (Preferred)

Apply the prepared ruleset:

```bash
gh api --method POST repos/kinveetech/AarogyaBackend/rulesets --input .github/rulesets/main-ruleset.json
```

The ruleset enforces:
- no branch deletion
- no force pushes
- PR required with at least 1 approval
- stale review dismissal on new pushes
- last-push approval required
- review thread resolution required
- required status checks:
  - `.NET Backend CI / build-and-test`
  - `.NET Backend CI / lint`
  - `PR Guardrails / semantic-pr-title`
  - `PR Guardrails / dependency-review`
  - `SonarQube Analysis / sonarqube`

## If Rulesets Are Unavailable

If GitHub returns `HTTP 403` (private-repo feature limitation), use GitHub UI branch protection on `main` to mirror the same settings, and keep this ruleset JSON for later activation.

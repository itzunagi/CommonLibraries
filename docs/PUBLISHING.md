# Publishing NuGet packages to CodeArtifact

Workflow: `.github/workflows/publish-nuget.yml`. It runs only on pushes/merges to `master`. A package is published only when its `.csproj` `<Version>` value changed in that push. To release, bump `<Version>` in the project file (e.g. `1.0.2$(VersionSuffix)`).

## One-time AWS setup

1. Create the GitHub OIDC provider (`https://token.actions.githubusercontent.com`, audience `sts.amazonaws.com`) if not present.
2. Create an IAM role with this trust policy (restricts to this repo's `codeartifact` environment):

```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": {"Federated": "arn:aws:iam::<ACCOUNT_ID>:oidc-provider/token.actions.githubusercontent.com"},
    "Action": "sts:AssumeRoleWithWebIdentity",
    "Condition": {
      "StringEquals": {
        "token.actions.githubusercontent.com:aud": "sts.amazonaws.com",
        "token.actions.githubusercontent.com:sub": "repo:itzunagi@<OWNER ID>/CommonLibraries@<REPO ID>:environment:codeartifact",
        "token.actions.githubusercontent.com:ref": "refs/heads/master"
      }
    }
  }]
}
```

Exact sub string can be retrieved from GitHub CLI with `gh api repos/itzunagi/CommonLibraries/actions/oidc/customization/sub`

3. Attach a least-privilege policy (no delete permissions):

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {"Effect": "Allow", "Action": "codeartifact:GetAuthorizationToken", "Resource": "arn:aws:codeartifact:<REGION>:<ACCOUNT_ID>:domain/<DOMAIN>"},
    {"Effect": "Allow", "Action": "sts:GetServiceBearerToken", "Resource": "*", "Condition": {"StringEquals": {"sts:AWSServiceName": "codeartifact.amazonaws.com"}}},
    {"Effect": "Allow", "Action": ["codeartifact:GetRepositoryEndpoint", "codeartifact:ReadFromRepository", "codeartifact:PublishPackageVersion", "codeartifact:PutPackageMetadata"], "Resource": ["arn:aws:codeartifact:<REGION>:<ACCOUNT_ID>:repository/<DOMAIN>/<REPO>", "arn:aws:codeartifact:<REGION>:<ACCOUNT_ID>:package/<DOMAIN>/<REPO>/nuget/*/*"]}
  ]
}
```

## GitHub setup

Create an environment named `codeartifact` (Settings > Environments); restrict deployment branches to `master` and optionally add a required reviewer. Add to it:

- Secrets: `AWS_ROLE_ARN`, `CODEARTIFACT_DOMAIN_OWNER` (account ID)
- Variables: `AWS_REGION`, `CODEARTIFACT_DOMAIN`, `CODEARTIFACT_REPOSITORY`

Also enable branch protection on `master` (require PRs and reviews, restrict who can push), and require approval for workflows from outside contributors. Never add `pull_request_target`/`pull_request` triggers to the publish job.

## Notes

- Actions are pinned to commit SHAs; Dependabot keeps them updated.
- Pushes of an already-published version are skipped (`--skip-duplicate`), so re-runs are safe.
- Changed packages are packed/pushed in dependency order.

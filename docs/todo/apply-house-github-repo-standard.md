---
title: Apply the house GitHub repo standard to result
summary: Catalog of the repo settings, workflows, and Dependabot config that plumber and pool share; result now has everything except the NUGET_API_KEY secret and the deferred code_scanning gate.
tags: [github, repo-standard, ci, house-canon]
created: 2026-07-28
priority: medium
effort: low
status: open
---

Bring `marklauter/result` in line with `marklauter/plumber` and
`marklauter/pool`, the two reference repos for the house standard.

## Done

**Branch ruleset `main`** (id `19932131`, enforcement `active`) — identical to
plumber's and pool's: target `~DEFAULT_BRANCH`; rules `deletion`,
`non_fast_forward`, `required_linear_history`, `pull_request`; 0 required
approvals, dismiss stale reviews on push, no code-owner review, no last-push
approval, no review-thread resolution, all three merge methods;
`bypass_actors: []` and `current_user_can_bypass: never`, so direct pushes to
`main` are refused for the owner too.

**Repo settings** — `delete_branch_on_merge`, `allow_auto_merge`,
`allow_squash_merge`, `allow_rebase_merge`, `allow_merge_commit` all `true`;
`squash_merge_commit_title` `COMMIT_OR_PR_TITLE`; `squash_merge_commit_message`
`COMMIT_MESSAGES`; fork-PR CI approval policy `all_external_contributors`.

**Description and topics** — set 2026-07-28. Topics mirror the csproj
`PackageTags`.

**Workflows** — all four ported from pool on branch `scaffold-results`:

- `dotnet.tests.yml` — format check, then build and test on the Debug/Release
  matrix, on PR and push to `main`. The `samples/**` path filter was dropped
  (result has no samples) and the coverage artifact points at
  `tests/Results.Tests/coverage.cobertura.xml`.
- `dotnet.publish.yml` — packs `Results.slnx` on release and pushes to
  nuget.org. The version comes from the release tag (`v1.2.3` →
  `-p:PackageVersion=1.2.3`), so nothing version-related belongs in the repo.
  `workflow_dispatch` gives a pack-and-upload dry run with no push.
- `codeql.yml` — committed advanced setup, not GitHub's API default setup.
  Both `actions` and `csharp` use `build-mode: none`.
- `dependabot-auto-merge.yml` — auto-merges patch and minor Dependabot PRs;
  majors stay manual.

**Composite action** — `.github/actions/setup-dotnet/action.yml`, ported from
pool. Installs the SDK via `global-json-file: global.json` and caches
`~/.nuget/packages`, so the CI SDK pin flows from `global.json` and no workflow
hardcodes `dotnet-version`.

**Dependabot** — `.github/dependabot.yml`, weekly, open-PR limit 10, NuGet and
github-actions ecosystems at `directory: "/"`.

**`.gitattributes`** — result had none; ported from pool (LF normalization,
CRLF for `.cmd`/`.bat`). The tests workflow path filter references it.

## Outstanding

**`NUGET_API_KEY`** — repository Actions secret, consumed by
`dotnet.publish.yml`. `gh api repos/marklauter/result/actions/secrets` returns
an empty list, so the publish job will fail at the push step on the first
release. Add it before tagging.

See also [add-package-icon.md](add-package-icon.md), which breaks `dotnet pack`
before the publish job reaches that step.

## Deferred

The `code_scanning` merge gate (block PRs on high CodeQL findings) is
deliberately left off until CodeQL has run at least once on this repo — adding
it first deadlocks merges under an admin-enforced ruleset. Open the
`scaffold-results` PR, let CodeQL complete, then add the gate.

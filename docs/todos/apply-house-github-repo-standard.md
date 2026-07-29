---
title: Apply the house GitHub repo standard to result
type: todo
summary: Catalog of the repo settings, workflows, and Dependabot config that plumber, pool, and dynamodblite share; result matches all of them.
tags: [github, repo-standard, ci, house-canon]
created: 2026-07-28
priority: medium
status: closed
---

Bring `marklauter/result` in line with `marklauter/plumber`,
`marklauter/pool`, and `marklauter/dynamodblite`, the reference repos for the
house standard.

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

## Resolution

Closed 2026-07-28. `result` matches `plumber`, `pool`, and `dynamodblite` on
every axis checked through the GitHub API:

| Axis | State |
| --- | --- |
| Branch ruleset rules | `deletion`, `non_fast_forward`, `required_linear_history`, `pull_request` — identical across all four, one ruleset each |
| Merge settings | squash, rebase, merge, auto-merge, delete-on-merge all `true`; `COMMIT_OR_PR_TITLE` / `COMMIT_MESSAGES` |
| Workflows | `codeql.yml`, `dependabot-auto-merge.yml`, `dotnet.publish.yml`, `dotnet.tests.yml` |
| Actions secrets | `NUGET_API_KEY` present |
| Code scanning | CodeQL analyses landing; result's most recent ran on `main` |

`NUGET_API_KEY` was the one open item and has since been added, so
`dotnet.publish.yml` will reach its push step. `dotnet pack` still needs
[add-package-icon.md](add-package-icon.md) before a release is worth cutting.

The `code_scanning` merge gate this note deferred is not part of the standard it
was measuring against. None of `plumber`, `pool`, or `dynamodblite` carries that
rule. The only repo that does is `hoplite`, which is Python, with
`alerts_threshold: errors` and `security_alerts_threshold: high_or_higher` on
tool `CodeQL`. Adopting it for the C# repos is a change to the standard rather
than a gap against it, and belongs in its own note if it is wanted.

The `scaffold-results` branch named above is merged and deleted.

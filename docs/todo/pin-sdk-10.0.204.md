---
title: Pin global.json to SDK 10.0.204 with rollForward disable
summary: global.json currently pins 10.0.100 / latestFeature; bump to 10.0.204 and set rollForward to disable to match the house SDK version.
tags: [todo, global-json, sdk, house-canon]
created: 2026-07-28
priority: medium
effort: low
status: open
---

Carried from plumber, which was scaffolded from the same template and has this
todo open too.

Update `global.json` to pin the .NET SDK to `10.0.204` and set `rollForward` to
`disable` rather than keeping `latestFeature`.

Current: version `10.0.100`, rollForward `latestFeature`.

Target (matches lexi, the house canon):

```json
{
  "sdk": {
    "version": "10.0.204",
    "rollForward": "disable"
  }
}
```

## CI implication

The `setup-dotnet` composite action installs via `global-json-file: global.json`,
so the CI SDK pin flows straight from this file — no workflow hardcodes
`dotnet-version`. With `rollForward: disable`, CI becomes hermetic: it installs
*exactly* `10.0.204` and fails loudly if the runner image doesn't ship it, rather
than silently rolling forward to a newer patch/feature band.

Before merging, confirm the GitHub-hosted runner image (`ubuntu-latest`) actually
carries `10.0.204`. If it doesn't, either the workflow must add an explicit
`dotnet-version: 10.0.204` to install it, or the pin will red the build — which is
the intended fail-loud behavior, not a regression.

Confirm the `Directory.Build.props` `TargetFramework` (`net10.0`) still aligns
after the bump, then run `dotnet build -c Release` and `dotnet test` to verify.

Coordinate with plumber so both repos move in the same change.

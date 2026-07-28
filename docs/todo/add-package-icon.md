---
title: Add images/result.png and unblock dotnet pack
summary: Results.csproj declares PackageIcon result.png but the repo has no images/ directory, so packing fails.
tags: [todo, packaging, nuget, blocker]
created: 2026-07-28
priority: high
effort: low
status: open
---

`src/Results/Results.csproj` sets `<PackageIcon>result.png</PackageIcon>` and
packs `..\..\images\result.png`. The file does not exist yet.

`dotnet build` is unaffected. `dotnet pack` fails:

```
NuGet.Build.Tasks.Pack.targets(222,5): error : Could not find a part of the
path 'D:\projects\result\result\images'.
```

That means `dotnet.publish.yml` reds on the first release, at the pack step,
before it ever reaches the nuget.org push.

Drop the artwork at `images/result.png`, then re-run `dotnet pack -c Release` to
confirm. pool keeps three files in `images/`: `pool.png`, `pool.svg`, and the
shared `msl.armory.small.png`. plumber keeps the same three plus comic and
sketch variants.

Both reference READMEs embed their artwork by raw GitHub URL, in this repo's case
`https://raw.githubusercontent.com/marklauter/result/main/images/result.png`.
Wire that into `README.md` alongside the MSL Armory mark once the files land.

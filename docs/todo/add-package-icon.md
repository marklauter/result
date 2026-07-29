---
title: Replace the placeholder package icon
summary: images/results-logo.png is a generated placeholder that says PLACEHOLDER on it; pack and README are wired to it and need real artwork.
tags: [todo, packaging, nuget, branding]
created: 2026-07-28
priority: medium
effort: low
status: open
---

`images/results-logo.png` is a stand-in, not artwork. It is a 256x256 slate tile
with a dashed border reading `R<T>` / `PLACEHOLDER` / `replace me`. It ships in
the package and renders on the nuget.org listing page and in the README, so it
needs replacing before the package is announced anywhere.

Everything is already wired to that filename. Replace the file in place and
nothing else has to change:

- `src/Results/Results.csproj` — `<PackageIcon>results-logo.png</PackageIcon>`
  and the `None Include="..\..\images\results-logo.png"` pack item.
- `README.md` — the raw GitHub URL above the MSL Armory mark.

Keep 256x256 RGBA PNG to match the house icons: `pool.png`, `plumber.png`, and
`msl.armory.small.png` are all that size.

Verify with `dotnet pack -c Release`, then confirm the icon is in the package:

```sh
unzip -l bin/MSL.Results.1.0.0.nupkg | grep png
```

pool keeps three files in `images/`: `pool.png`, `pool.svg`, and the shared
`msl.armory.small.png`. plumber keeps the same three plus comic and sketch
variants. An SVG source alongside the PNG would match both.

`images/msl.armory.small.png` is already correct — copied byte-identical from
pool (md5 `a617544f38dac741bab7eb4b67df89c2`, same file in plumber). Leave it.

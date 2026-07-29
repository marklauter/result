---
title: Replace the placeholder package icon
summary: images/results-logo.png is a generated placeholder that says PLACEHOLDER on it; pack and README are wired to it and need real artwork.
tags: [todo, packaging, nuget, branding]
created: 2026-07-28
priority: medium
effort: low
status: open
---

The logo is a stand-in, not artwork: a slate tile with a dashed border reading
`R<T>` / `PLACEHOLDER` / `replace me`. It ships in the package and renders on the
nuget.org listing page and in the README, so it needs replacing before the
package is announced anywhere.

There are two files, following plumber's split rather than pool's single-file
approach:

| File | Size | Used by |
| --- | --- | --- |
| `images/results-logo.png` | 256x256 | the README header, by raw GitHub URL |
| `images/results-logo.small.png` | 128x128 | the packed NuGet icon |

Replace both in place and nothing else has to change:

- `src/Results/Results.csproj` — `<PackageIcon>results-logo.small.png</PackageIcon>`
  and the `None Include="..\..\images\results-logo.small.png"` pack item. Only
  the small one is packed; the README's image resolves over HTTP.
- `README.md` — the raw GitHub URL above the MSL Armory mark.

Keep the sizes. 128x128 is NuGet's recommended icon size, and 256x256 matches
`pool.png`, `plumber.comic.small.png`, and `msl.armory.small.png`. The current
small file is a LANCZOS downscale of the 256; real artwork should be exported at
each size rather than resampled.

Verify with `dotnet pack -c Release`, then confirm the icon is in the package:

```sh
unzip -l bin/MSL.Results.1.0.0.nupkg | grep png
```

pool keeps three files in `images/`: `pool.png`, `pool.svg`, and the shared
`msl.armory.small.png`. plumber keeps the same three plus comic and sketch
variants. An SVG source alongside the PNG would match both.

`images/msl.armory.small.png` is already correct — copied byte-identical from
pool (md5 `a617544f38dac741bab7eb4b67df89c2`, same file in plumber). Leave it.

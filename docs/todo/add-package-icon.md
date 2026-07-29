---
title: Replace the placeholder package icon
summary: Both logo files are generated placeholders with PLACEHOLDER on their face; the csproj and the README are already wired to them and need real artwork.
tags: [packaging, nuget, branding]
created: 2026-07-28
priority: medium
status: open
---

The logo is a placeholder: a slate tile with a dashed border reading `R<T>` /
`PLACEHOLDER` / `replace me`. It ships in the package, so it renders on the
nuget.org listing page and in the README. Replace it before announcing the
package anywhere.

There are two files, following plumber, which uses a separate image for each
role. pool uses one image for both.

| File | Size | Used by |
| --- | --- | --- |
| `images/results-logo.png` | 256x256 | the README header, over HTTP by raw GitHub URL |
| `images/results-logo.small.png` | 128x128 | the packed NuGet icon |

Both are referenced by path, so replacing the files in place is the only step.
For reference, those paths are:

- `src/Results/Results.csproj` — `<PackageIcon>results-logo.small.png</PackageIcon>`
  and the `None Include="..\..\images\results-logo.small.png"` pack item. Only
  the small file is packed, because the README's image resolves over HTTP.
- `README.md` — the raw GitHub URL above the MSL Armory mark.

Keep the sizes. 128x128 is NuGet's recommended icon size, and 256x256 matches
`pool.png`, `plumber.comic.small.png`, and `msl.armory.small.png`. The current
small file is a LANCZOS downscale of the 256. Real artwork should be exported at
each size rather than resampled.

Verify with `dotnet pack -c Release`, then confirm the icon landed:

```sh
unzip -l src/Results/bin/Release/MSL.Results.1.0.0.nupkg | grep png
```

Both siblings keep an SVG source next to the PNG: pool has `pool.svg`, plumber
has `plumber.svg` and `plumber-ideas.svg`. result has no vector source, and
adding one would match them.

`images/msl.armory.small.png` is already correct, copied byte-identical from
pool (md5 `a617544f38dac741bab7eb4b67df89c2`, the same file in plumber). Leave
it alone.

# bubble-core

The core that BubbleBIM Pro and Lapin share, as NuGet packages:

| Package | What |
|---|---|
| `BubbleBim.Geometry` | the B-rep kernel: solids, extrusions, tessellation, projections, the weighted straight skeleton; no dependencies |
| `BubbleBim.Booleans` | booleans between solids through [manifold](https://github.com/elalish/manifold) (Apache-2.0), with its native library for macOS, Linux, Windows and the browser |
| `BubbleBim.Step` | STEP files (ISO 10303-21) read and written; an IFC file read into geometry, placements, materials and properties |

C# on .NET 10, MIT. The libraries are trimmable: they also run compiled to WebAssembly.

## Use

```xml
<PackageReference Include="BubbleBim.Geometry" Version="0.1.0" />
<PackageReference Include="BubbleBim.Booleans" Version="0.1.0" />
<PackageReference Include="BubbleBim.Step" Version="0.1.0" />
```

`BubbleBim.Booleans` carries manifold under `runtimes/<rid>/native/`, so a consumer needs no step of its own. For `browser-wasm` the package's `build/BubbleBim.Booleans.targets` links the static libraries into the consumer's WebAssembly (`WasmBuildNative`).

Working on the core and a consumer at once: check this repository out beside the consumer's (`../bubble-core`); BubbleBIM Pro and Lapin then reference the projects here instead of the packages. Their CI never has it beside, so CI always builds against published packages.

## Build

```sh
node scripts/build-manifold.mjs                  # manifold for this machine → native/<rid>/ (git, cmake, a C++ compiler; MSVC prompt on Windows)
node scripts/build-manifold.mjs --browser-wasm   # for the browser (the .NET wasm-tools workload)
dotnet build BubbleCore.slnx
dotnet test BubbleCore.slnx
```

## Release

A tag `v1.2.3` on `main` runs the release workflow: manifold built on macOS, Linux and Windows and for the browser, the tests on each, then one `dotnet pack` with every native library, pushed to nuget.org (the repository secret `NUGET_API_KEY`). The version comes from the tag (MinVer).

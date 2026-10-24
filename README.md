# AdamantiumVulkan

C# bindings to the Vulkan API, generated from the Khronos registry, with hand-written wrappers over the generated
interop so that calling Vulkan reads as C# rather than as marshalling.

The Vulkan bindings come from `vk.xml` and `video.xml` - the registry Khronos publishes as the specification's own
machine-readable form - so extensions, aliases, enum extensions and the structure chains arrive as the registry
defines them rather than as a parse of the headers happened to see them.

The other three packages have no registry to read from, so their bindings are generated from the C headers by
[QuantumBinding](https://github.com/AdamantiumStudio/QuantumBinding), which parses through Clang's own front end
rather than a bespoke parser: macros, bitfields, anonymous unions and platform conditionals mean there what they
mean to a C compiler.

These packages are what the [Adamantium](https://github.com/AdamantiumStudio/AdamantiumEngine) engine is built on, but they
carry no dependency on it and can be used on their own.

## Packages

| Package | What it is |
|---|---|
| [`Adamantium.Vulkan`](https://www.nuget.org/packages/Adamantium.Vulkan) | The bindings themselves: structures, handles, enums and commands |
| [`Adamantium.Vulkan.Loader`](https://www.nuget.org/packages/Adamantium.Vulkan.Loader) | Finds the Vulkan loader at run time and resolves its entry points |
| [`Adamantium.Vulkan.Slang`](https://www.nuget.org/packages/Adamantium.Vulkan.Slang) | Bindings to the Slang shader compiler, shipping the compiler itself and its language server, `slangd` |
| [`Adamantium.Vulkan.Spirv`](https://www.nuget.org/packages/Adamantium.Vulkan.Spirv) | Bindings to SPIRV-Cross, for reading back what a compiled shader declares |
| [`Adamantium.Vulkan.ShaderObjectLayer`](https://www.nuget.org/packages/Adamantium.Vulkan.ShaderObjectLayer) | Khronos' `VK_LAYER_KHRONOS_shader_object` for Windows x64, Linux x64 and macOS arm64, copied beside the application under `vulkan/layers/<rid>/`: shader objects on a driver without them |

```
dotnet add package Adamantium.Vulkan
dotnet add package Adamantium.Vulkan.Loader
```

## Requirements

- **.NET 10** or later. The shader packages also target `netstandard2.0`.
- **x64.** The projects build for that platform only; other architectures have not been tried.
- **A Vulkan driver** on the machine at run time. No SDK is needed to build: the loader resolves the driver's own
  library, and the Slang package carries the shader compiler with it - and `slangd`, which an editor starts from the
  application's folder for completion, hover and definitions in shaders.

## Why the loader is a separate package

`Adamantium.Vulkan.Loader` resolves Vulkan at run time instead of linking an import library at build time. An
application therefore binds to whichever Vulkan the machine actually has, and a machine without one fails with an
error you can catch rather than refusing to start.

## Third-party binaries

The Slang, SPIRV-Cross and shader-object layer packages ship compiled binaries of projects that are not ours - among them the Slang
compiler (Apache-2.0 with the LLVM exception, itself carrying glslang, SPIRV-Tools, LLVM, mimalloc and lz4),
SPIRV-Cross, SPIRV-Tools and Vulkan-ExtensionLayer's shader-object layer (Apache-2.0, Khronos). Every one of those licences is permissive, and the notices travel
with the binaries in
[THIRD-PARTY-NOTICES.md](https://github.com/AdamantiumStudio/AdamantiumVulkan/blob/master/THIRD-PARTY-NOTICES.md).

Nothing here derives from the Vulkan logo or any other Khronos trademark.

## Building from source

```
git clone https://github.com/AdamantiumStudio/AdamantiumVulkan
cd AdamantiumVulkan
dotnet build AdamantiumVulkan.sln -c Release
```

**Regenerating the bindings** is only needed when the Vulkan registry moves. `Adamantium.Vulkan.Generator` takes
QuantumBinding from the `QuantumBinding.Generator` package and the SPIR-V headers from the Vulkan SDK that
`VULKAN_SDK` points at. It reads `vk.xml` and `video.xml` from the current directory, so run it from its own:

```
cd Adamantium.Vulkan.Generator
dotnet run -c Release
```

## Status

In use, and under active development alongside the engine that depends on it. The API follows the Vulkan registry, so
it changes when the registry does.

## Licence

[Apache-2.0](https://github.com/AdamantiumStudio/AdamantiumVulkan/blob/master/LICENSE).

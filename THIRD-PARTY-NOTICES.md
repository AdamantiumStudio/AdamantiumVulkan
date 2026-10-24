# Third-party notices

This repository **redistributes compiled binaries of other people's projects** alongside its own code. That is
permitted by their licences — all of them permissive — on one condition: the notices travel with the binaries. This
file is that notice.

Listed here is what ships in the repository as a build output of someone else. Packages consumed through NuGet are
not listed: those carry their own licences with them and are not redistributed here.

**On versions.** These binaries were taken from upstream release builds, and the repository does not record which
build each came from. Where a filename carries a version it is named below; otherwise treat the entry as covering
whatever build is committed. This is worth fixing — see the note at the end.

---

## Slang — shader compiler

**Where:** `Adamantium.Vulkan.Slang/runtimes/` — `win-x64/native/`: `slang.dll`, `slang-compiler.dll`,
`slang-glslang.dll`, `slangd.exe`; `linux-x64/native/`: `libslang-compiler.so.0.2026.17`, `libslang-glslang-2026.17.so`,
`slangd`. Slang 2026.17, from Vulkan SDK 1.4.363.0 (its build `2026.17-25-gc7954ebd1`). `slang-c-shared.dll` and
`libslang-c-shared.so` beside them are this repository's own shim (`native/`), not Slang's.

The compiler that turns this engine's shaders into SPIR-V. Shipped so that building shaders needs no Vulkan SDK
installed.

Slang is licensed under the **Apache License 2.0 with LLVM exception**. The Apache-2.0 text is in `LICENSE` at the
root of this repository; the LLVM exception adds to it:

```
---- LLVM Exceptions to the Apache 2.0 License ----

As an exception, if, as a result of your compiling your source code, portions
of this Software are embedded into an Object form of such source code, you
may redistribute such embedded portions in such Object form without complying
with the conditions of Sections 4(a), 4(b) and 4(d) of the License.

In addition, if you combine or link compiled forms of this Software with
software that is licensed under the GPLv2 ("Combined Software") and if a
court of competent jurisdiction determines that the patent provision (Section
3), the indemnity provision (Section 9) or other Section of the License
conflicts with the conditions of the GPLv2, you may retroactively and
prospectively choose to deem waived or otherwise exclude such Section(s) of
the License, but only in their entirety and only with respect to the Combined
Software.
```

**Slang bundles further projects inside these binaries**, each under its own licence — among them glslang (BSD),
SPIRV-Tools (Apache-2.0), SPIRV-Headers (modified MIT), LLVM (Apache-2.0 with LLVM exception), mimalloc (MIT) and
lz4 (BSD). `slang-glslang.dll` is glslang as Slang ships it. The authoritative and complete list is the `LICENSE`
file of the Slang release these binaries came from.

Upstream: https://github.com/shader-slang/slang

---

## SPIRV-Cross — SPIR-V reflection and cross-compilation

**Where:** `Adamantium.Vulkan.Spirv/runtimes/` — `win-x64/native/spirv-cross-c-shared.dll` and
`linux-x64/native/libspirv-cross-c-shared.so`, both from Vulkan SDK 1.4.363.0 (the Linux build names version 0.68.0);
`osx-x64/native/libspirv-cross-c-shared.dylib`, of a release not recorded

Used to read back what a compiled shader declares, so the engine can bind resources by what the shader actually asks
for rather than by a hand-kept table.

A Khronos Group project, licensed under the **Apache License 2.0** — text in `LICENSE` at the root of this
repository.

Upstream: https://github.com/KhronosGroup/SPIRV-Cross

---

## SPIRV-Tools

**Where:** `Adamantium.Vulkan.SpirvTools/SPIRV-Tools-shared.dll`

Assembly, disassembly, validation and optimisation of SPIR-V modules.

A Khronos Group project, licensed under the **Apache License 2.0** — text in `LICENSE` at the root of this
repository.

Upstream: https://github.com/KhronosGroup/SPIRV-Tools

---

## Vulkan-ExtensionLayer — VK_LAYER_KHRONOS_shader_object

**Where:** the `Adamantium.Vulkan.ShaderObjectLayer` package, `layers/<rid>/` — `win-x64/VkLayer_khronos_shader_object.dll`,
`linux-x64/libVkLayer_khronos_shader_object.so`, `osx-arm64/libVkLayer_khronos_shader_object.dylib`, each with its
manifest `VkLayer_khronos_shader_object.json`. Not committed: CI builds them from the tag `vulkan-sdk-1.4.363.0`
(`native/build.sh`, `native/build.ps1`) and the publish workflow packs what it built.

Gives `VK_EXT_shader_object` to a driver that does not have it - Intel Arc on Windows, KosmicKrisp on macOS - by making
the pipelines itself; on a driver that has it, the layer stands aside.

A Khronos Group project, licensed under the **Apache License 2.0** — text in `LICENSE` at the root of this
repository.

Upstream: https://github.com/KhronosGroup/Vulkan-ExtensionLayer

---

## Not third-party

For the avoidance of doubt, these committed binaries are this project's own and are covered by the repository's
`LICENSE`: `Adamantium.Core.dll`, `Adamantium.Imaging.dll`, `Adamantium.Mathematics.dll`.

---

## Worth fixing

Committing other people's binaries is what makes this file necessary. The Slang, SPIRV-Cross and SPIRV-Tools
binaries are genuinely redistributed — they ship so that building needs no Vulkan SDK, which is a deliberate and good
property. They should stay, but the upstream release version of each ought to be recorded next to them, so this file
can name it.

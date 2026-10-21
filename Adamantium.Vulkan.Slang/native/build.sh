#!/usr/bin/env bash
#
# Builds the slang-c-shared shim on Linux/macOS and stages it, the Slang runtime and slangd under
# runtimes/<rid>/native next to the C# project, which packs that folder as it is.
# Slang is taken from $SLANG_DIR, else $VULKAN_SDK (the Vulkan SDK bundles Slang).
#   Usage:  ./native/build.sh [Release|Debug]
#
set -euo pipefail

NATIVE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(dirname "$NATIVE")"
CONFIG="${1:-Release}"

SLANG="${SLANG_DIR:-${VULKAN_SDK:-}}"
if [ -z "$SLANG" ]; then
    echo "Set SLANG_DIR or VULKAN_SDK to a Slang / Vulkan SDK directory." >&2
    exit 1
fi

case "$(uname -s)-$(uname -m)" in
    Linux-x86_64)  RID=linux-x64;  EXT=so ;;
    Darwin-x86_64) RID=osx-x64;    EXT=dylib ;;
    Darwin-arm64)  RID=osx-arm64;  EXT=dylib ;;
    *) echo "No runtime identifier for $(uname -s) $(uname -m)." >&2; exit 1 ;;
esac
STAGE="$PROJECT/runtimes/$RID/native"
mkdir -p "$STAGE"

cmake -S "$NATIVE" -B "$NATIVE/build" -DCMAKE_BUILD_TYPE="$CONFIG" -DSLANG_DIR="$SLANG"
cmake --build "$NATIVE/build" --config "$CONFIG"

# The shim (single-config generators emit to build/, multi-config to build/<Config>).
shim="$(find "$NATIVE/build" -maxdepth 2 -name "libslang-c-shared.$EXT" | head -n1)"
[ -n "$shim" ] || { echo "The shim was not built." >&2; exit 1; }
cp "$shim" "$STAGE/"

# The Slang runtime, under the names it is loaded by: the compiler by its soname (the shim and slangd link that), and
# glslang - the SPIRV-Tools optimizer - which the compiler opens by its versioned name. Real files, not the symlinks.
copied=()
for pattern in "libslang-compiler.*$EXT*" "libslang-glslang*.$EXT"; do
    while IFS= read -r file; do
        cp -L "$file" "$STAGE/"
        copied+=("$(basename "$file")")
    done < <(find "$SLANG" -maxdepth 2 -name "$pattern" -type f)
done
[ "${#copied[@]}" -gt 0 ] || { echo "No Slang runtime found under $SLANG." >&2; exit 1; }

# Slang's language server from the same SDK.
server="$(find "$SLANG" -maxdepth 2 -name 'slangd' -type f | head -n1)"
[ -n "$server" ] && cp "$server" "$STAGE/"

echo "OK: $(basename "$shim") ${copied[*]} ${server:+slangd} -> $STAGE"

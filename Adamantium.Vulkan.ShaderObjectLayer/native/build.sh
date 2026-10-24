#!/usr/bin/env bash
#
# Builds VK_LAYER_KHRONOS_shader_object from Vulkan-ExtensionLayer on Linux/macOS and stages the library and its
# manifest under layers/<rid>/ next to the C# project, which packs that folder as it is.
#   Usage:  ./native/build.sh [tag]
# The tag defaults to the Vulkan SDK release the rest of this repository is generated from.
#
set -euo pipefail

NATIVE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(dirname "$NATIVE")"
TAG="${1:-vulkan-sdk-1.4.363.0}"

case "$(uname -s)-$(uname -m)" in
    Linux-x86_64)  RID=linux-x64;  LIB=libVkLayer_khronos_shader_object.so ;;
    Darwin-arm64)  RID=osx-arm64;  LIB=libVkLayer_khronos_shader_object.dylib ;;
    *) echo "No runtime identifier for $(uname -s) $(uname -m)." >&2; exit 1 ;;
esac

WORK="$NATIVE/build"
SOURCE="$WORK/Vulkan-ExtensionLayer"
if [ ! -d "$SOURCE" ]; then
    git clone --depth 1 --branch "$TAG" https://github.com/KhronosGroup/Vulkan-ExtensionLayer.git "$SOURCE"
fi

# No window system: the layer only passes WSI calls through, and the X11/Wayland headers would be one more thing the
# build machine needs.
cmake -S "$SOURCE" -B "$WORK/out" -D CMAKE_BUILD_TYPE=Release -D UPDATE_DEPS=ON \
    -D BUILD_WSI_XCB_SUPPORT=OFF -D BUILD_WSI_XLIB_SUPPORT=OFF -D BUILD_WSI_WAYLAND_SUPPORT=OFF
cmake --build "$WORK/out" --config Release --target VkLayer_khronos_shader_object

library="$(find "$WORK/out/layers" -name "$LIB" -type f | head -n1)"
manifest="$(find "$WORK/out/layers" -maxdepth 2 -name 'VkLayer_khronos_shader_object.json' -type f | head -n1)"
[ -n "$library" ] && [ -n "$manifest" ] || { echo "The layer was not built." >&2; exit 1; }

STAGE="$PROJECT/layers/$RID"
mkdir -p "$STAGE"
cp "$library" "$manifest" "$STAGE/"
echo "OK: $LIB + manifest ($TAG) -> $STAGE"

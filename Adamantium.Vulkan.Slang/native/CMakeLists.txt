cmake_minimum_required(VERSION 3.20)
project(slang_c_shared CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Slang location: pass -DSLANG_DIR=<dir>, otherwise fall back to the Vulkan SDK, which bundles Slang.
if(NOT SLANG_DIR AND DEFINED ENV{SLANG_DIR})
    set(SLANG_DIR "$ENV{SLANG_DIR}")
endif()
if(NOT SLANG_DIR AND DEFINED ENV{VULKAN_SDK})
    set(SLANG_DIR "$ENV{VULKAN_SDK}")
endif()
if(NOT SLANG_DIR)
    message(FATAL_ERROR "SLANG_DIR not set and neither $SLANG_DIR nor $VULKAN_SDK is in the environment.")
endif()

# Resolve headers + library across platforms (Windows: Include/Lib + slang.lib; *nix: include/lib + libslang.*).
find_path(SLANG_INCLUDE_DIR slang.h
    PATHS "${SLANG_DIR}/include/slang" "${SLANG_DIR}/Include/slang" "${SLANG_DIR}/include" "${SLANG_DIR}/Include"
    NO_DEFAULT_PATH)
find_library(SLANG_LIBRARY NAMES slang
    PATHS "${SLANG_DIR}/lib" "${SLANG_DIR}/Lib"
    NO_DEFAULT_PATH)
if(NOT SLANG_INCLUDE_DIR OR NOT SLANG_LIBRARY)
    message(FATAL_ERROR "Could not find slang.h / slang library under '${SLANG_DIR}'.")
endif()
message(STATUS "Slang headers: ${SLANG_INCLUDE_DIR}")
message(STATUS "Slang library: ${SLANG_LIBRARY}")

# slang-c-shared.dll (win) / libslang-c-shared.so|dylib (unix) — matching the spirv-cross-c-shared naming.
add_library(slang-c-shared SHARED slang_c.cpp slang_c.h)
set_target_properties(slang-c-shared PROPERTIES OUTPUT_NAME "slang-c-shared")
target_include_directories(slang-c-shared PRIVATE "${SLANG_INCLUDE_DIR}")
target_link_libraries(slang-c-shared PRIVATE "${SLANG_LIBRARY}")

# The shim links the Slang compiler by its soname (libslang-compiler.so.0.<version>) and ships beside it in
# runtimes/<rid>/native, so it looks there first - not in the SDK directory it was linked against.
if(APPLE)
    set_target_properties(slang-c-shared PROPERTIES BUILD_WITH_INSTALL_RPATH ON INSTALL_RPATH "@loader_path")
elseif(UNIX)
    set_target_properties(slang-c-shared PROPERTIES BUILD_WITH_INSTALL_RPATH ON INSTALL_RPATH "$ORIGIN")
endif()

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Adamantium.Vulkan.Loader
{
    public static class VulkanDllMap
    {
        private static readonly Dictionary<Assembly, LibraryNameResolver> registeredAssemblies;

        static VulkanDllMap()
        {
            registeredAssemblies = new Dictionary<Assembly, LibraryNameResolver>();
        }

        // Register a call
        public static void Register()
        {
            // libvulkan.so.1, not libvulkan.so: the unversioned name comes only with the -devel package, the loader itself
            // installs the versioned one.
            var coreResolver = new LibraryNameResolver("Adamantium.Vulkan") { WindowsLibraryName = "vulkan-1", OSXLibraryName = "libvulkan", LinuxLibraryName = "libvulkan.so.1" };
            var spirvCrossResolver = new LibraryNameResolver("Adamantium.Vulkan.Spirv") { WindowsLibraryName = "spirv-cross-c-shared", OSXLibraryName = "libspirv-cross-c-shared", LinuxLibraryName = "libspirv-cross-c-shared" };
            RegisterAssembly(coreResolver);
            RegisterAssembly(spirvCrossResolver);
        }

        private static void RegisterAssembly(LibraryNameResolver libNameResolver)
        {
            if (RegisterAssembly(libNameResolver.AssemblyName, out var assembly))
            {
                registeredAssemblies[assembly] = libNameResolver;
            }
        }

        private static bool RegisterAssembly(string assemblyName, out Assembly assembly)
        {
            try
            {
                assembly = Assembly.Load(assemblyName);
                NativeLibrary.SetDllImportResolver(assembly, MapAndLoad);
                return true;
            }
            catch(Exception)
            {
                Console.WriteLine($"Could not load assembly {assemblyName}");
                assembly = null;
                return false;
            }
        }

        // The callback: which loads the mapped library in place of the original
        private static IntPtr MapAndLoad(string libraryName, Assembly assembly, DllImportSearchPath? dllImportSearchPath)
        {
            string mappedName = libraryName;
            if (registeredAssemblies.TryGetValue(assembly, out var resolver))
            {
                mappedName = resolver.LibraryNameForCurrentPlatform;
            }

            return NativeLibrary.Load(mappedName, assembly, dllImportSearchPath);
        }
    }
}

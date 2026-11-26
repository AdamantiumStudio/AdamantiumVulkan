using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Adamantium.Vulkan.Core;
using Adamantium.Vulkan.Core.Interop;
using Adamantium.Vulkan.Loader;
using NUnit.Framework;

namespace Adamantium.Vulkan.Tests;

/// <summary>This machine's Vulkan as the tests use it: an instance - on a Mac with KosmicKrisp as its only driver - the
/// first device of Vulkan 1.3 with a graphics queue, that queue and a command pool. Where the validation layer is
/// installed, it checks every call from creating the instance to destroying it, and its errors are kept here.</summary>
internal sealed unsafe class ThisMachinesVulkan
{
    private const string ValidationLayer = "VK_LAYER_KHRONOS_validation";
    private const string NeedADevice = "ADAMANTIUM_VULKAN_TESTS_NEED_A_DEVICE";
    private const string NeedValidation = "ADAMANTIUM_VULKAN_TESTS_NEED_VALIDATION";
    private const string KosmicKrispManifest = "libkosmickrisp_icd.json";

    private readonly List<string> _validationErrors = [];
    private GCHandle _errorsHandle;
    private DebugUtilsMessengerEXT _messenger;
    private IntPtr _driver;

    static ThisMachinesVulkan()
    {
        VulkanDllMap.Register();
    }

    private ThisMachinesVulkan()
    {
    }

    public Instance Instance { get; private set; }

    public PhysicalDevice PhysicalDevice { get; private set; }

    public Device Device { get; private set; }

    public Queue Queue { get; private set; }

    public uint QueueFamily { get; private set; }

    public CommandPool CommandPool { get; private set; }

    /// <summary>Whether the validation layer checks the calls.</summary>
    public bool IsValidated { get; private set; }

    /// <summary>This machine's Vulkan. Where it has none the test is ignored - or fails, where the environment says it
    /// has one; anything else going wrong is the bindings' fault and fails it.</summary>
    public static ThisMachinesVulkan Open()
    {
        var vulkan = new ThisMachinesVulkan();
        try
        {
            vulkan.CreateInstance();
            vulkan.CreateDevice();
            return vulkan;
        }
        catch (NoVulkanHereException e)
        {
            vulkan.Close();
            if (Environment.GetEnvironmentVariable(NeedADevice) == "1")
                Assert.Fail(e.Message);

            Assert.Ignore(e.Message);
            throw;
        }
        catch
        {
            vulkan.Close();
            throw;
        }
    }

    /// <summary>The validation errors since last asked, taken.</summary>
    public string[] TakeValidationErrors()
    {
        lock (_validationErrors)
        {
            var errors = _validationErrors.ToArray();
            _validationErrors.Clear();
            return errors;
        }
    }

    /// <summary>Records commands, submits them and waits for them to be done.</summary>
    public void Submit(Action<CommandBuffer> record)
    {
        var commands = Device.BeginSingleTimeCommand(CommandPool);
        record(commands);
        Device.EndSingleTimeCommands(Queue, CommandPool, commands);
    }

    /// <summary>A buffer with its own memory of the kind asked, bound.</summary>
    public BoundBuffer CreateBuffer(ulong size, BufferUsageFlagBits usage, MemoryPropertyFlagBits memoryKind)
    {
        var buffer = Device.CreateBuffer(new BufferCreateInfo { Size = size, Usage = usage, SharingMode = SharingMode.Exclusive });
        try
        {
            var requirements = Device.GetBufferMemoryRequirements(buffer);
            var memory = Device.AllocateMemory(new MemoryAllocateInfo
            {
                AllocationSize = requirements.Size,
                MemoryTypeIndex = PhysicalDevice.FindCorrespondingMemoryType(requirements.MemoryTypeBits, memoryKind)
            });
            Device.BindBufferMemory(buffer, memory, 0);
            return new BoundBuffer(Device, buffer, memory, size);
        }
        catch
        {
            Device.DestroyBuffer(buffer);
            throw;
        }
    }

    /// <summary>Destroys all it made and gives the validation errors told meanwhile - objects left alive among them.
    /// </summary>
    public string[] Close()
    {
        if (Device != null)
        {
            Device.DeviceWaitIdle();
            if (CommandPool != null)
                Device.DestroyCommandPool(CommandPool);
            Device.DestroyDevice();
        }

        if (Instance != null)
        {
            if (_messenger != null)
                Instance.DestroyDebugUtilsMessengerEXT(_messenger);

            // The messenger chained to the instance's creation hears its destruction too.
            Instance.DestroyInstance();
        }

        var errors = TakeValidationErrors();
        if (_errorsHandle.IsAllocated)
            _errorsHandle.Free();

        if (_driver != IntPtr.Zero)
            NativeLibrary.Free(_driver);

        Device = null;
        Instance = null;
        _driver = IntPtr.Zero;
        return errors;
    }

    private void CreateInstance()
    {
        HashSet<string> layers;
        try
        {
            layers = Instance.EnumerateInstanceLayerProperties().Select(layer => layer.LayerName).ToHashSet();
        }
        catch (DllNotFoundException e)
        {
            throw new NoVulkanHereException("no Vulkan loader on this machine", e);
        }

        IsValidated = layers.Contains(ValidationLayer);
        if (!IsValidated && Environment.GetEnvironmentVariable(NeedValidation) == "1")
            Assert.Fail($"{ValidationLayer} is not installed, and {NeedValidation}=1 says it is");

        var createInfo = new InstanceCreateInfo
        {
            PApplicationInfo = new ApplicationInfo
            {
                PApplicationName = "Adamantium.Vulkan.Tests",
                ApplicationVersion = Constants.VK_MAKE_API_VERSION(0, 1, 0, 0),
                PEngineName = "Adamantium.Vulkan.Tests",
                EngineVersion = Constants.VK_MAKE_API_VERSION(0, 1, 0, 0),
                ApiVersion = Constants.VK_API_VERSION_1_3
            }
        };

        var extensions = new HashSet<string>();
        object chain = null;
        if (OperatingSystem.IsMacOS())
        {
            chain = LoadKosmicKrispAlone();
            extensions.Add(Constants.VK_LUNARG_DIRECT_DRIVER_LOADING_EXTENSION_NAME);
        }

        DebugUtilsMessengerCreateInfoEXT listening = null;
        if (IsValidated)
        {
            createInfo.EnabledLayerCount = 1;
            createInfo.PEnabledLayerNames = new[] { ValidationLayer };
            extensions.Add(Constants.VK_EXT_DEBUG_UTILS_EXTENSION_NAME);
            _errorsHandle = GCHandle.Alloc(_validationErrors);
            listening = ListenToValidation();
            listening.PNext = chain;
            chain = listening;
        }

        var enabled = extensions.ToArray();
        createInfo.EnabledExtensionCount = (uint)enabled.Length;
        createInfo.PEnabledExtensionNames = enabled;
        createInfo.PNext = chain;

        var result = VulkanNative.CreateInstance(createInfo, null, out var instance);
        if (result is Result.ErrorIncompatibleDriver or Result.ErrorInitializationFailed)
            throw new NoVulkanHereException($"vkCreateInstance: {result} - no driver for Vulkan 1.3 here");
        ResultHelper.CheckResult(result, "vkCreateInstance");
        Instance = instance;

        if (listening != null)
        {
            listening.PNext = null;
            Assert.That(Instance.CreateDebugUtilsMessengerEXT(listening, null, out _messenger), Is.EqualTo(Result.Success),
                "vkCreateDebugUtilsMessengerEXT");
        }
    }

    // KosmicKrisp from its manifest, handed to the loader as its only driver: MoltenVK, installed beside it, takes no part.
    private DirectDriverLoadingListLUNARG LoadKosmicKrispAlone()
    {
        var manifest = KosmicKrispManifests().FirstOrDefault(File.Exists)
                       ?? throw new NoVulkanHereException($"KosmicKrisp is not installed: no {KosmicKrispManifest}");

        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var library = document.RootElement.GetProperty("ICD").GetProperty("library_path").GetString()!;
        if (library.Contains('/') && !Path.IsPathRooted(library))
            library = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifest)!, library));

        _driver = NativeLibrary.Load(library);
        var getInstanceProcAddr = (delegate* unmanaged<VkInstance_T, sbyte*, nuint>)NativeLibrary.GetExport(_driver,
            "vk_icdGetInstanceProcAddr");

        return new DirectDriverLoadingListLUNARG
        {
            Mode = DirectDriverLoadingModeLUNARG.ExclusiveLunarg,
            DriverCount = 1,
            PDrivers = new[] { new DirectDriverLoadingInfoLUNARG { PfnGetInstanceProcAddr = getInstanceProcAddr } }
        };
    }

    // Where the loader's own variables point first, then the Vulkan SDK and the folders it and Homebrew install to.
    private static IEnumerable<string> KosmicKrispManifests()
    {
        foreach (var variable in new[] { "VK_DRIVER_FILES", "VK_ICD_FILENAMES" })
        {
            var files = Environment.GetEnvironmentVariable(variable) ?? string.Empty;
            foreach (var file in files.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Path.GetFileName(file) == KosmicKrispManifest)
                    yield return file;
            }
        }

        var sdk = Environment.GetEnvironmentVariable("VULKAN_SDK");
        if (!string.IsNullOrEmpty(sdk))
            yield return Path.Combine(sdk, "share", "vulkan", "icd.d", KosmicKrispManifest);

        yield return Path.Combine("/usr/local/share/vulkan/icd.d", KosmicKrispManifest);
        yield return Path.Combine("/opt/homebrew/share/vulkan/icd.d", KosmicKrispManifest);
    }

    private DebugUtilsMessengerCreateInfoEXT ListenToValidation() => new()
    {
        MessageSeverity = DebugUtilsMessageSeverityFlagBitsEXT.ErrorBitExt,
        MessageType = DebugUtilsMessageTypeFlagBitsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagBitsEXT.GeneralBitExt,
        PfnUserCallback = &Report,
        PUserData = (nuint)(nint)GCHandle.ToIntPtr(_errorsHandle)
    };

    [UnmanagedCallersOnly]
    private static VkBool32 Report(DebugUtilsMessageSeverityFlagBitsEXT severity, DebugUtilsMessageTypeFlagBitsEXT types,
        VkDebugUtilsMessengerCallbackDataEXT* data, void* errors)
    {
        if (data != null && GCHandle.FromIntPtr((IntPtr)errors).Target is List<string> list)
        {
            lock (list)
                list.Add(new string(data->pMessage));
        }

        return VkBool32.FALSE;
    }

    private void CreateDevice()
    {
        var devices = Instance.EnumeratePhysicalDevices();
        var refused = new List<string>();
        foreach (var device in devices)
        {
            var properties = device.GetPhysicalDeviceProperties();
            if (properties.ApiVersion < Constants.VK_API_VERSION_1_3)
            {
                refused.Add($"{properties.DeviceName}: Vulkan below 1.3");
                continue;
            }

            var vulkan13 = new PhysicalDeviceVulkan13Features();
            var features = new PhysicalDeviceFeatures2 { Features = new PhysicalDeviceFeatures(), PNext = vulkan13 };
            device.GetPhysicalDeviceFeatures2(ref features);
            var graphics = Array.FindIndex(device.GetQueueFamilyProperties(),
                family => (family.QueueFlags & QueueFlagBits.GraphicsBit) != 0);
            if (!vulkan13.DynamicRendering || graphics < 0)
            {
                refused.Add($"{properties.DeviceName}: no dynamic rendering or no graphics queue");
                continue;
            }

            PhysicalDevice = device;
            QueueFamily = (uint)graphics;
            break;
        }

        if (PhysicalDevice == null)
        {
            throw new NoVulkanHereException(devices.Length == 0
                ? "no Vulkan device on this machine"
                : $"no device for the tests: {string.Join("; ", refused)}");
        }

        Device = PhysicalDevice.CreateDevice(new DeviceCreateInfo
        {
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = new[]
            {
                new DeviceQueueCreateInfo { QueueFamilyIndex = QueueFamily, QueueCount = 1, PQueuePriorities = new[] { 1.0f } }
            },
            PNext = new PhysicalDeviceVulkan13Features { DynamicRendering = VkBool32.TRUE }
        });
        Queue = Device.GetDeviceQueue(QueueFamily, 0);
        CommandPool = Device.CreateCommandPool(new CommandPoolCreateInfo
        {
            QueueFamilyIndex = QueueFamily,
            Flags = CommandPoolCreateFlagBits.ResetCommandBufferBit
        });
    }

    /// <summary>This machine has no Vulkan the tests can use: no fault of the bindings.</summary>
    private sealed class NoVulkanHereException(string message, Exception inner = null) : Exception(message, inner);
}

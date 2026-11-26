using System.Linq;
using System.Text.RegularExpressions;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.Vulkan.Tests;

/// <summary>What the device tells of itself comes through whole: strings, fixed arrays, counted arrays and chains.
/// </summary>
[TestFixture]
public partial class DeviceTests
{
    private ThisMachinesVulkan _vulkan;

    // Opened and closed for each test: a failed teardown fails its test, a failed one-time teardown fails nothing.
    [SetUp]
    public void Open() => _vulkan = ThisMachinesVulkan.Open();

    [TearDown]
    public void Close()
    {
        var errors = _vulkan?.Close() ?? [];
        _vulkan = null;
        Assert.That(errors, Is.Empty, "the validation layer's errors, objects left alive on closing among them");
    }

    [Test]
    public void TheDevice_TellsItsNameVersionAndCacheId()
    {
        var properties = _vulkan.PhysicalDevice.GetPhysicalDeviceProperties();
        TestContext.Out.WriteLine($"{properties.DeviceName}, Vulkan {Constants.VK_API_VERSION_MAJOR(properties.ApiVersion)}." +
                                  $"{Constants.VK_API_VERSION_MINOR(properties.ApiVersion)}, validated: {_vulkan.IsValidated}");

        Assert.Multiple(() =>
        {
            Assert.That(properties.DeviceName, Is.Not.Empty.And.Not.Contains('\0'));
            Assert.That(Constants.VK_API_VERSION_MAJOR(properties.ApiVersion), Is.EqualTo(1));
            Assert.That(Constants.VK_API_VERSION_MINOR(properties.ApiVersion), Is.GreaterThanOrEqualTo(3));
            Assert.That(properties.PipelineCacheUUID.Length, Is.EqualTo(Constants.VK_UUID_SIZE), "a fixed array, whole");
            Assert.That(properties.Limits.MaxImageDimension2D, Is.GreaterThanOrEqualTo(4096u), "the limits, a struct inside");
            Assert.That(properties.Limits.MaxViewportDimensions.Length, Is.EqualTo(2), "a fixed array inside a struct inside");
        });
    }

    [Test]
    public void ExtensionNames_AreWholeNames()
    {
        uint count = 0;
        _vulkan.PhysicalDevice.EnumerateDeviceExtensionProperties(null, ref count, null);
        var extensions = new ExtensionProperties[count];
        Assert.That(_vulkan.PhysicalDevice.EnumerateDeviceExtensionProperties(null, ref count, extensions), Is.EqualTo(Result.Success));
        var names = extensions.Take((int)count).Select(extension => extension.ExtensionName).ToArray();

        Assert.That(names, Is.Not.Empty);
        Assert.That(names, Is.Unique);
        Assert.That(names, Has.All.Matches<string>(name => ExtensionName().IsMatch(name)));
    }

    [Test]
    public void MemoryTypes_AreAsManyAsTold_EachInAHeapThatIs()
    {
        var memory = _vulkan.PhysicalDevice.GetPhysicalDeviceMemoryProperties();
        var types = memory.MemoryTypes.Span[..(int)memory.MemoryTypeCount].ToArray();

        Assert.That(memory.MemoryTypeCount, Is.InRange(1u, (uint)Constants.VK_MAX_MEMORY_TYPES));
        Assert.That(memory.MemoryHeapCount, Is.InRange(1u, (uint)Constants.VK_MAX_MEMORY_HEAPS));
        Assert.That(types.Select(type => type.HeapIndex), Has.All.LessThan(memory.MemoryHeapCount));
        Assert.That(types, Has.Some.Matches<MemoryType>(type =>
            type.PropertyFlags.HasFlag(MemoryPropertyFlagBits.HostVisibleBit | MemoryPropertyFlagBits.HostCoherentBit)));
    }

    [Test]
    public void AChainOfFeatures_IsFilledInLinkByLink()
    {
        var vulkan13 = new PhysicalDeviceVulkan13Features();
        var vulkan12 = new PhysicalDeviceVulkan12Features { PNext = vulkan13 };
        var features = new PhysicalDeviceFeatures2 { Features = new PhysicalDeviceFeatures(), PNext = vulkan12 };

        _vulkan.PhysicalDevice.GetPhysicalDeviceFeatures2(ref features);

        Assert.Multiple(() =>
        {
            // What Vulkan 1.3 and 1.2 require of every device: told by the second and third link.
            Assert.That((bool)vulkan13.DynamicRendering, Is.True, "1.3 requires dynamic rendering");
            Assert.That((bool)vulkan13.Synchronization2, Is.True, "1.3 requires synchronization2");
            Assert.That((bool)vulkan12.TimelineSemaphore, Is.True, "1.2 requires timeline semaphores");
            Assert.That((bool)features.Features.RobustBufferAccess, Is.True, "1.0 requires robust buffer access");
        });
    }

    [GeneratedRegex("^VK_[A-Za-z0-9_]+$")]
    private static partial Regex ExtensionName();
}

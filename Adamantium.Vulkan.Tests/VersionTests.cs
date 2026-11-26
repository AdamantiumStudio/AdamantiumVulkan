using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.Vulkan.Tests;

/// <summary>vulkan_core.h's version macros, as generated: no device needed.</summary>
[TestFixture]
public class VersionTests
{
    [Test]
    public void AVersion_IsTakenApartAsItWasPutTogether()
    {
        var version = Constants.VK_MAKE_API_VERSION(2, 1, 3, 275);

        Assert.Multiple(() =>
        {
            Assert.That(Constants.VK_API_VERSION_VARIANT(version), Is.EqualTo(2u));
            Assert.That(Constants.VK_API_VERSION_MAJOR(version), Is.EqualTo(1u));
            Assert.That(Constants.VK_API_VERSION_MINOR(version), Is.EqualTo(3u));
            Assert.That(Constants.VK_API_VERSION_PATCH(version), Is.EqualTo(275u));
        });
    }

    [Test]
    public void TheLargestPartsOfAVersion_DoNotSpillIntoEachOther()
    {
        var version = Constants.VK_MAKE_API_VERSION(7, 127, 1023, 4095);

        Assert.Multiple(() =>
        {
            Assert.That(Constants.VK_API_VERSION_VARIANT(version), Is.EqualTo(7u));
            Assert.That(Constants.VK_API_VERSION_MAJOR(version), Is.EqualTo(127u));
            Assert.That(Constants.VK_API_VERSION_MINOR(version), Is.EqualTo(1023u));
            Assert.That(Constants.VK_API_VERSION_PATCH(version), Is.EqualTo(4095u));
        });
    }
}

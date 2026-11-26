using System.Linq;
using Adamantium.Vulkan.Core;
using Adamantium.Vulkan.Slang;
using Adamantium.Vulkan.Spirv.Reflection;
using NUnit.Framework;

namespace Adamantium.Vulkan.Tests;

/// <summary>Slang compiles to SPIR-V the device takes, and SPIRV-Cross reads back what the SPIR-V binds.</summary>
[TestFixture]
public class ShaderTests
{
    [Test]
    public void SlangCompiles_SpirvTheDeviceTakes()
    {
        var vertex = Shaders.Compile(Shaders.Triangle, "VertexMain", SlangcStage.Vertex);
        var fragment = Shaders.Compile(Shaders.Triangle, "FragmentMain", SlangcStage.Fragment);

        Assert.Multiple(() =>
        {
            Assert.That(Shaders.FirstWord(vertex), Is.EqualTo(Shaders.SpirvMagic));
            Assert.That(Shaders.FirstWord(fragment), Is.EqualTo(Shaders.SpirvMagic));
            Assert.That(vertex.Length % 4, Is.Zero, "SPIR-V is words");
        });

        var vulkan = ThisMachinesVulkan.Open();
        string[] errors;
        try
        {
            foreach (var spirv in new[] { vertex, fragment })
            {
                var module = vulkan.Device.CreateShaderModule(new ShaderModuleCreateInfo { PCodeBytes = spirv });
                Assert.That(module, Is.Not.Null);
                vulkan.Device.DestroyShaderModule(module);
            }
        }
        finally
        {
            errors = vulkan.Close();
        }

        Assert.That(errors, Is.Empty, "the validation layer's errors");
    }

    [Test]
    public void SpirvCross_ReadsWhatTheShaderBinds_WhereAndHowLarge()
    {
        var spirv = Shaders.Compile(Shaders.Lit, "FragmentMain", SlangcStage.Fragment);
        using var reflection = new SpirvReflection(spirv);

        var result = reflection.Disassemble();
        foreach (var resource in result.AllResources)
            TestContext.Out.WriteLine(resource);

        var light = result.UniformBuffers.Single().Description;
        var albedo = result.Images.Single().Description;
        var sampler = result.Samplers.Single().Description;
        var members = Enumerable.Range(0, (int)result.UniformBuffers.Single().VariablesCount)
            .Select(index => result.UniformBuffers.Single().GetVariable((uint)index)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(light.DescriptorSet, Is.Zero);
            Assert.That(light.SlotIndex, Is.Zero);
            Assert.That(light.Size, Is.EqualTo(32UL), "float4, float3 and a float packed after it");
            Assert.That(members.Select(member => member.Offset), Is.EqualTo(new uint[] { 0, 16, 28 }));
            Assert.That(albedo.SlotIndex, Is.EqualTo(1u));
            Assert.That(sampler.SlotIndex, Is.EqualTo(2u));
        });
    }
}

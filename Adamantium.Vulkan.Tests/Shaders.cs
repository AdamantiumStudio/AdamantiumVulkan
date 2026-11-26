using System;
using Adamantium.Vulkan.Slang;
using NUnit.Framework;

namespace Adamantium.Vulkan.Tests;

/// <summary>Slang source compiled to SPIR-V for the tests, entry points keeping their names.</summary>
internal static class Shaders
{
    /// <summary>The first word of every SPIR-V module.</summary>
    public const uint SpirvMagic = 0x07230203;

    public const string Triangle = """
        struct Tint { float4 color; };

        [[vk::push_constant]]
        ConstantBuffer<Tint> tint;

        struct VertexOutput { float4 position : SV_Position; };

        [shader("vertex")]
        VertexOutput VertexMain(float2 position : POSITION)
        {
            VertexOutput output;
            output.position = float4(position, 0.0, 1.0);
            return output;
        }

        [shader("fragment")]
        float4 FragmentMain() : SV_Target
        {
            return tint.color;
        }
        """;

    public const string Lit = """
        struct Light { float4 color; float3 direction; float strength; };

        [[vk::binding(0, 0)]] ConstantBuffer<Light> light;
        [[vk::binding(1, 0)]] Texture2D albedo;
        [[vk::binding(2, 0)]] SamplerState linear;

        [shader("fragment")]
        float4 FragmentMain(float2 uv : TEXCOORD) : SV_Target
        {
            return albedo.Sample(linear, uv) * light.color * light.strength * light.direction.x;
        }
        """;

    public static byte[] Compile(string source, string entryPoint, SlangcStage stage)
    {
        using var compiler = new SlangCompiler("spirv_1_6",
        [
            new SlangcCompilerOption { Name = (int)SlangcCompilerOptionName.VulkanUseEntryPointName, ValueKind = 0, IntValue0 = 1 }
        ]);
        var result = compiler.Compile(source, entryPoint, stage);
        Assert.That(result.Success, Is.True, $"{entryPoint} compiles: {result.Diagnostics}");
        return result.Spirv;
    }

    public static uint FirstWord(byte[] spirv) => BitConverter.ToUInt32(spirv, 0);
}

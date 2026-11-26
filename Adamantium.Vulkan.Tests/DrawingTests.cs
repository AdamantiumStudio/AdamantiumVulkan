using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Vulkan.Core;
using Adamantium.Vulkan.Slang;
using NUnit.Framework;
using Image = Adamantium.Vulkan.Core.Image;
using ImageLayout = Adamantium.Vulkan.Core.ImageLayout;

namespace Adamantium.Vulkan.Tests;

/// <summary>A triangle drawn off screen and read back: buffers, a pipeline, push constants, dynamic rendering, barriers
/// and a copy all reach the device as meant, or the pixels say otherwise.</summary>
[TestFixture]
public class DrawingTests
{
    private const int Size = 64;
    private const Format ColorFormat = Format.R8G8B8A8_UNORM;

    private static readonly float[] Clear = [0, 0, 1, 1];
    private static readonly float[] Tint = [1, 0, 0, 1];

    // Wide at the top of the picture (Vulkan's y points down), a point at the bottom.
    private static readonly float[] Corners = [-0.5f, -0.5f, 0.5f, -0.5f, 0.0f, 0.5f];

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
    public void ATriangle_IsDrawnWhereItsCornersAre_InTheColourPushed()
    {
        var pixels = DrawTriangle();
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "triangle.png");
        Png.Write(path, pixels, Size, Size);
        TestContext.AddTestAttachment(path, "What the device drew");

        var red = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (ColorAt(pixels, i / 4 % Size, i / 4 / Size) == "255 0 0 255")
                red++;
        }

        Assert.Multiple(() =>
        {
            Assert.That(_vulkan.TakeValidationErrors(), Is.Empty, "the validation layer's errors");
            Assert.That(ColorAt(pixels, 32, 32), Is.EqualTo("255 0 0 255"), "the middle, inside");
            Assert.That(ColorAt(pixels, 0, 0), Is.EqualTo("0 0 255 255"), "a corner, cleared");
            Assert.That(ColorAt(pixels, Size - 1, Size - 1), Is.EqualTo("0 0 255 255"), "the other corner, cleared");
            Assert.That(ColorAt(pixels, 20, 18), Is.EqualTo("255 0 0 255"), "near the wide edge, inside");
            Assert.That(ColorAt(pixels, 20, 46), Is.EqualTo("0 0 255 255"), "as far out near the point, outside");
            // Half of a 32 by 32 square.
            Assert.That(red, Is.InRange(480, 544), "as many pixels as the triangle covers");
        });
    }

    private byte[] DrawTriangle()
    {
        var vertexCode = Shaders.Compile(Shaders.Triangle, "VertexMain", SlangcStage.Vertex);
        var fragmentCode = Shaders.Compile(Shaders.Triangle, "FragmentMain", SlangcStage.Fragment);

        using var vertices = _vulkan.CreateBuffer(sizeof(float) * (ulong)Corners.Length, BufferUsageFlagBits.VertexBufferBit,
            MemoryPropertyFlagBits.HostVisibleBit | MemoryPropertyFlagBits.HostCoherentBit);
        vertices.Write<float>(Corners);
        using var readBack = _vulkan.CreateBuffer(Size * Size * 4, BufferUsageFlagBits.TransferDstBit,
            MemoryPropertyFlagBits.HostVisibleBit | MemoryPropertyFlagBits.HostCoherentBit);
        using var target = new Target(_vulkan);
        target.Create(vertexCode, fragmentCode);

        _vulkan.Submit(commands =>
        {
            Barrier(commands, target.Image, 0, AccessFlagBits.ColorAttachmentWriteBit, ImageLayout.Undefined,
                ImageLayout.ColorAttachmentOptimal, PipelineStageFlagBits.TopOfPipeBit,
                PipelineStageFlagBits.ColorAttachmentOutputBit);

            commands.BeginRendering(new RenderingInfo
            {
                RenderArea = new Rect2D { Offset = new Offset2D(), Extent = new Extent2D { Width = Size, Height = Size } },
                LayerCount = 1,
                ColorAttachmentCount = 1,
                PColorAttachments = new[]
                {
                    new RenderingAttachmentInfo
                    {
                        ImageView = target.View,
                        ImageLayout = ImageLayout.ColorAttachmentOptimal,
                        ResolveMode = ResolveModeFlagBits.None,
                        LoadOp = AttachmentLoadOp.Clear,
                        StoreOp = AttachmentStoreOp.Store,
                        ClearValue = new ClearValue { Color = new ClearColorValue { Float32 = Clear } }
                    }
                }
            });
            commands.BindPipeline(PipelineBindPoint.Graphics, target.Pipeline);
            commands.BindVertexBuffers(0, 1, vertices.Buffer, 0);
            var tint = MemoryMarshal.AsBytes<float>(Tint);
            commands.PushConstants(target.Layout, ShaderStageFlagBits.FragmentBit, 0, (uint)tint.Length, tint);
            commands.Draw(3, 1, 0, 0);
            commands.EndRendering();

            Barrier(commands, target.Image, AccessFlagBits.ColorAttachmentWriteBit, AccessFlagBits.TransferReadBit,
                ImageLayout.ColorAttachmentOptimal, ImageLayout.TransferSrcOptimal,
                PipelineStageFlagBits.ColorAttachmentOutputBit, PipelineStageFlagBits.TransferBit);
            commands.CopyImageToBuffer(target.Image, ImageLayout.TransferSrcOptimal, readBack.Buffer, 1, new BufferImageCopy
            {
                ImageSubresource = new ImageSubresourceLayers { AspectMask = ImageAspectFlagBits.ColorBit, LayerCount = 1 },
                ImageOffset = new Offset3D(),
                ImageExtent = new Extent3D { Width = Size, Height = Size, Depth = 1 }
            });
            commands.PipelineBarrier(PipelineStageFlagBits.TransferBit, PipelineStageFlagBits.HostBit, 0, 1,
                [new MemoryBarrier { SrcAccessMask = AccessFlagBits.TransferWriteBit, DstAccessMask = AccessFlagBits.HostReadBit }],
                0, ReadOnlySpan<BufferMemoryBarrier>.Empty, 0, ReadOnlySpan<ImageMemoryBarrier>.Empty);
        });

        return readBack.Read();
    }

    private static void Barrier(CommandBuffer commands, Image image, AccessFlagBits from, AccessFlagBits to,
        ImageLayout oldLayout, ImageLayout newLayout, PipelineStageFlagBits after, PipelineStageFlagBits before)
    {
        var barrier = new ImageMemoryBarrier
        {
            SrcAccessMask = from,
            DstAccessMask = to,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED,
            DstQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED,
            Image = image,
            SubresourceRange = ColorRange()
        };
        commands.PipelineBarrier(after, before, 0, 0, null, 0, null, 1, [barrier]);
    }

    private static ImageSubresourceRange ColorRange() => new()
    {
        AspectMask = ImageAspectFlagBits.ColorBit,
        BaseMipLevel = 0,
        LevelCount = 1,
        BaseArrayLayer = 0,
        LayerCount = 1
    };

    private static string ColorAt(byte[] pixels, int x, int y)
    {
        var at = (y * Size + x) * 4;
        return $"{pixels[at]} {pixels[at + 1]} {pixels[at + 2]} {pixels[at + 3]}";
    }

    /// <summary>What the triangle is drawn into and with: each object destroyed, if made, whatever failed after it.
    /// </summary>
    private sealed class Target(ThisMachinesVulkan vulkan) : IDisposable
    {
        private readonly Device _device = vulkan.Device;
        private DeviceMemory _memory;

        public Image Image { get; private set; }

        public ImageView View { get; private set; }

        public PipelineLayout Layout { get; private set; }

        public Pipeline Pipeline { get; private set; }

        public void Create(byte[] vertexCode, byte[] fragmentCode)
        {
            Assert.That(_device.CreateImage(new ImageCreateInfo
            {
                ImageType = ImageType._2d,
                Format = ColorFormat,
                Extent = new Extent3D { Width = Size, Height = Size, Depth = 1 },
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = SampleCountFlagBits._1Bit,
                Tiling = ImageTiling.Optimal,
                Usage = ImageUsageFlagBits.ColorAttachmentBit | ImageUsageFlagBits.TransferSrcBit,
                SharingMode = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined
            }, null, out var image), Is.EqualTo(Result.Success));
            Image = image;

            _device.GetImageMemoryRequirements(Image, out var requirements);
            _memory = _device.AllocateMemory(new MemoryAllocateInfo
            {
                AllocationSize = requirements.Size,
                MemoryTypeIndex = vulkan.PhysicalDevice.FindCorrespondingMemoryType(requirements.MemoryTypeBits,
                    MemoryPropertyFlagBits.DeviceLocalBit)
            });
            _device.BindImageMemory(Image, _memory, 0);
            View = _device.CreateImageView(new ImageViewCreateInfo
            {
                Image = Image,
                ViewType = ImageViewType._2d,
                Format = ColorFormat,
                SubresourceRange = ColorRange()
            });

            Layout = _device.CreatePipelineLayout(new PipelineLayoutCreateInfo
            {
                PushConstantRangeCount = 1,
                PushConstantRanges = new[]
                {
                    new PushConstantRange { StageFlags = ShaderStageFlagBits.FragmentBit, Offset = 0, Size = sizeof(float) * 4 }
                }
            });
            Pipeline = CreatePipeline(vertexCode, fragmentCode);
        }

        public void Dispose()
        {
            if (Pipeline != null)
                _device.DestroyPipeline(Pipeline);
            if (Layout != null)
                _device.DestroyPipelineLayout(Layout);
            if (View != null)
                _device.DestroyImageView(View);
            if (Image != null)
                _device.DestroyImage(Image);
            if (_memory != null)
                _device.FreeMemory(_memory);
        }

        private Pipeline CreatePipeline(byte[] vertexCode, byte[] fragmentCode)
        {
            var vertexModule = _device.CreateShaderModule(new ShaderModuleCreateInfo { PCodeBytes = vertexCode });
            ShaderModule fragmentModule = null;
            try
            {
                fragmentModule = _device.CreateShaderModule(new ShaderModuleCreateInfo { PCodeBytes = fragmentCode });
                return _device.CreateGraphicsPipelines(null, 1, Describe(vertexModule, fragmentModule))[0];
            }
            finally
            {
                _device.DestroyShaderModule(vertexModule);
                if (fragmentModule != null)
                    _device.DestroyShaderModule(fragmentModule);
            }
        }

        private GraphicsPipelineCreateInfo Describe(ShaderModule vertexModule, ShaderModule fragmentModule) => new()
        {
            PNext = new PipelineRenderingCreateInfo { ColorAttachmentCount = 1, PColorAttachmentFormats = new[] { ColorFormat } },
            StageCount = 2,
            PStages = new[]
            {
                new PipelineShaderStageCreateInfo { Stage = ShaderStageFlagBits.VertexBit, Module = vertexModule, PName = "VertexMain" },
                new PipelineShaderStageCreateInfo { Stage = ShaderStageFlagBits.FragmentBit, Module = fragmentModule, PName = "FragmentMain" }
            },
            PVertexInputState = new PipelineVertexInputStateCreateInfo
            {
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = new[]
                {
                    new VertexInputBindingDescription { Binding = 0, Stride = sizeof(float) * 2, InputRate = VertexInputRate.Vertex }
                },
                VertexAttributeDescriptionCount = 1,
                PVertexAttributeDescriptions = new[]
                {
                    new VertexInputAttributeDescription { Binding = 0, Location = 0, Format = Format.R32G32_SFLOAT, Offset = 0 }
                }
            },
            PInputAssemblyState = new PipelineInputAssemblyStateCreateInfo { Topology = PrimitiveTopology.TriangleList },
            PViewportState = new PipelineViewportStateCreateInfo
            {
                ViewportCount = 1,
                PViewports = new[] { new Viewport { Width = Size, Height = Size, MaxDepth = 1 } },
                ScissorCount = 1,
                PScissors = new[]
                {
                    new Rect2D { Offset = new Offset2D(), Extent = new Extent2D { Width = Size, Height = Size } }
                }
            },
            PRasterizationState = new PipelineRasterizationStateCreateInfo
            {
                PolygonMode = PolygonMode.Fill,
                CullMode = CullModeFlagBits.None,
                FrontFace = FrontFace.Clockwise,
                LineWidth = 1
            },
            PMultisampleState = new PipelineMultisampleStateCreateInfo { RasterizationSamples = SampleCountFlagBits._1Bit },
            PColorBlendState = new PipelineColorBlendStateCreateInfo
            {
                AttachmentCount = 1,
                PAttachments = new[]
                {
                    new PipelineColorBlendAttachmentState
                    {
                        ColorWriteMask = ColorComponentFlagBits.RBit | ColorComponentFlagBits.GBit |
                                         ColorComponentFlagBits.BBit | ColorComponentFlagBits.ABit
                    }
                },
                BlendConstants = new float[4]
            },
            Layout = Layout
        };
    }
}

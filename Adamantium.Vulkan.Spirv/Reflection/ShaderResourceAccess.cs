namespace Adamantium.Vulkan.Spirv.Reflection;

/// <summary>What a shader does with a resource. Storage buffers and images are told by their <c>NonWritable</c> and
/// <c>NonReadable</c> decorations; every other resource is read only.</summary>
public enum ShaderResourceAccess
{
    /// <summary>Read and written.</summary>
    ReadWrite = 0,

    /// <summary>Only read: a uniform buffer, a sampled image, a sampler, or storage marked <c>NonWritable</c>.</summary>
    ReadOnly = 1,

    /// <summary>Only written: storage marked <c>NonReadable</c>.</summary>
    WriteOnly = 2
}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.Vulkan.Spirv.Cross;

namespace Adamantium.Vulkan.Spirv.Reflection
{
    public class SpirvReflectionResult
    {
        private List<ShaderReflectionResource> resources; 

        public SpirvReflectionResult()
        {
            resources = new List<ShaderReflectionResource>();
        }
        
        public ReadOnlyCollection<ShaderReflectionResource> UniformBuffers => resources.Where(x=>x.Description.Class == ResourceType.UniformBuffer).ToList().AsReadOnly(); // cBuffer (constant buffers)
        public ReadOnlyCollection<ShaderReflectionResource> Samplers => resources.Where(x => x.Description.Class == ResourceType.SeparateSamplers).ToList().AsReadOnly(); // Samplers
        public ReadOnlyCollection<ShaderReflectionResource> Images => resources.Where(x => x.Description.Class is ResourceType.SeparateImage or ResourceType.SampledImage).ToList().AsReadOnly(); // Textures
        public ReadOnlyCollection<ShaderReflectionResource> StorageImages => resources.Where(x => x.Description.Class == ResourceType.StorageImage).ToList().AsReadOnly(); // RWTextures
        public ReadOnlyCollection<ShaderReflectionResource> AccelerationStructures => resources.Where(x => x.Description.Class == ResourceType.AccelerationStructure).ToList().AsReadOnly(); // RWBuffers
        public ReadOnlyCollection<ShaderReflectionResource> StorageBuffers => resources.Where(x => x.Description.Class == ResourceType.StorageBuffer).ToList().AsReadOnly(); // StructuredBuffers

        public ReadOnlyCollection<ShaderReflectionResource> AllResources => resources.AsReadOnly();

        /// <summary>How many control points a tessellation control shader reads a patch of: the length of its inputs,
        /// built-in ones included, each an array over the patch. 0 for another stage, or one with no such input.
        /// </summary>
        public uint InputPatchSize { get; internal set; }

        internal void AddShaderResource(ShaderReflectionResource resource)
        {
            if (!resources.Contains(resource))
            {
                resources.Add(resource);
            }
        }
    }
}

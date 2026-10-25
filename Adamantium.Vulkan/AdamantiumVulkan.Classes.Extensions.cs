using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Adamantium.Vulkan.Core.Interop;
using Adamantium.Vulkan.Linux;
using Adamantium.Vulkan.Linux.Interop;
using Adamantium.Vulkan.Metal;
using Adamantium.Vulkan.Metal.Interop;
using Adamantium.Vulkan.Windows;
using Adamantium.Vulkan.Windows.Interop;
using QuantumBinding.Utils;

namespace Adamantium.Vulkan.Core
{
    public unsafe partial class Instance 
    {
        public static LayerProperties[] EnumerateInstanceLayerProperties()
        {
            uint layersCount = 0;
            var result = VulkanNative.EnumerateInstanceLayerProperties(ref layersCount, null);
            ResultHelper.CheckResult(result, nameof(VulkanNative.EnumerateInstanceLayerProperties));

            var layers = new LayerProperties[layersCount];
            result = VulkanNative.EnumerateInstanceLayerProperties(ref layersCount, layers);
            ResultHelper.CheckResult(result, nameof(VulkanNative.EnumerateInstanceLayerProperties));
            return layers;
        }

        public static ExtensionProperties[] EnumerateInstanceExtensionProperties(string layerName)
        {
            uint propertyCount = 0;
            var result = VulkanNative.EnumerateInstanceExtensionProperties(layerName, ref propertyCount, null);
            ResultHelper.CheckResult(result, nameof(VulkanNative.EnumerateInstanceExtensionProperties));

            var properties = new ExtensionProperties[propertyCount];
            result = VulkanNative.EnumerateInstanceExtensionProperties(layerName, ref propertyCount, properties);
            ResultHelper.CheckResult(result, nameof(EnumeratePhysicalDevices));
            return properties;
        }

        public static ExtensionProperties[] EnumerateInstanceExtensionProperties()
        {
            uint extensionsCount = 0;
            var result = VulkanNative.EnumerateInstanceExtensionProperties(null, ref extensionsCount, null);
            ResultHelper.CheckResult(result, nameof(VulkanNative.EnumerateInstanceExtensionProperties));

            var properties = new ExtensionProperties[extensionsCount];
            result = VulkanNative.EnumerateInstanceExtensionProperties(null, ref extensionsCount, properties);
            ResultHelper.CheckResult(result, nameof(EnumeratePhysicalDevices));
            return properties;
        }

        public static Instance Create(in InstanceCreateInfo createInfo, in AllocationCallbacks allocator = null)
        {
            var result = VulkanNative.CreateInstance(createInfo, allocator, out var instance);
            ResultHelper.CheckResult(result, nameof(Create));
            return instance;
        }

        public uint PhysicalDeviceCount
        {
            get
            {
                if (IsDisposed) return 0;

                uint physicalDeviceCount = 0;
                var result = EnumeratePhysicalDevices(ref physicalDeviceCount, null);
                ResultHelper.CheckResult(result, nameof(EnumeratePhysicalDevices));
                return physicalDeviceCount;
            }
        }

        public PhysicalDevice[] EnumeratePhysicalDevices()
        {
            var physicalDeviceCount = PhysicalDeviceCount;
            var pPhysicalDevices = new PhysicalDevice[physicalDeviceCount];
            var result = EnumeratePhysicalDevices(ref physicalDeviceCount, pPhysicalDevices);
            ResultHelper.CheckResult(result, nameof(EnumeratePhysicalDevices));

            return pPhysicalDevices;
        }
        
        public SurfaceKHR CreateWin32Surface(Win32SurfaceCreateInfoKHR surfaceInfo)
        {
            using var ctx = new NativeContext(surfaceInfo.GetSize(), stackalloc byte[(int)MarshalingUtils.StackAllocThreshold]);
            var native = surfaceInfo.MarshalToNative(ctx);
            var infoPtr = (VkWin32SurfaceCreateInfoKHR*)Unsafe.AsPointer(ref native);
            VkSurfaceKHR_T surface = default;
            var result = Commands.vkCreateWin32SurfaceKHR(this, infoPtr, null, &surface);
            ResultHelper.CheckResult(result, nameof(CreateWin32Surface));
            return new SurfaceKHR(surface);
        }
        
        /// <summary>A surface on a CAMetalLayer - on every Apple platform, through whichever driver runs Vulkan there.
        /// </summary>
        public SurfaceKHR CreateMetalSurface(MetalSurfaceCreateInfoEXT surfaceInfo)
        {
            using var ctx = new NativeContext(surfaceInfo.GetSize(), stackalloc byte[(int)MarshalingUtils.StackAllocThreshold]);
            var native = surfaceInfo.MarshalToNative(ctx);
            var infoPtr = (VkMetalSurfaceCreateInfoEXT*)Unsafe.AsPointer(ref native);
            VkSurfaceKHR_T surface = default;
            var result = Commands.vkCreateMetalSurfaceEXT(this, infoPtr, null, &surface);
            ResultHelper.CheckResult(result, nameof(CreateMetalSurface));
            return new SurfaceKHR(surface);
        }

        public SurfaceKHR CreateWaylandSurface(WaylandSurfaceCreateInfoKHR surfaceInfo)
        {
            using var ctx = new NativeContext(surfaceInfo.GetSize(), stackalloc byte[(int)MarshalingUtils.StackAllocThreshold]);
            var native = surfaceInfo.MarshalToNative(ctx);
            var infoPtr = (VkWaylandSurfaceCreateInfoKHR*)Unsafe.AsPointer(ref native);
            VkSurfaceKHR_T surface = default;
            var result = Commands.vkCreateWaylandSurfaceKHR(this, infoPtr, null, &surface);
            ResultHelper.CheckResult(result, nameof(CreateWaylandSurface));
            return new SurfaceKHR(surface);
        }
    }

    public static partial class InstanceExtension
    {
        public static SurfaceKHR CreateHeadlessSurface(this Instance instance, HeadlessSurfaceCreateInfoEXT surfaceInfo, AllocationCallbacks allocator = null)
        {
            var result = instance.CreateHeadlessSurfaceEXT(surfaceInfo, null, out var surface);
            ResultHelper.CheckResult(result, nameof(CreateHeadlessSurface));
            return surface;
        }
    }

    public partial class PhysicalDevice
    {
        public PhysicalDeviceMemoryProperties GetPhysicalDeviceMemoryProperties()
        {
            GetPhysicalDeviceMemoryProperties(out var memoryProperties);
            return memoryProperties;
        }

        public PhysicalDeviceProperties GetPhysicalDeviceProperties()
        {
            GetPhysicalDeviceProperties(out var deviceProperties);
            return deviceProperties;
        }

        public QueueFamilyProperties[] GetQueueFamilyProperties()
        {
            uint queueFamilyCount = 0;
            GetPhysicalDeviceQueueFamilyProperties(ref queueFamilyCount, null);

            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            GetPhysicalDeviceQueueFamilyProperties(ref queueFamilyCount, queueFamilies);

            return queueFamilies;
        }

        public PhysicalDeviceFeatures GetPhysicalDeviceFeatures()
        {
            GetPhysicalDeviceFeatures(out var deviceFeatures);
            ClearPhysicalDeviceFeatures(deviceFeatures);
            return deviceFeatures;
        }
        
        public PhysicalDeviceFeatures2 GetPhysicalDeviceFeatures2()
        {
            var deviceFeatures = new PhysicalDeviceFeatures2();
            GetPhysicalDeviceFeatures2(ref deviceFeatures);
            ClearPhysicalDeviceFeatures(deviceFeatures.Features);
            return deviceFeatures;
        }

        public Device CreateDevice(DeviceCreateInfo createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateDevice(createInfo, null, out var logicalDevice);
            ResultHelper.CheckResult(result, nameof(CreateDevice));
            return logicalDevice;
        }

        public SurfaceCapabilitiesKHR GetPhysicalDeviceSurfaceCapabilitiesKHR(SurfaceKHR surface)
        {
            var result = GetPhysicalDeviceSurfaceCapabilitiesKHR(surface, out var surfaceCapabilities);
            ResultHelper.CheckResult(result, nameof(GetPhysicalDeviceSurfaceCapabilitiesKHR));
            return surfaceCapabilities;
        }

        public SurfaceFormatKHR[] GetPhysicalDeviceSurfaceFormatsKHR(SurfaceKHR surface)
        {
            uint formatCount = 0;
            var result = GetPhysicalDeviceSurfaceFormatsKHR(surface, ref formatCount, null);
            ResultHelper.CheckResult(result, nameof(GetPhysicalDeviceSurfaceFormatsKHR));

            if (formatCount != 0)
            {
                var formats = new SurfaceFormatKHR[formatCount];
                result = GetPhysicalDeviceSurfaceFormatsKHR(surface, ref formatCount, formats);
                ResultHelper.CheckResult(result, nameof(GetPhysicalDeviceSurfaceFormatsKHR));
                return formats;
            }

            return new SurfaceFormatKHR[0];
        }

        public PresentModeKHR[] GetPhysicalDeviceSurfacePresentModesKHR(SurfaceKHR surface)
        {
            uint presentModeCount = 0;
            var result = GetPhysicalDeviceSurfacePresentModesKHR(surface, ref presentModeCount, null);
            ResultHelper.CheckResult(result, nameof(GetPhysicalDeviceSurfacePresentModesKHR));

            if (presentModeCount != 0)
            {
                var presentModes = new PresentModeKHR[presentModeCount];
                result = GetPhysicalDeviceSurfacePresentModesKHR(surface, ref presentModeCount, presentModes);
                ResultHelper.CheckResult(result, nameof(GetPhysicalDeviceSurfacePresentModesKHR));
                return presentModes;
            }

            return [];
        }

        public uint FindCorrespondingMemoryType(uint typeFilter, MemoryPropertyFlagBits properties)
        {
            var memProperties = GetPhysicalDeviceMemoryProperties();

            for (int i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                var shift = 1 << i;
                if ((typeFilter & shift) > 0 && 
                    (memProperties.MemoryTypes.Span[i].PropertyFlags).HasFlag(properties))
                {
                    return (uint)i;
                }
            }

            throw new Exception("failed to find suitable memory type!");
        }

        private void ClearPhysicalDeviceFeatures(PhysicalDeviceFeatures deviceFeatures)
        {
            deviceFeatures.RobustBufferAccess = VkBool32.FALSE;
            deviceFeatures.FullDrawIndexUint32 = VkBool32.FALSE;                    
            deviceFeatures.ImageCubeArray = VkBool32.FALSE;                         
            deviceFeatures.IndependentBlend = VkBool32.FALSE;                       
            deviceFeatures.GeometryShader = VkBool32.FALSE;                         
            deviceFeatures.TessellationShader = VkBool32.FALSE;                     
            deviceFeatures.SampleRateShading = VkBool32.FALSE;                      
            deviceFeatures.DualSrcBlend = VkBool32.FALSE;                           
            deviceFeatures.LogicOp = VkBool32.FALSE;                                
            deviceFeatures.MultiDrawIndirect = VkBool32.FALSE;                      
            deviceFeatures.DrawIndirectFirstInstance = VkBool32.FALSE;              
            deviceFeatures.DepthClamp = VkBool32.FALSE;                             
            deviceFeatures.DepthBiasClamp = VkBool32.FALSE;                         
            deviceFeatures.FillModeNonSolid = VkBool32.FALSE;                       
            deviceFeatures.DepthBounds = VkBool32.FALSE;                            
            deviceFeatures.WideLines = VkBool32.FALSE;                              
            deviceFeatures.LargePoints = VkBool32.FALSE;                            
            deviceFeatures.AlphaToOne = VkBool32.FALSE;                             
            deviceFeatures.MultiViewport = VkBool32.FALSE;                          
            deviceFeatures.SamplerAnisotropy = VkBool32.FALSE;                      
            deviceFeatures.TextureCompressionETC2 = VkBool32.FALSE;                 
            deviceFeatures.TextureCompressionASTC_LDR = VkBool32.FALSE;             
            deviceFeatures.TextureCompressionBC = VkBool32.FALSE;                   
            deviceFeatures.OcclusionQueryPrecise = VkBool32.FALSE;                  
            deviceFeatures.PipelineStatisticsQuery = VkBool32.FALSE;                
            deviceFeatures.VertexPipelineStoresAndAtomics = VkBool32.FALSE;         
            deviceFeatures.FragmentStoresAndAtomics = VkBool32.FALSE;              
            deviceFeatures.ShaderTessellationAndGeometryPointSize = VkBool32.FALSE; 
            deviceFeatures.ShaderImageGatherExtended = VkBool32.FALSE;              
            deviceFeatures.ShaderStorageImageExtendedFormats = VkBool32.FALSE;     
            deviceFeatures.ShaderStorageImageMultisample = VkBool32.FALSE;      
            deviceFeatures.ShaderStorageImageReadWithoutFormat = VkBool32.FALSE;
            deviceFeatures.ShaderStorageImageWriteWithoutFormat = VkBool32.FALSE;
            deviceFeatures.ShaderUniformBufferArrayDynamicIndexing = VkBool32.FALSE;
            deviceFeatures.ShaderSampledImageArrayDynamicIndexing = VkBool32.FALSE;
            deviceFeatures.ShaderStorageBufferArrayDynamicIndexing = VkBool32.FALSE;
            deviceFeatures.ShaderStorageImageArrayDynamicIndexing = VkBool32.FALSE;
            deviceFeatures.ShaderClipDistance = VkBool32.FALSE;               
            deviceFeatures.ShaderCullDistance = VkBool32.FALSE;
            deviceFeatures.ShaderFloat64 = VkBool32.FALSE;                          
            deviceFeatures.ShaderInt64 = VkBool32.FALSE;
            deviceFeatures.ShaderInt16 = VkBool32.FALSE;                            
            deviceFeatures.ShaderResourceResidency = VkBool32.FALSE;                
            deviceFeatures.ShaderResourceMinLod = VkBool32.FALSE;                   
            deviceFeatures.SparseBinding = VkBool32.FALSE;                        
            deviceFeatures.SparseResidencyBuffer = VkBool32.FALSE;                  
            deviceFeatures.SparseResidencyImage2D = VkBool32.FALSE;                 
            deviceFeatures.SparseResidencyImage3D = VkBool32.FALSE;                 
            deviceFeatures.SparseResidency2Samples = VkBool32.FALSE;               
            deviceFeatures.SparseResidency4Samples = VkBool32.FALSE;               
            deviceFeatures.SparseResidency8Samples = VkBool32.FALSE;               
            deviceFeatures.SparseResidency16Samples = VkBool32.FALSE;               
            deviceFeatures.SparseResidencyAliased = VkBool32.FALSE;                 
            deviceFeatures.VariableMultisampleRate = VkBool32.FALSE;                
            deviceFeatures.InheritedQueries = VkBool32.FALSE;                     
        }

        public Result GetPhysicalDeviceSurfaceSupport(uint queueFamilyIndex, SurfaceKHR surface, out bool pSupported)
        {
            var result = GetPhysicalDeviceSurfaceSupportKHR(queueFamilyIndex, surface, out var supported);
            pSupported = (VkBool32)supported;
            return result;
        }
    }

    public unsafe partial class Device
    {
        public Queue GetDeviceQueue(uint queueFamilyIndex, uint queueIndex)
        {
            GetDeviceQueue(queueFamilyIndex, queueIndex, out var queue);
            return queue;
        }

        public Queue GetDeviceQueue2(DeviceQueueInfo2 queueInfo)
        {
            GetDeviceQueue2(queueInfo, out var pQueue);
            return pQueue;
        }

        public SwapchainKHR CreateSwapchainKHR(SwapchainCreateInfoKHR createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateSwapchainKHR(createInfo, allocator, out var swapchain);
            ResultHelper.CheckResult(result, nameof(CreateSwapchainKHR));
            return swapchain;
        }

        public Image[] GetSwapchainImagesKHR(SwapchainKHR swapchain)
        {
            uint imageCount = 0;
            var result = GetSwapchainImagesKHR(swapchain, ref imageCount, null);
            ResultHelper.CheckResult(result, nameof(GetSwapchainImagesKHR));

            var swapchainImages = new Image[imageCount];
            result = GetSwapchainImagesKHR(swapchain, ref imageCount, swapchainImages);
            ResultHelper.CheckResult(result, nameof(GetSwapchainImagesKHR));

            return swapchainImages;
        }

        public ImageView CreateImageView(ImageViewCreateInfo createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateImageView(createInfo, allocator, out var view);
            ResultHelper.CheckResult(result, nameof(CreateImageView));
            return view;
        }

        public RenderPass CreateRenderPass(RenderPassCreateInfo createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateRenderPass(createInfo, allocator, out var renderPass);
            ResultHelper.CheckResult(result, nameof(CreateRenderPass));
            return renderPass;
        }

        public PipelineLayout CreatePipelineLayout(PipelineLayoutCreateInfo createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreatePipelineLayout(createInfo, allocator, out var pipelineLayout);
            ResultHelper.CheckResult(result, nameof(CreatePipelineLayout));
            return pipelineLayout;
        }

        public Pipeline[] CreateGraphicsPipelines(PipelineCache pipelineCache, uint createInfoCount, GraphicsPipelineCreateInfo createInfos, AllocationCallbacks allocator = null)
        {
            var result = CreateGraphicsPipelines(pipelineCache, createInfoCount, createInfos, allocator, out var pipelines);
            ResultHelper.CheckResult(result, nameof(CreateGraphicsPipelines));
            return pipelines;
        }

        public Pipeline[] CreateComputePipelines(PipelineCache pipelineCache, uint createInfoCount, ComputePipelineCreateInfo createInfos, AllocationCallbacks allocator = null)
        {
            var result = CreateComputePipelines(pipelineCache, createInfoCount, createInfos, allocator, out var pipelines);
            ResultHelper.CheckResult(result, nameof(CreateComputePipelines));
            return pipelines;
        }

        public Framebuffer CreateFramebuffer(FramebufferCreateInfo createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateFramebuffer(createInfo, allocator, out var framebuffer);
            ResultHelper.CheckResult(result, nameof(CreateFramebuffer));
            return framebuffer;
        }

        public CommandPool CreateCommandPool(CommandPoolCreateInfo createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateCommandPool(createInfo, allocator, out var commandPool);
            ResultHelper.CheckResult(result, nameof(CreateCommandPool));
            return commandPool;
        }

        public Buffer CreateBuffer(BufferCreateInfo createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateBuffer(createInfo, allocator, out var buffer);
            ResultHelper.CheckResult(result, nameof(CreateBuffer));
            return buffer;
        }

        public MemoryRequirements GetBufferMemoryRequirements(Buffer buffer)
        {
            GetBufferMemoryRequirements(buffer, out var memoryRequirements);
            return memoryRequirements;
        }

        public DeviceMemory AllocateMemory(MemoryAllocateInfo allocateInfo, AllocationCallbacks allocator = null)
        {
            var result = AllocateMemory(allocateInfo, allocator, out var deviceMemory);
            ResultHelper.CheckResult(result, nameof(AllocateMemory));
            return deviceMemory;
        }

        public nuint MapMemory(DeviceMemory memory, ulong offset, ulong size, MemoryMapFlagBits flags)
        {
            var result = MapMemory(memory, offset, size, flags, out var data);
            ResultHelper.CheckResult(result, nameof(MapMemory));
            return data;
        }

        public CommandBuffer[] AllocateCommandBuffers(CommandBufferAllocateInfo allocateInfo)
        {
            var result = AllocateCommandBuffers(allocateInfo, out var commandBuffers);
            ResultHelper.CheckResult(result, nameof(AllocateCommandBuffers));
            return commandBuffers;
        }

        public Semaphore CreateSemaphore(SemaphoreCreateInfo createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateSemaphore(createInfo, allocator, out var semaphore);
            ResultHelper.CheckResult(result, nameof(CreateSemaphore));
            return semaphore;
        }

        public Semaphore[] CreateSemaphores(SemaphoreCreateInfo createInfo, uint semaphoreCount, AllocationCallbacks allocator = null)
        {
            if (semaphoreCount == 0)
            {
                throw new ArgumentOutOfRangeException("Semaphore count should be non-zero");
            }

            var semaphores = new Semaphore[semaphoreCount];
            for (var i = 0; i < semaphoreCount; ++i)
            {
                semaphores[i] = CreateSemaphore(createInfo, allocator);
            }

            return semaphores;
        }

        public Fence CreateFence(FenceCreateInfo createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateFence(createInfo, allocator, out var fence);
            ResultHelper.CheckResult(result, nameof(CreateFence));
            return fence;
        }

        public Fence[] CreateFences(FenceCreateInfo createInfo, uint fenceCount, AllocationCallbacks allocator = null)
        {
            if (fenceCount == 0)
            {
                throw new ArgumentOutOfRangeException("Fence count count should be non-zero");
            }

            var fences = new Fence[fenceCount];
            for (var i = 0; i < fenceCount; ++i)
            {
                fences[i] = CreateFence(createInfo, allocator);
            }

            return fences;
        }

        public ShaderModule CreateShaderModule(ShaderModuleCreateInfo createInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateShaderModule(createInfo, allocator, out var shaderModule);
            ResultHelper.CheckResult(result, nameof(CreateShaderModule));
            return shaderModule;
        }

        public CommandBuffer BeginSingleTimeCommand(CommandPool commandPool)
        {
            var allocInfo = new CommandBufferAllocateInfo();
            allocInfo.Level = CommandBufferLevel.Primary;
            allocInfo.CommandPool = commandPool;
            allocInfo.CommandBufferCount = 1;

            var commandBuffers = AllocateCommandBuffers(allocInfo);

            var beginInfo = new CommandBufferBeginInfo();
            beginInfo.Flags = CommandBufferUsageFlagBits.OneTimeSubmitBit;

            var commandBuffer = commandBuffers[0];
            commandBuffer.BeginCommandBuffer(beginInfo);

            return commandBuffer;
        }

        public void EndSingleTimeCommands(Queue queue, CommandPool commandPool, CommandBuffer commandBuffer)
        {
            commandBuffer.EndCommandBuffer();
            var fenceInfo = new FenceCreateInfo();
            var fence = CreateFence(fenceInfo);

            var submitInfo = new SubmitInfo();
            submitInfo.CommandBufferCount = 1;
            submitInfo.PCommandBuffers = new ReadOnlyMemory<CommandBuffer>([commandBuffer]);

            var submitInfoArray = new SubmitInfo[1];
            submitInfoArray[0] = submitInfo;
            queue.QueueSubmit(1, submitInfoArray, fence);
            WaitForFences(1, fence, true, ulong.MaxValue);
            
            FreeCommandBuffers(commandPool, 1, commandBuffer);
            DestroyFence(fence);
        }

        public DescriptorPool CreateDescriptorPool(DescriptorPoolCreateInfo descriptorPoolInfo, AllocationCallbacks allocator = null)
        {
            var result = CreateDescriptorPool(descriptorPoolInfo, null, out var descriptorPool);
            ResultHelper.CheckResult(result, nameof(CreateDescriptorPool));
            return descriptorPool;
        }
        
        public uint GetDescriptorSetLayoutOffset(DescriptorSetLayout layout, uint bindingSlot)
        {
            GetDescriptorSetLayoutBindingOffsetEXT(layout, bindingSlot, out var offsetSize);
            return (uint)offsetSize;
        }

        /// <summary>
        /// After a device-lost (<see cref="Result.ErrorDeviceLost"/>), retrieves the VK_EXT_device_fault report - the
        /// driver's human-readable <see cref="DeviceFaultInfoEXT.Description"/> plus every faulting GPU address region
        /// (read/write/execute-invalid + the reported address) and any vendor records. Runs the two-phase
        /// count -> fill protocol itself: this is the one Vulkan query whose array lengths come back in a SEPARATE
        /// counts struct (not inside the info struct, nor as a sibling count param), so it can't be auto-generated.
        /// </summary>
        public Result GetDeviceFaultInfoEXT(out DeviceFaultInfoEXT faultInfo)
        {
            faultInfo = null;

            // Phase 1: a null pFaultInfo makes the driver report ONLY the counts (the array lengths live here).
            VkDeviceFaultCountsEXT counts = new() { sType = StructureType.DeviceFaultCountsExt };
            var result = Commands.vkGetDeviceFaultInfoEXT(this, &counts, null);
            if (result != Result.Success && result != Result.Incomplete) return result;

            int addressCount = (int)counts.addressInfoCount;
            int vendorCount = (int)counts.vendorInfoCount;

            var nativeAddresses = new VkDeviceFaultAddressInfoKHR[addressCount];
            var nativeVendors = new VkDeviceFaultVendorInfoKHR[vendorCount];

            // We do NOT retrieve the vendor binary blob (pVendorBinaryData stays null), so its capacity MUST read 0 for
            // phase 2. Phase 1 left counts.vendorBinarySize = the driver's required size (often large); reusing that with
            // a null buffer tells the driver "here is an N-byte buffer" at address null -> it writes into null -> the AV
            // in vkGetDeviceFaultInfoEXT. Zeroing it opts out of the blob per spec.
            counts.vendorBinarySize = 0;

            // Phase 2: hand the driver the count-sized arrays; it fills them + the description in-place.
            fixed (VkDeviceFaultAddressInfoKHR* pAddresses = nativeAddresses)
            fixed (VkDeviceFaultVendorInfoKHR* pVendors = nativeVendors)
            {
                VkDeviceFaultInfoEXT nativeInfo = new()
                {
                    sType = StructureType.DeviceFaultInfoExt,
                    pAddressInfos = addressCount > 0 ? pAddresses : null,
                    pVendorInfos = vendorCount > 0 ? pVendors : null,
                };
                // INCOMPLETE is the expected answer here, not a failure: zeroing vendorBinarySize above opts out of the
                // vendor blob, and the driver reports "written, but not all of it" for exactly that. The description and
                // the address records ARE filled - treating this as failure threw away the whole diagnostic, which is
                // why the fault probe looked like it only worked sometimes.
                result = Commands.vkGetDeviceFaultInfoEXT(this, &counts, &nativeInfo);
                if (result != Result.Success && result != Result.Incomplete) return result;
                faultInfo = new DeviceFaultInfoEXT(in nativeInfo);   // keeps InteropSource + marshals the description
            }

            // The arrays are filled now - wrap each element here, the only place that knows the counts.
            if (addressCount > 0)
            {
                var addresses = new DeviceFaultAddressInfoKHR[addressCount];
                for (int i = 0; i < addressCount; i++) addresses[i] = new DeviceFaultAddressInfoKHR(in nativeAddresses[i]);
                faultInfo.PAddressInfos = addresses;
            }
            if (vendorCount > 0)
            {
                var vendors = new DeviceFaultVendorInfoKHR[vendorCount];
                for (int i = 0; i < vendorCount; i++) vendors[i] = new DeviceFaultVendorInfoKHR(in nativeVendors[i]);
                faultInfo.PVendorInfos = vendors;
            }

            return result;
        }

    }

    public unsafe partial class ShaderModuleCreateInfo
    {
        public ReadOnlySpan<byte> PCodeBytes
        {
            set
            {
                if (value.Length % 4 != 0) throw new ArgumentException("SPIR-V size must be % 4");
                PCode = MemoryMarshal.Cast<byte, uint>(value).ToArray();
                CodeSize = (nuint)value.Length;
            }
        }
    }

    public unsafe partial class Device
    {
        /// <summary>
        /// Creates a shader object from a driver BINARY (VK_SHADER_CODE_TYPE_BINARY_EXT) with the 16-byte
        /// <c>pCode</c> alignment the spec mandates (VUID-VkShaderCreateInfoEXT-pCode-08878). The generated struct
        /// marshaller packs <c>pCode</c> at an 8-byte-aligned cursor offset - fine for SPIR-V (needs 4) but a binary
        /// needs 16, and NVIDIA rejects an under-aligned binary with <c>VK_INCOMPATIBLE_SHADER_BINARY_EXT</c> even for
        /// a binary it just produced. This path allocates <c>pCode</c> on a 16-byte boundary so cached binaries load.
        /// Only the fields the engine sets for shader objects are marshalled (no pNext, no push-constant ranges,
        /// no specialization info).
        /// </summary>
        public Result CreateShaderFromBinary(ShaderCreateInfoEXT info, out ShaderEXT shader)
        {
            shader = null;
            var code = info.PCode.Span;
            void* alignedCode = NativeMemory.AlignedAlloc((nuint)code.Length, 16);
            sbyte* namePtr = null;
            VkDescriptorSetLayout_T* setLayouts = null;
            try
            {
                code.CopyTo(new Span<byte>(alignedCode, code.Length));

                if (!string.IsNullOrEmpty(info.PName))
                {
                    var byteCount = System.Text.Encoding.UTF8.GetByteCount(info.PName);
                    namePtr = (sbyte*)NativeMemory.Alloc((nuint)(byteCount + 1));
                    var nameSpan = new Span<byte>(namePtr, byteCount + 1);
                    System.Text.Encoding.UTF8.GetBytes(info.PName, nameSpan);
                    nameSpan[byteCount] = 0;
                }

                var setLayoutCount = info.SetLayoutCount;
                if (setLayoutCount > 0 && !info.PSetLayouts.IsEmpty)
                {
                    setLayouts = (VkDescriptorSetLayout_T*)NativeMemory.Alloc((nuint)((int)setLayoutCount * sizeof(VkDescriptorSetLayout_T)));
                    var src = info.PSetLayouts.Span;
                    for (int i = 0; i < (int)setLayoutCount; i++) setLayouts[i] = src[i];
                }

                var native = new VkShaderCreateInfoEXT
                {
                    sType = StructureType.ShaderCreateInfoExt,
                    pNext = null,
                    flags = info.Flags,
                    stage = info.Stage,
                    nextStage = info.NextStage,
                    codeType = info.CodeType,
                    codeSize = (nuint)code.Length,
                    pCode = (byte*)alignedCode,
                    pName = namePtr,
                    setLayoutCount = setLayoutCount,
                    pSetLayouts = setLayouts,
                    pushConstantRangeCount = 0,
                    pPushConstantRanges = null,
                    pSpecializationInfo = null
                };

                var outShader = stackalloc VkShaderEXT_T[1];
                var result = Commands.vkCreateShadersEXT(this, 1, &native, null, outShader);
                if (result == Result.Success) shader = outShader[0];
                return result;
            }
            finally
            {
                NativeMemory.AlignedFree(alignedCode);
                if (namePtr != null) NativeMemory.Free(namePtr);
                if (setLayouts != null) NativeMemory.Free(setLayouts);
            }
        }
    }

    public unsafe partial class CommandBuffer
    {
        public void PipelineBarrier(PipelineStageFlagBits srcStageMask, PipelineStageFlagBits dstStageMask, DependencyFlagBits dependencyFlags, uint memoryBarrierCount, in ReadOnlySpan<MemoryBarrier> pMemoryBarriers, uint bufferMemoryBarrierCount, in ReadOnlySpan<BufferMemoryBarrier> pBufferMemoryBarriers, uint imageMemoryBarrierCount, in ImageMemoryBarrier pImageMemoryBarriers)
    {
        int CalculateSize(ReadOnlySpan<MemoryBarrier> pMemoryBarriers, ReadOnlySpan<BufferMemoryBarrier> pBufferMemoryBarriers, ImageMemoryBarrier pImageMemoryBarriers)
        {
            int totalSize = 0;
            for (var i = 0U; i < pMemoryBarriers.Length; i++)
            {
                if(pMemoryBarriers[(int)i] == null)
                    totalSize += Marshal.SizeOf<Adamantium.Vulkan.Core.Interop.VkMemoryBarrier>();
                else
                    totalSize += pMemoryBarriers[(int)i].GetSize();
            }
            for (var i = 0U; i < pBufferMemoryBarriers.Length; i++)
            {
                if(pBufferMemoryBarriers[(int)i] == null)
                    totalSize += Marshal.SizeOf<Adamantium.Vulkan.Core.Interop.VkBufferMemoryBarrier>();
                else
                    totalSize += pBufferMemoryBarriers[(int)i].GetSize();
            }

            if (pImageMemoryBarriers != null)
            {
                totalSize += pImageMemoryBarriers.GetSize();
            }
            
            return totalSize;
        }

        var totalSize = CalculateSize(pMemoryBarriers, pBufferMemoryBarriers, pImageMemoryBarriers);
        byte[] rentedArray = null;
        var mainBuffer = totalSize <= QuantumBinding.Utils.MarshalingUtils.StackAllocThreshold ? stackalloc byte[totalSize] : (rentedArray = System.Buffers.ArrayPool<byte>.Shared.Rent(totalSize)).AsSpan(0, totalSize);
        // PINNED, as every generated call site is: above the threshold this buffer is a managed array, and the native
        // pointers below point INTO it - a collection moving it mid-call would hand the driver a stale address.
        fixed (byte* bufferPtr = mainBuffer)
        {
            try
            {
                ref System.Span<byte> currentCursor = ref mainBuffer;
                Adamantium.Vulkan.Core.Interop.VkMemoryBarrier* arg5 = null;
                if (!pMemoryBarriers.IsEmpty)
                {
                    arg5 = QuantumBinding.Utils.MarshalContextUtils.MarshalArrayOfWrappers<Adamantium.Vulkan.Core.MemoryBarrier, Adamantium.Vulkan.Core.Interop.VkMemoryBarrier>(pMemoryBarriers, ref currentCursor);
                }
                Adamantium.Vulkan.Core.Interop.VkBufferMemoryBarrier* arg7 = null;
                if (!pBufferMemoryBarriers.IsEmpty)
                {
                    arg7 = QuantumBinding.Utils.MarshalContextUtils.MarshalArrayOfWrappers<Adamantium.Vulkan.Core.BufferMemoryBarrier, Adamantium.Vulkan.Core.Interop.VkBufferMemoryBarrier>(pBufferMemoryBarriers, ref currentCursor);
                }
                Adamantium.Vulkan.Core.Interop.VkImageMemoryBarrier* arg9 = null;
                if (pImageMemoryBarriers != null)
                {
                    arg9 = QuantumBinding.Utils.MarshalContextUtils.MarshalStructToPointer<ImageMemoryBarrier, Adamantium.Vulkan.Core.Interop.VkImageMemoryBarrier>(pImageMemoryBarriers, ref currentCursor);
                }
                Commands.vkCmdPipelineBarrier(this, srcStageMask, dstStageMask, dependencyFlags, memoryBarrierCount, arg5, bufferMemoryBarrierCount, arg7, imageMemoryBarrierCount, arg9);
            }
            finally
            {
                if (rentedArray != null)
                    System.Buffers.ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }
    }

    public unsafe partial class Framebuffer
    {
        public void Destroy(Device device)
        {
            if (NativePointer == null) return;

            device.DestroyFramebuffer(this);
            __Instance.pointer = null;
        }
    }

    public unsafe partial class Image
    {
        public void Destroy(Device device)
        {
            if (NativePointer == null) return;

            device.DestroyImage(this);
            __Instance.pointer = null;
        }
    }

    public unsafe partial class ImageView
    {
        public void Destroy(Device device)
        {
            if (NativePointer == null) return;

            device.DestroyImageView(this);
            __Instance.pointer = null;
        }
    }

    public unsafe partial class Pipeline
    {
        public void Destroy(Device device)
        {
            if (NativePointer == null) return;

            device.DestroyPipeline(this);
            __Instance.pointer = null;
        }
    }

    public unsafe partial class SwapchainKHR
    {
        public void Destroy(Device device)
        {
            if (NativePointer == null) return;

            device.DestroySwapchainKHR(this);
            __Instance.pointer = null;
        }
    }

    public unsafe partial class DeviceMemory
    {
        public void FreeMemory(Device device)
        {
            if (NativePointer == null) return;

            device.FreeMemory(this);
            __Instance.pointer = null;
        }
    }

    public unsafe partial class Buffer
    {
        public void Destroy(Device device)
        {
            if (NativePointer == null) return;

            device.DestroyBuffer(this);
            __Instance.pointer = null;
        }
    }

    public unsafe partial class BufferView
    {
        public void Destroy(Device device)
        {
            if (NativePointer == null) return;

            device.DestroyBufferView(this);
            __Instance.pointer = null;
        }
    }

    public unsafe partial class DescriptorSetLayout
    {
        public void Destroy(Device device)
        {
            if (NativePointer == null) return;

            device.DestroyDescriptorSetLayout(this);
            __Instance.pointer = null;
        }
    }

    public unsafe partial class PipelineLayout
    {
        public void Destroy(Device device)
        {
            if (NativePointer == null) return;

            device.DestroyPipelineLayout(this);
            __Instance.pointer = null;
        }
    }

    public unsafe partial class ExtensionProperties
    {
        public override string ToString()
        {
            return $"{ExtensionName} : {SpecVersion}";
        }
    }
}

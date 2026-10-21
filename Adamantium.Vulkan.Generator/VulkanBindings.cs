using System;
using System.Collections.Generic;
using QuantumBinding.Generator;
using QuantumBinding.Generator.AST;
using QuantumBinding.Generator.ProcessingFluentApi;
using QuantumBinding.Generator.Processors;
using QuantumBinding.Generator.Types;

namespace Adamantium.Vulkan.Generator;

public static partial class VulkanBindings
{
    public static void ProvideFunctionsForParametersFix(ProcessingContext ctx)
    {
        var api = new PostProcessingApi();

        api.Class("VkFramebufferAttachmentImageInfoKHR").WithField("pViewFormats")
            .InterpretAsPointerToArray(new CustomType("vkFormat"), true, "viewFormatCount");

        api.Class("VkInstanceCreateInfo")
            .WithField("ppEnabledLayerNames")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.Sbyte), arraySizeSource: "enabledLayerCount")
            .WithField("ppEnabledExtensionNames")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.Sbyte), arraySizeSource: "enabledExtensionCount");
        
        api.Class("VkDeviceCreateInfo")
            .WithField("ppEnabledLayerNames")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.Sbyte), arraySizeSource: "enabledLayerCount")
            .WithField("ppEnabledExtensionNames")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.Sbyte), arraySizeSource: "enabledExtensionCount");

        api.Class("VkPipelineViewportStateCreateInfo")
            .WithField("pViewports")
            .InterpretAsPointerToArray(new CustomType("VkViewport"), true, "viewportCount")
            .WithField("pScissors")
            .InterpretAsPointerToArray(new CustomType("VkRect2D"), true, "scissorCount");

        api.Class("VkPipelineColorBlendStateCreateInfo")
            .WithField("pAttachments")
            .InterpretAsPointerToArray(new CustomType("VkPipelineColorBlendAttachmentState"), true,
                "attachmentCount");

        api.Class("VkDescriptorPoolCreateInfo")
            .WithField("pPoolSizes")
            .InterpretAsPointerToArray(new CustomType("VkDescriptorPoolSize"), true, "poolSizeCount");

        api.Class("VkWriteDescriptorSet")
            .WithField("pImageInfo")
            .InterpretAsPointerToArray(new CustomType("VkDescriptorImageInfo"), true, "descriptorCount")
            .WithField("pBufferInfo")
            .InterpretAsPointerToArray(new CustomType("VkDescriptorBufferInfo"), true, "descriptorCount")
            .WithField("pTexelBufferView")
            .InterpretAsPointerToArray(new CustomType("VkBufferView"), true, "descriptorCount");

        api.Class("VkPipelineRenderingCreateInfo")
            .WithField("pColorAttachmentFormats")
            .InterpretAsPointerToArray(new CustomType("VkFormat"), arraySizeSource: "colorAttachmentCount");

        api.Class("VkRenderingInfo")
            .WithField("pColorAttachments")
            .InterpretAsPointerToArray(new CustomType("VkRenderingAttachmentInfo"), arraySizeSource: "colorAttachmentCount");

        api.Class("VkShaderCreateInfoEXT")
            .WithField("pCode")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.Byte))
            .WithField("pSetLayouts")
            .InterpretAsPointerToArray(new CustomType("VkDescriptorSetLayout_T"), arraySizeSource: "setLayoutCount")
            .WithField("pPushConstantRanges")
            .InterpretAsPointerToArray(new CustomType("VkPushConstantRange"), arraySizeSource: "pushConstantRangeCount");
        
        api.Class("VkAccelerationStructureBuildGeometryInfoKHR")
            .WithField("pGeometries")
            .InterpretAsPointerToArray(new CustomType("VkAccelerationStructureGeometryKHR"),
                arraySizeSource: "geometryCount")
            .WithField("ppGeometries")
            .InterpretAsPointerToArray(new CustomType("VkAccelerationStructureGeometryKHR"),
                arraySizeSource: "geometryCount");

        api.Class("VkImageFormatListCreateInfo")
            .WithField("pViewFormats")
            .InterpretAsPointerToArray(new CustomType("VkFormat"), arraySizeSource: "viewFormatCount");

        api.Class("VkFramebufferAttachmentImageInfo")
            .WithField("pViewFormats")
            .InterpretAsPointerToArray(new CustomType("VkFormat"), arraySizeSource: "viewFormatCount");

        api.Class("VkIndirectCommandsLayoutTokenNV")
            .WithField("pIndexTypes")
            .InterpretAsPointerToArray(new CustomType("VkIndexType"), arraySizeSource: "indexTypeCount")
            .WithField("pIndexTypeValues")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.UInt32), arraySizeSource: "indexTypeCount");

        api.Class("VkPipelineColorWriteCreateInfoEXT")
            .WithField("pColorWriteEnables")
            .InterpretAsPointerToArray(new CustomType("VkBool32"), arraySizeSource: "attachmentCount");
        
        api.Function("vkUpdateDescriptorSets")
            .WithParameterName("pDescriptorWrites")
            .InterpretAsPointerToArray(new CustomType("VkWriteDescriptorSet"), true, "descriptorWriteCount")
            .WithParameterName("pDescriptorCopies")
            .InterpretAsPointerToArray(new CustomType("VkCopyDescriptorSet"), true, "descriptorCopyCount")
            .SetParameterKind(ParameterKind.Out);

        api.Function("vkGetDisplayPlaneSupportedDisplaysKHR")
            .WithParameterName("pDisplays")
            .InterpretAsPointerToArray(new CustomType("VkDisplayKHR_T"), true, "pDisplayCount")
            .SetParameterKind(ParameterKind.Out);

        api.Function("vkGetPhysicalDeviceDisplayPlanePropertiesKHR")
            .WithParameterName("pProperties")
            .InterpretAsPointerToArray(new CustomType("VkDisplayPlanePropertiesKHR"), true, "pPropertyCount")
            .SetParameterKind(ParameterKind.Out);

        api.Function("vkEnumerateDeviceExtensionProperties")
            .WithParameterName("pProperties")
            .InterpretAsPointerToArray(new CustomType("VkExtensionProperties"), true, "pPropertyCount")
            .SetParameterKind(ParameterKind.Ref);

        api.Function("vkEnumerateDeviceLayerProperties")
            .WithParameterName("pProperties")
            .InterpretAsPointerToArray(new CustomType("VkLayerProperties"), true, "pPropertyCount")
            .SetParameterKind(ParameterKind.Ref);

        api.Function("vkCmdSetScissor")
            .WithParameterName("pScissors")
            .InterpretAsPointerToArray(new CustomType("VkRect2D"), true, "scissorCount");

        api.Function("vkCmdSetViewport")
            .WithParameterName("pViewports")
            .InterpretAsPointerToArray(new CustomType("VkViewport"), true, "viewportCount");

        api.Function("vkGetPhysicalDeviceSurfaceSupportKHR")
            .WithParameterName("pSupported")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.Out);
        
        api.Function("vkGetMemoryWin32HandleKHR")
            .WithParameterName("pHandle")
            .InterpretAsPointerType(new BuiltinType(PrimitiveType.Void))
            .SetParameterKind(ParameterKind.Out);
        
        api.Function("vkGetPhysicalDeviceFeatures2KHR").
            WithParameterName("pFeatures").
            InterpretAsIs().
            SetParameterKind(ParameterKind.Ref);
        
        api.Function("vkGetPhysicalDeviceFeatures2").
            WithParameterName("pFeatures").
            InterpretAsIs().
            SetParameterKind(ParameterKind.Ref);

        // Ref rather than Out: the answer is not the top-level structure alone. Which scaling modes and which
        // compatible present modes a surface offers arrive in structures the CALLER chains onto pSurfaceCapabilities,
        // and an out parameter gives the caller no way to put them there.
        api.Function("vkGetPhysicalDeviceSurfaceCapabilities2KHR").
            WithParameterName("pSurfaceCapabilities").
            InterpretAsIs().
            SetParameterKind(ParameterKind.Ref);

        api.Function("vkGetInstanceProcAddr").WithReturnType(new PointerType() { Pointee = new BuiltinType(PrimitiveType.Void)});
        api.Function("vkGetDeviceProcAddr").WithReturnType(new PointerType() { Pointee = new BuiltinType(PrimitiveType.Void)});
        
        api.Function("vkCmdExecuteCommands")
            .WithParameterName("pCommandBuffers")
            .InterpretAsPointerToArray(new CustomType("VkCommandBuffer"), true, "commandBufferCount");
        
        api.Function("vkCreateShadersEXT")
            .WithParameterName("pCreateInfos")
            .InterpretAsPointerToArray(new CustomType("VkShaderCreateInfoEXT"), arraySizeSource: "createInfoCount")
            .WithParameterName("pShaders")
            .InterpretAsPointerToArray(new CustomType("VkShaderEXT"), arraySizeSource: "createInfoCount");

        api.Delegate("PFN_vkGetInstanceProcAddr").WithReturnType(new PointerType() {Pointee = new BuiltinType(PrimitiveType.Void)});
        api.Delegate("PFN_vkGetDeviceProcAddr").WithReturnType(new PointerType() {Pointee = new BuiltinType(PrimitiveType.Void)});
        
        api.Delegate("PFN_vkVoidFunction").WithReturnType(new PointerType() {Pointee = new BuiltinType(PrimitiveType.Void)});
        api.Delegate("PFN_vkDebugUtilsMessengerCallbackEXT")
            .WithParameterName("messageTypes")
            .ChangeType(new CustomType("VkDebugUtilsMessageTypeFlagBitsEXT"));
        
        var structsList = new List<string>
        {
            "VkInstance_T",
            "VkPhysicalDevice_T",
            "VkDevice_T",
            "VkQueue_T",
            "VkSemaphore_T",
            "VkCommandBuffer_T",
            "VkFence_T",
            "VkDeviceMemory_T",
            "VkBuffer_T",
            "VkImage_T",
            "VkEvent_T",
            "VkQueryPool_T",
            "VkBufferView_T",
            "VkImageView_T",
            "VkShaderModule_T",
            "VkPipelineCache_T",
            "VkPipelineLayout_T",
            "VkRenderPass_T",
            "VkPipeline_T",
            "VkDescriptorSetLayout_T",
            "VkSampler_T",
            "VkDescriptorPool_T",
            "VkDescriptorSet_T",
            "VkFramebuffer_T",
            "VkCommandPool_T",
            "VkSamplerYcbcrConversion_T",
            "VkDescriptorUpdateTemplate_T",
            "VkSurfaceKHR_T",
            "VkSwapchainKHR_T",
            "VkDisplayKHR_T",
            "VkDisplayModeKHR_T",
            "VkDescriptorUpdateTemplateKHR_T",
            "VkSamplerYcbcrConversionKHR_T",
            "VkDebugReportCallbackEXT_T",
            "VkObjectTableNVX_T",
            "VkIndirectCommandsLayoutNVX_T",
            "VkDebugUtilsMessengerEXT_T",
            "VkValidationCacheEXT_T",
            "VkAccelerationStructureNV_T"
        };

        api.Classes(structsList)
            .AddField("pointer")
            .SetType(new PointerType() { Pointee = new BuiltinType(PrimitiveType.Void) });

        var classesList = new List<string>()
        {
            "VkInstance",
            "VkPhysicalDevice",
            "VkDevice",
            "VkQueue",
            "VkSemaphore",
            "VkCommandBuffer",
            "VkFence",
            "VkDeviceMemory",
            "VkBuffer",
            "VkImage",
            "VkEvent",
            "VkQueryPool",
            "VkBufferView",
            "VkImageView",
            "VkShaderModule",
            "VkPipelineCache",
            "VkPipelineLayout",
            "VkRenderPass",
            "VkPipeline",
            "VkDescriptorSetLayout",
            "VkSampler",
            "VkDescriptorPool",
            "VkDescriptorSet",
            "VkFramebuffer",
            "VkCommandPool",
            "VkSamplerYcbcrConversion",
            "VkDescriptorUpdateTemplate",
            "VkSurfaceKHR",
            "VkSwapchainKHR",
            "VkDisplayKHR",
            "VkDisplayModeKHR",
            "VkDescriptorUpdateTemplateKHR",
            "VkSamplerYcbcrConversionKHR",
            "VkDebugReportCallbackEXT",
            "VkObjectTableNVX",
            "VkIndirectCommandsLayoutNVX",
            "VkDebugUtilsMessengerEXT",
            "VkValidationCacheEXT",
            "VkAccelerationStructureNV",
            "VkShaderEXT"
        };

        api.Classes(classesList)
            .AddProperty("NativePointer")
            .SetField("__Instance.pointer")
            .SetType(new PointerType() { Pointee = new BuiltinType(PrimitiveType.Void) })
            .SetGetter(new Method());

        var defaultParamFunctions = new[]
        {
            "vkDestroySurfaceKHR",
            "vkDestroyDebugReportCallbackEXT",
            "vkDestroyDebugUtilsMessengerEXT",
            "vkDestroyFence",
            "vkDestroySemaphore",
            "vkDestroyEvent",
            "vkDestroyQueryPool",
            "vkDestroyBuffer",
            "vkDestroyBufferView",
            "vkDestroyImage",
            "vkDestroyImageView",
            "vkDestroyShaderModule",
            "vkDestroyPipelineCache",
            "vkDestroyPipeline",
            "vkDestroyPipelineLayout",
            "vkDestroySampler",
            "vkDestroyDescriptorPool",
            "vkDestroyDescriptorSetLayout",
            "vkDestroyFramebuffer",
            "vkDestroyRenderPass",
            "vkDestroyCommandPool",
            "vkDestroySamplerYcbcrConversion",
            "vkDestroyDescriptorUpdateTemplate",
            "vkDestroySwapchainKHR",
            "vkDestroyDescriptorUpdateTemplateKHR",
            "vkDestroySamplerYcbcrConversionKHR",
            "vkDestroyIndirectCommandsLayoutNVX",
            "vkDestroyObjectTableNVX",
            "vkDestroyValidationCacheEXT",
            "vkDestroyAccelerationStructureNV",
            "vkFreeMemory",
            "vkDestroyShaderEXT"
        };

        api.Functions(defaultParamFunctions).WithParameterName("pAllocator").InterpretAsIs()
            .SetDefaultValue("null");

        api.Function("vkCmdSetVertexInputEXT")
            .WithParameterName("pVertexAttributeDescriptions")
            .InterpretAsPointerToArray(new CustomType("VkVertexInputAttributeDescription2EXT"),
                arraySizeSource: "vertexAttributeDescriptionCount");

        api.Function("vkMapMemory")
            .WithParameterName("ppData")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.Out);
        
        api.Function("vkAcquireNextImageKHR")
            .WithParameterName("pImageIndex")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.Ref);
        
        api.Function("vkGetPhysicalDeviceProperties2").
            WithParameterName("pProperties").
            InterpretAsIs().
            SetParameterKind(ParameterKind.Ref);
        
        api.Delegate("PFN_vkCreateShadersEXT").
            WithParameterName("pShaders").
            InterpretAsPointerToArray(new CustomType("VkShaderEXT_T")).
            SetParameterKind(ParameterKind.Ref);

        api.Delegate("PFN_vkGetDescriptorSetLayoutSizeEXT").
            WithParameterName("pLayoutSizeInBytes").
            InterpretAsIs().
            SetParameterKind(ParameterKind.Out);
        
        api.Delegate("PFN_vkGetDescriptorSetLayoutBindingOffsetEXT").
            WithParameterName("pOffset").
            InterpretAsIs().
            SetParameterKind(ParameterKind.Out);
        
        api.Delegate("PFN_vkCmdSetViewportWithCountEXT")
            .WithParameterName("pViewports")
            .InterpretAsPointerToArray(new CustomType("VkViewport"),
                arraySizeSource: "viewportCount");
        
        api.Delegate("PFN_vkCmdSetCullModeEXT")
                .WithParameterName("cullMode")
                .ChangeType(new CustomType("VkCullModeFlagBits"));
        
        api.Function("vkCmdSetCullModeEXT")
            .WithParameterName("cullMode")
            .ChangeType(new CustomType("VkCullModeFlagBits"));

        api.Delegate("PFN_vkCmdSetColorWriteMaskEXT")
            .WithParameterName("pColorWriteMasks")
            .ChangeType(new CustomType("VkColorComponentFlagBits"))
            .WithParameterName("pColorWriteMasks")
            .InterpretAsPointerToArray(new CustomType("VkColorComponentFlagBits"), arraySizeSource: "attachmentCount");
        
        api.Function("vkCmdSetColorWriteMaskEXT")
            .WithParameterName("pColorWriteMasks")
            .InterpretAsPointerToArray(new CustomType("VkColorComponentFlagBits"),
                arraySizeSource: "attachmentCount");

        api.Delegate("PFN_vkCmdSetDescriptorBufferOffsetsEXT").WithParameterName("pOffsets")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.UInt64));
        
        api.Enum("VkShaderStageFlagBits").SetIsFlagsEnum(true);
                
        api.Class("VkDeviceQueueCreateInfo")
            .WithField("pQueuePriorities")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.Float), arraySizeSource: "queueCount");
        
        api.Class("VkWin32SurfaceCreateInfoKHR")
            .WithField("hinstance")
            .InterpretAs(new BuiltinType(PrimitiveType.IntPtr))
            .WithField("hwnd")
            .InterpretAs(new BuiltinType(PrimitiveType.IntPtr));

        api.Class("VkWaylandSurfaceCreateInfoKHR")
            .WithField("display")
            .InterpretAs(new BuiltinType(PrimitiveType.IntPtr))
            .WithField("surface")
            .InterpretAs(new BuiltinType(PrimitiveType.IntPtr));

        // The display goes in: read as a void*, it would come back out.
        api.Function("vkGetPhysicalDeviceWaylandPresentationSupportKHR")
            .WithParameterName("display")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.In);

        api.Class("StdVideoH265HrdParameters")
            .SaveInteropSource(true)
            .WithField("pSubLayerHrdParametersNal")
            .InterpretAsPointerToArray(new CustomType("StdVideoH265SubLayerHrdParameters"))
            .WithField("pSubLayerHrdParametersVcl")
            .InterpretAsPointerToArray(new CustomType("StdVideoH265SubLayerHrdParameters"));
        
        api.Class("VkHostAddressRangeEXT")
            .WithField("address")
            .InterpretAsPointerType(new BuiltinType(PrimitiveType.Void));

        api.Class("VkHostAddressRangeConstEXT")
            .WithField("address")
            .InterpretAsPointerType(new BuiltinType(PrimitiveType.Void));

        api.Function("vkGetDescriptorEXT")
            .WithParameterName("pDescriptor")
            .InterpretAsPointerToVoid()
            .SetParameterKind(ParameterKind.In);
        
        api.Function("vkGetMemoryWin32HandleNV")
            .WithParameterName("pHandle")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.Out);
        
        api.Function("vkGetSemaphoreWin32HandleKHR")
            .WithParameterName("pHandle")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.Out);
        
        api.Function("vkGetFenceWin32HandleKHR")
            .WithParameterName("pHandle")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.Out);

        api.Class("VkDeviceFaultInfoEXT")
            .SaveInteropSource(true)
            .WithField("pAddressInfos")
            .InterpretAsPointerToArray(new CustomType("VkDeviceFaultAddressInfoKHR"))
            .WithField("pVendorInfos")
            .InterpretAsPointerToArray(new CustomType("VkDeviceFaultVendorInfoKHR"));
        
        // SPIRV tools, SPIRV reflection
        var spirvToolsStructsList = new List<string>
        {
            "spv_binary_t",
            "spv_const_binary_t",
            "spv_optimizer_t"
        };

        api.Classes(spirvToolsStructsList)
            .AddField("pointer")
            .SetType(new PointerType() { Pointee = new BuiltinType(PrimitiveType.Void) });

        api.Wrappers().WithField("pNext").InterpretAsPointerToPrimitiveType(PrimitiveType.Object);
        
        api.Function("shaderc_result_get_bytes").WithReturnType(new BuiltinType(PrimitiveType.IntPtr));

        api.Function("spvc_context_parse_spirv").WithParameterName("spirv")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.Byte));

        api.Function("spvc_resources_get_resource_list_for_type")
            .WithParameterName("resource_list")
            .InterpretAsPointerToArray(new CustomType("SpvcReflectedResource"), true, "resource_size", pointerDepth: 2)
            .SetParameterKind(ParameterKind.Out)
            .WithParameterName("resource_size")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.Out);

        api.Delegate("spvc_error_callback")
            .WithParameterName("error")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.Out);

        api.Function("spvc_constant_get_type").RenameTo("spvc_constant_get_constant_type");

        api.Class("spv_parsed_instruction_t").WithField("operands")
            .InterpretAsPointerToArray(new CustomType("spv_parsed_operand_t"));

        api.Class("spv_text_t").Ignore();
        api.Class("spv_text")
            .CleanObject()
            .CopyFieldsFromLinkedObject()
            .SetClassType(ClassType.Struct);
        
        api.Class("spv_diagnostic_t").Ignore();
        api.Class("spv_diagnostic")
            .CleanObject()
            .CopyFieldsFromLinkedObject()
            .SetClassType(ClassType.Struct);

        api.CreateClass("libspirv", "spv_optimizer", ClassType.Class, "spv_optimizer_t")
            .AddField("__Instance")
            .SetType(new CustomType("spv_optimizer_t"))
            .ChangeType()
            .Return()
            .AddOverloadOperator(OperatorKind.Implicit, TransformationKind.FromClassToValue, "__Instance", false)
            .AddOverloadOperator(OperatorKind.Implicit, TransformationKind.FromValueToClass, "__Instance", true)
            .AddDefaultConstructor()
            .AddConstructorWithParameters<Class>("__Instance", ParameterKind.In, new CustomType("spv_optimizer_t"))
            .AddProperty("NativePointer")
            .SetField("__Instance.pointer")
            .SetType(new PointerType() { Pointee = new BuiltinType(PrimitiveType.Void) })
            .SetGetter(new Method());
            
        api.Class("spv_optimizer_t").UpdateLinkedClass("spv_optimizer");
        
        api.Function("spvBinaryToText")
            .WithParameterName("binary")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.Byte))
            .WithParameterName("text")
            .InterpretAsPointerType(new CustomType("spv_text"), 2)
            .SetParameterKind(ParameterKind.Out)
            .WithParameterName("diagnostic")
            .InterpretAsPointerType(new CustomType("spv_diagnostic"), 2)
            .SetParameterKind(ParameterKind.Out);
        
        api.Function("spvc_compiler_get_active_buffer_ranges")
            .WithParameterName("ranges")
            .InterpretAsPointerToArray(new CustomType("spvc_buffer_range"), pointerDepth: 2, arraySizeSource: "num_ranges")
            .SetParameterKind(ParameterKind.Out);

        api.Function("spvBinaryParse")
            .WithParameterName("user_data")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.In);
        
        api.Function("spvc_compiler_get_declared_extensions")
            .WithParameterName("extensions")
            .InterpretAsPointerToArray(new BuiltinType(PrimitiveType.Sbyte), pointerDepth: 3, arraySizeSource: "num_extensions", isConst:true)
            .SetParameterKind(ParameterKind.Out);
        
        api.Function("spvOptimizerDestroy")
            .WithParameterName("optimizer")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.In);

        api.Function("spvc_constant_get_subconstants")
            .WithParameterName("constituents")
            .InterpretAsPointerToArray(new CustomType("SpvcConstantId"), pointerDepth: 2, arraySizeSource: "count")
            .SetParameterKind(ParameterKind.Out)
            .WithParameterName("count")
            .InterpretAsIs()
            .SetParameterKind(ParameterKind.Ref);

        // SLANG API
        api.Function("slangc_session_create")
            .WithParameterName("options")
            .InterpretAsPointerToArray(new CustomType("SlangcCompilerOption"), pointerDepth: 1, arraySizeSource: "optionCount");

        // macros 
        api.Macro("VK_STD_VULKAN_VIDEO_CODEC_VP9_DECODE_SPEC_VERSION").SetPrimitiveType(PrimitiveType.UInt32);
        api.Macro("VK_STD_VULKAN_VIDEO_CODEC_AV1_DECODE_SPEC_VERSION").SetPrimitiveType(PrimitiveType.UInt32);
        api.Macro("VK_STD_VULKAN_VIDEO_CODEC_AV1_ENCODE_SPEC_VERSION").SetPrimitiveType(PrimitiveType.UInt32);
        api.Macro("VK_STD_VULKAN_VIDEO_CODEC_H264_ENCODE_SPEC_VERSION").SetPrimitiveType(PrimitiveType.UInt32);
        api.Macro("VK_STD_VULKAN_VIDEO_CODEC_H264_DECODE_SPEC_VERSION").SetPrimitiveType(PrimitiveType.UInt32);
        api.Macro("VK_STD_VULKAN_VIDEO_CODEC_H265_ENCODE_SPEC_VERSION").SetPrimitiveType(PrimitiveType.UInt32);
        api.Macro("VK_STD_VULKAN_VIDEO_CODEC_H265_DECODE_SPEC_VERSION").SetPrimitiveType(PrimitiveType.UInt32);
        
        var fixingFunctionParameters = new PostProcessingApiPass(api);
        ctx.AddPreGeneratorPass(fixingFunctionParameters, ExecutionPassKind.PerTranslationUnit);
    }
}
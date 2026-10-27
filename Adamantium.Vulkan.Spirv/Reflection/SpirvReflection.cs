using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Vulkan.Spirv.Cross;
using QuantumBinding.Utils;

namespace Adamantium.Vulkan.Spirv.Reflection
{
    public class SpirvReflection : QBDisposableObject
    {
        private SpirvContext context;
        private SpirvCompiler compiler;
        private SpirvParsedIr parsedIr;
        private SpirvResources resources;
        private SpirvSet set;
        private Result lastResult;

        private static readonly List<ResourceType> resourcesToReflect;

        private byte[] bytecode;
        private Backend reflectionBackend;

        static SpirvReflection()
        {
            resourcesToReflect = new List<ResourceType>
            {
                ResourceType.UniformBuffer,
                ResourceType.SeparateSamplers,
                ResourceType.SeparateImage,
                ResourceType.SampledImage,
                ResourceType.StorageImage,
                ResourceType.StorageBuffer,
                ResourceType.AccelerationStructure,
                ResourceType.RayQuery
            };
        }

        public SpirvReflection(byte[] bytecode, Backend reflectionBackend = Backend.Hlsl)
        {
            if (bytecode == null || bytecode.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytecode), "bytecode array should not be null or empty");
            }
            
            lastResult = SpirvContext.Create(out context);
            SpirvResultHelper.CheckResult(lastResult, "SpvcContext::Create");

            this.bytecode = bytecode;
            this.reflectionBackend = reflectionBackend;
        }
        
        /// <summary>The resources the SPIR-V declares, each where it is bound, as it binds it: the bytecode is read, never
        /// changed.</summary>
        public SpirvReflectionResult Disassemble()
        {
            lastResult = context.ParseSpirv(bytecode, (ulong)bytecode.Length / 4, out parsedIr);
            SpirvResultHelper.CheckResult(lastResult, "SpvcContext::ParseSpirv");
            lastResult = context.CreateCompiler(reflectionBackend, parsedIr, CaptureMode.TakeOwnership, out compiler);
            SpirvResultHelper.CheckResult(lastResult, "SpvcContext::CreateCompiler");
            lastResult = compiler.GetActiveInterfaceVariables(out set);
            SpirvResultHelper.CheckResult(lastResult, "SpvcCompiler::GetActiveInterfaceVariables");
            lastResult = compiler.CreateShaderResourcesForActiveVariables(out resources, set);

            var disassembleResult = new SpirvReflectionResult();

            foreach (var resourceType in resourcesToReflect)
            {
                lastResult = resources.GetResourceListForType(resourceType, out var shaderResources, out var size);

                for (ulong i = 0; i < (ulong)shaderResources.Length; ++i)
                {
                    var shaderResource = new ShaderReflectionResource();
                    var resourceDescription = new ShaderReflectionVariable(resourceType);
                    shaderResource.Description = resourceDescription;
                    
                    resourceDescription.Name = shaderResources[i].Name;
                    // SPIRV-Cross quirk: for buffer BLOCKS, SpvcReflectedResource.Name is the externally-visible BLOCK
                    // (type) name, not the shader VARIABLE name. For a UniformBuffer (cbuffer Foo) the block name IS the
                    // wanted name, but for a StorageBuffer (StructuredBuffer<T> Instances) the block name is the generated
                    // type wrapper ("type.StructuredBuffer"/"StructuredBuffer") - the real variable name ("Instances") is
                    // only reachable via get_name(id). Prefer the instance name for storage buffers (fall back to the
                    // block name if the variable is anonymous).
                    if (resourceType == ResourceType.StorageBuffer)
                    {
                        var instanceName = compiler.GetName(shaderResources[i].Id);
                        if (!string.IsNullOrEmpty(instanceName))
                        {
                            resourceDescription.Name = instanceName;
                        }
                    }
                    resourceDescription.TypeId = shaderResources[i].Id;
                    var spvcType = compiler.GetTypeHandle(shaderResources[i].Base_type_id);
                    resourceDescription.Type = spvcType.GetBasetype();

                    resourceDescription.DescriptorSet = compiler.GetDecoration(shaderResources[i].Id, Decoration.DescriptorSet);
                    resourceDescription.SlotIndex = compiler.GetDecoration(shaderResources[i].Id, Decoration.Binding);
                    resourceDescription.Access = GetAccess(resourceType, shaderResources[i].Id);

                    ulong tmpSize = 0;
                    lastResult = compiler.GetDeclaredStructSize(spvcType, ref tmpSize);
                    resourceDescription.Size = tmpSize;

                    resourceDescription.RowCount = spvcType.GetColumns();
                    // The base type is the element: an array of resources is told by the variable's own type, a size of
                    // 0 meaning one of no length.
                    var arrayType = compiler.GetTypeHandle(shaderResources[i].Type_id);
                    resourceDescription.ArrayDimensionsCount = arrayType.GetNumArrayDimensions();
                    for (var x = 0u; x < resourceDescription.ArrayDimensionsCount; ++x)
                    {
                        resourceDescription.AddArraySizeForDimension(x, arrayType.GetArrayDimension(x));
                    }

                    resourceDescription.Dimension = (ShaderResourceDimension)spvcType.GetImageDimension();

                    var membersCount = spvcType.GetNumMemberTypes();
                    for (uint k = 0; k < membersCount; ++k)
                    {
                        var memberType = spvcType.GetMemberType(k);
                        var member = new ShaderReflectionVariable(ResourceType.UniformBuffer);

                        member.TypeId = memberType;
                        var memberTypeHandle = compiler.GetTypeHandle(memberType);
                        var isPointer = memberTypeHandle.GetStorageClass() == StorageClass.PhysicalStorageBuffer;

                        // Slang wraps an ARRAY inside a std140 uniform block in a synthetic single-member struct
                        // ("_Array_std140_matrix<float,4,4>64"), which is how it pins the element padding std140 asks
                        // for - but it is not what the member IS. Left alone, an array of matrices reflects as an
                        // opaque struct: no row/column layout, no element count, and a zero element stride, so the
                        // matrix copy was never wired up and writing such a parameter died on a null delegate.
                        // The layout facts then live on the WRAPPER's only member, not on the block's.
                        var layoutType = spvcType;
                        var layoutIndex = k;
                        var layoutTypeId = shaderResources[i].Base_type_id;

                        if (!isPointer && memberTypeHandle.GetBasetype() == Basetype.Struct &&
                            memberTypeHandle.GetNumMemberTypes() == 1)
                        {
                            var innerType = memberTypeHandle.GetMemberType(0);
                            var innerHandle = compiler.GetTypeHandle(innerType);

                            if (innerHandle.GetNumArrayDimensions() > 0)
                            {
                                layoutType = memberTypeHandle;
                                layoutIndex = 0;
                                layoutTypeId = memberType;
                                memberTypeHandle = innerHandle;
                            }
                        }

                        member.Type = isPointer ? Basetype.Uint64 : memberTypeHandle.GetBasetype();
                        ulong typeSize = 0;
                        // Size and offset describe where the member sits IN THE BLOCK, so they stay on the block.
                        lastResult = compiler.GetDeclaredStructMemberSize(spvcType, k, ref typeSize);
                        member.Size = typeSize;
                        uint offset = 0;
                        lastResult = compiler.TypeStructMemberOffset(spvcType, k, ref offset);
                        member.Offset = offset;
                        member.Name = compiler.GetMemberName(shaderResources[i].Base_type_id, k);
                        uint stride = 0;
                        // The strides describe the ELEMENTS, so they come from wherever the array actually is.
                        lastResult = compiler.TypeStructMemberArrayStride(layoutType, layoutIndex, ref stride);
                        member.ArrayStride = stride;
                        lastResult = compiler.TypeStructMemberMatrixStride(layoutType, layoutIndex, ref stride);
                        member.MatrixStride = stride;

                        //Get the number of rows in the type
                        member.RowCount = isPointer ? 1 : memberTypeHandle.GetVectorSize();
                        //Get the number of columns in the type
                        member.ColumnCount = isPointer ? 1 : memberTypeHandle.GetColumns();
                        member.ArrayDimensionsCount = memberTypeHandle.GetNumArrayDimensions();

                        if (isPointer)
                        {
                            member.VariableType = ShaderVariableClass.Pointer;
                        }
                        else if (member.RowCount == 1 && member.ColumnCount == 1)
                        {
                            member.VariableType = ShaderVariableClass.Scalar;
                        }
                        else if (member.RowCount > 1 && member.ColumnCount == 1)
                        {
                            member.VariableType = ShaderVariableClass.Vector;
                        }
                        else
                        {
                            // HasMemberDecoration takes the member INDEX (k), not the byte offset. Passing the offset
                            // only happened to work while a matrix was the first member (offset 0 == index 0); once a
                            // member precedes it (e.g. a uint64 BDA address), offset != index, the row/col-major lookup
                            // failed and VariableType defaulted to Scalar -> CopyMatrix was never wired -> NRE on SetValue.
                            if (compiler.HasMemberDecoration(layoutTypeId, layoutIndex, Decoration.RowMajor))
                            {
                                member.VariableType = ShaderVariableClass.MatrixRows;
                            }
                            else if (compiler.HasMemberDecoration(layoutTypeId, layoutIndex, Decoration.ColMajor))
                            {
                                member.VariableType = ShaderVariableClass.MatrixColumns;
                            }
                        }

                        if (!isPointer && member.Type == Basetype.Struct)
                        {
                            member.VariableType = ShaderVariableClass.Struct;
                        }
                        
                        // The MEMBER's dimensions, not the block's - the block is not an array, so this read its own
                        // (empty) dimensions and every element count came back as zero.
                        for (var x = 0u; x < member.ArrayDimensionsCount; ++x)
                        {
                            member.AddArraySizeForDimension(x, memberTypeHandle.GetArrayDimension(x));
                        }

                        shaderResource.AddVariable(member);
                    }

                    disassembleResult.AddShaderResource(shaderResource);
                }
            }

            disassembleResult.InputPatchSize = GetInputPatchSize();
            return disassembleResult;
        }

        private uint GetInputPatchSize()
        {
            if (compiler.GetExecutionModel() != ExecutionModel.TessellationControl)
            {
                return 0;
            }

            const uint opConstant = 43, opTypeArray = 28, opTypePointer = 32, opVariable = 59, storageInput = 1;
            var words = MemoryMarshal.Cast<byte, uint>(bytecode);
            var constants = new Dictionary<uint, uint>();
            var arrayLengths = new Dictionary<uint, uint>();
            var inputPointees = new Dictionary<uint, uint>();
            for (var at = 5; at < words.Length;)
            {
                var length = (int)(words[at] >> 16);
                if (length == 0)
                {
                    break;
                }

                switch (words[at] & 0xFFFF)
                {
                    case opConstant:
                        constants[words[at + 2]] = words[at + 3];
                        break;
                    case opTypeArray:
                        arrayLengths[words[at + 1]] = words[at + 3];
                        break;
                    case opTypePointer when words[at + 2] == storageInput:
                        inputPointees[words[at + 1]] = words[at + 3];
                        break;
                    case opVariable when words[at + 3] == storageInput
                                         && inputPointees.TryGetValue(words[at + 1], out var pointee)
                                         && arrayLengths.TryGetValue(pointee, out var lengthId)
                                         && constants.TryGetValue(lengthId, out var size):
                        return size;
                }

                at += length;
            }

            return 0;
        }

        private ShaderResourceAccess GetAccess(ResourceType resourceType, uint variableId)
        {
            if (resourceType is not (ResourceType.StorageBuffer or ResourceType.StorageImage))
            {
                return ShaderResourceAccess.ReadOnly;
            }

            if (compiler.HasDecoration(variableId, Decoration.NonWritable))
            {
                return ShaderResourceAccess.ReadOnly;
            }

            return compiler.HasDecoration(variableId, Decoration.NonReadable)
                ? ShaderResourceAccess.WriteOnly
                : ShaderResourceAccess.ReadWrite;
        }

        protected override void UnmanagedDisposeOverride()
        {
            context?.ReleaseAllocations();
            context?.Destroy();
        }
    }
}

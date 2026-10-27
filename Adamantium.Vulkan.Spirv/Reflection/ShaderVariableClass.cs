namespace Adamantium.Vulkan.Spirv.Reflection
{
    /// <summary>What kind of value a shader variable holds.</summary>
    public enum ShaderVariableClass : byte
    {
        /// <summary>A scalar.</summary>
        Scalar = 0,

        /// <summary>A vector.</summary>
        Vector = 1,

        /// <summary>A matrix stored row by row.</summary>
        MatrixRows = 2,

        /// <summary>A matrix stored column by column.</summary>
        MatrixColumns = 3,

        /// <summary>An object: a texture, a sampler, a buffer.</summary>
        Object = 4,

        /// <summary>A structure.</summary>
        Struct = 5,

        /// <summary>A class.</summary>
        InterfaceClass = 6,

        /// <summary>A pointer to an interface.</summary>
        InterfacePointer = 7,

        /// <summary>A pointer - <c>T*</c> in Slang: the 64-bit device address of data of the type it points to.</summary>
        Pointer = 8,
    }
}

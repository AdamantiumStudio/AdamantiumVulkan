using System;
using QuantumBinding.Generator;
using QuantumBinding.Generator.AST;
using QuantumBinding.Generator.Types;

namespace Adamantium.Vulkan.Generator;

public partial class VulkanBindings
{
    public class MacroFunctions
    {
        public static MacroFunction CreateMakeApiVersionFunction()
        {
            var function = new MacroFunction();
            var variant = new Parameter()
                { Type = new BuiltinType(PrimitiveType.UInt32), Name = "variant", ParameterKind = ParameterKind.In };
            var major = new Parameter()
                { Type = new BuiltinType(PrimitiveType.UInt32), Name = "major", ParameterKind = ParameterKind.In };
            var minor = new Parameter()
                { Type = new BuiltinType(PrimitiveType.UInt32), Name = "minor", ParameterKind = ParameterKind.In };
            var patch = new Parameter()
                { Type = new BuiltinType(PrimitiveType.UInt32), Name = "patch", ParameterKind = ParameterKind.In };
            function.Parameters.Add(variant);
            function.Parameters.Add(major);
            function.Parameters.Add(minor);
            function.Parameters.Add(patch);
            function.FunctionBody =
                $"var version = (((variant) << 29) | ((major) << 22) | ((minor) << 12) | ((uint)(patch)));{Environment.NewLine}return (uint)version;";
            function.ReturnType = new BuiltinType(PrimitiveType.UInt32);

            return function;
        }

        public static MacroFunction CreateMakeProfileVersionFunction(byte major, byte minor, byte patch)
        {
            var function = new MacroFunction();
            function.FunctionBody =
                $"({major} << 22 | {minor} << 12 | {patch})";
            function.ApplyStrategy = MacroFunctionStrategy.ApplyOnlyFunctionBody;

            return function;
        }

        public static MacroFunction CreateApiVersion()
        {
            var function = new MacroFunction();
            function.ReturnType = new BuiltinType(PrimitiveType.UInt32);
            function.ApplyStrategy = MacroFunctionStrategy.ApplyOnlyReturnType;
            return function;
        }

        public static MacroFunction CreateSpirvMSLVersion()
        {
            var function = new MacroFunction();
            var major = new Parameter()
                { Type = new BuiltinType(PrimitiveType.Byte), Name = "major", ParameterKind = ParameterKind.In };
            var minor = new Parameter()
                { Type = new BuiltinType(PrimitiveType.Byte), Name = "minor", ParameterKind = ParameterKind.In };
            var patch = new Parameter()
                { Type = new BuiltinType(PrimitiveType.Byte), Name = "patch", ParameterKind = ParameterKind.In };
            function.Parameters.Add(major);
            function.Parameters.Add(minor);
            function.Parameters.Add(patch);
            function.FunctionBody =
                $"var version = major * 10000 + minor * 100 + patch;{Environment.NewLine}return (uint)version;";
            function.ReturnType = new BuiltinType(PrimitiveType.UInt32);
            return function;
        }

        /// <summary>VK_API_VERSION_VARIANT/MAJOR/MINOR/PATCH: that part of a packed version, as vulkan_core.h takes it out.
        /// </summary>
        public static MacroFunction CreateApiVersionPart(string part)
        {
            var function = new MacroFunction();
            function.Parameters.Add(new Parameter
                { Type = new BuiltinType(PrimitiveType.UInt32), Name = "version", ParameterKind = ParameterKind.In });
            function.FunctionBody = part switch
            {
                "variant" => "return version >> 29;",
                "major" => "return (version >> 22) & 0x7FU;",
                "minor" => "return (version >> 12) & 0x3FFU;",
                "patch" => "return version & 0xFFFU;",
                _ => throw new ArgumentOutOfRangeException(nameof(part), part, "a part of a Vulkan version")
            };
            function.ReturnType = new BuiltinType(PrimitiveType.UInt32);

            return function;
        }

        public static MacroFunction CreateHeaderVersion()
        {
            var function = new MacroFunction();
            function.ReturnType = new BuiltinType(PrimitiveType.UInt32);
            function.ApplyStrategy = MacroFunctionStrategy.ApplyOnlyReturnType;
            return function;
        }

        public static MacroFunction CreateLodClampNone()
        {
            var function = new MacroFunction();
            function.ReturnType = new BuiltinType(PrimitiveType.Float);
            function.ApplyStrategy = MacroFunctionStrategy.ApplyOnlyReturnType;
            return function;
        }

        public static MacroFunction CreateStringReturnMacro()
        {
            var func = new MacroFunction();
            func.ReturnType = new BuiltinType(PrimitiveType.String);
            func.ApplyStrategy = MacroFunctionStrategy.ApplyOnlyReturnType;
            return func;
        }

        public static MacroFunction CreateMakeVideoStdVersion()
        {
            var function = new MacroFunction();
            var major = new Parameter()
                { Type = new BuiltinType(PrimitiveType.Byte), Name = "major", ParameterKind = ParameterKind.In };
            var minor = new Parameter()
                { Type = new BuiltinType(PrimitiveType.Byte), Name = "minor", ParameterKind = ParameterKind.In };
            var patch = new Parameter()
                { Type = new BuiltinType(PrimitiveType.Byte), Name = "patch", ParameterKind = ParameterKind.In };
            function.Parameters.Add(major);
            function.Parameters.Add(minor);
            function.Parameters.Add(patch);
            function.FunctionBody =
                $"var version = ((((uint)(major)) << 22) | (((uint)(minor)) << 12) | ((uint)(patch)));{Environment.NewLine}return (uint)version;";
            function.ReturnType = new BuiltinType(PrimitiveType.UInt32);

            return function;
        }
    }
}
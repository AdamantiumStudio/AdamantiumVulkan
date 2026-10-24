using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Adamantium.Vulkan.Generator.Extensions;
using Adamantium.Vulkan.Generator.Parser.Schema;

namespace Adamantium.Vulkan.Generator.Parser;

public static class ParserExtensions
{
    public static bool IsCompileTimeConstant(this VulkanRegistry registry, string expression)
    {
        // Find all worlds (words) that are not numbers
        var tokens = Regex.Matches(expression, @"[a-zA-Z_][a-zA-Z0-9_]*");
    
        foreach (Match match in tokens)
        {
            string word = match.Value;
            // If this is not a number and it is not in the constant dictionary - it is a dynamic field (runtime)
            if (!registry.Constants.ContainsKey(word))
                return false;
        }
        return true;
    }
    
    public static Int128 ResolveConstant(this VulkanRegistry registry, string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        
        ReadOnlySpan<char> nameSpan = name.AsSpan();

        if (TryParseVulkanNumeric(nameSpan, out var result))
        {
            return result;
        }

        if (registry.Constants.TryGetValue(name, out var constant))
            return unchecked((ulong)constant.NumericValue);

        throw new Exception($"Cannot find value for constant : {name}");
    }
    
    public static Int128 ResolveExpression(this VulkanRegistry registry, string expression)
    {
        if (string.IsNullOrEmpty(expression)) return 0;

        // If it's just a number or a constant name without any operators'
        if (!expression.Any(c => "+-*/".Contains(c)))
        {
            return registry.ResolveConstant(expression.Trim());
        }

        if (expression.Contains('*'))
        {
            var parts = expression.Split('*');
            return registry.ResolveExpression(parts[0]) * registry.ResolveExpression(parts[1]);
        }
    
        if (expression.Contains('/'))
        {
            var parts = expression.Split('/');
            var divisor = registry.ResolveExpression(parts[1]);
            return divisor == 0 ? 0 : registry.ResolveExpression(parts[0]) / divisor;
        }

        return registry.ResolveConstant(expression.Trim());
    }
    
    public static bool TryParseVulkanNumeric(this ReadOnlySpan<char> input, out Int128 result)
    {
        result = 0;
        if (input.IsEmpty) return false;

        var cleanInput = input.Trim("() ".AsSpan());
        if (cleanInput.IsEmpty) return false;

        if (cleanInput.StartsWith("~".AsSpan()))
        {
            result = ulong.MaxValue; // 0xFFFFFFFFFFFFFFFF
            return true;
        }

        bool isNegative = false;
        if (cleanInput.StartsWith("-".AsSpan()))
        {
            isNegative = true;
            cleanInput = cleanInput.Slice(1);
        }

        // In hex F is a digit, not the float suffix: trimming it made 0x7FFFFFFF read as 7 and 0x0000001F as 1.
        if (cleanInput.StartsWith("0x".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            var digits = cleanInput.Slice(2).TrimEnd("uUlL".AsSpan());
            if (Int128.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexResult))
            {
                result = isNegative ? -hexResult : hexResult;
                return true;
            }
            return false;
        }

        // A decimal is read with its sign; only its type suffix goes.
        var number = input.Trim("() ".AsSpan()).TrimEnd("uUlLfF".AsSpan());
        return Int128.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }
    
    public static Int128 ParseVulkanNumeric(this string rawValue)
    {
        if (rawValue.AsSpan().TryParseVulkanNumeric(out var result))
        {
            return result;
        }
        return 0;
    }
    
    public static string GetMasterName(this VulkanRegistry registry, IEnumerable<string> aliases)
    {
        return aliases
            .OrderBy(name => GetWeight(registry, name))
            .ThenBy(name => name.Length)
            .ThenBy(name => name)
            .First();
    }

    public static int GetWeight(this VulkanRegistry registry, string name)
    {
        bool hasImplementation = registry.Structs.ContainsKey(name) ||
                                 registry.Unions.ContainsKey(name) ||
                                 registry.Commands.ContainsKey(name) ||
                                 registry.Enums.ContainsKey(name) ||
                                 registry.Handles.ContainsKey(name) ||
                                 registry.Typedefs.ContainsKey(name);

        if (!hasImplementation) return 1000;
        
        if (HasVendorSuffix(registry, name))
        {
            if (name.EndsWith("KHR")) return 1;
            if (name.EndsWith("EXT")) return 2;
            
            return 3;
        }

        if (name.EndsWith("KHR")) return 1;
        if (name.EndsWith("EXT")) return 2;

        return 0;
    }

    public static bool HasVendorSuffix(this VulkanRegistry registry, string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        foreach (var tag in registry.VendorTags)
        {
            if (name.EndsWith(tag))
            {
                int tagIndex = name.Length - tag.Length;
                if (tagIndex > 0)
                {
                    char charBeforeTag = name[tagIndex - 1];
                    if (char.IsLower(charBeforeTag)) continue; 
                }
            
                return true;
            }
        }
        return false;
    }
    
    public static bool IsVulkanProp(this XElement node)
    {
        var api = node.Attribute("api")?.Value;
        return api == null || api.Split(',').Any(s => s.Trim() == "vulkan");
    }
    
    public static bool IsTypeAllowed(this VulkanRegistry registry, VulkanParserSettings settings, HashSet<string> allowedPlatforms, string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        
        if (!settings.IncludeVideoExtensions)
        {
            if (registry.ForbiddenVideoTypes.Contains(name)) 
                return false;

            if (registry.TypeToExtensionMap.TryGetValue(name, out var ext_Name))
            {
                if (registry.VideoExtensions.Contains(ext_Name))
                    return false;
            }
        }
        
        if (registry.ExtensionPlatformMap.TryGetValue(name, out var platform))
        {
            if (!allowedPlatforms.Contains(platform))
                return false;
        }
        
        return true;
    }

    public static bool IsVideoType(this string name)
    {
        if (string.IsNullOrEmpty(name)) 
            return false;

        return name.StartsWith("StdVideo") || name.StartsWith("vk_video") || name.StartsWith("VkVideo");
    }
    
    public static string SanitizeLengthExpression(string expression)
    {
        if (string.IsNullOrEmpty(expression)) return "";
        string sanitized = expression.Replace("->", ".");
        return sanitized;
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Adamantium.Vulkan.Generator.CodeGeneration;
using Adamantium.Vulkan.Generator.Extensions;
using Adamantium.Vulkan.Generator.Parser.Schema;
using QuantumBinding.Generator;
using QuantumBinding.Generator.AST;
using QuantumBinding.Generator.Parser;
using QuantumBinding.Generator.Types;
using Delegate = QuantumBinding.Generator.AST.Delegate;

namespace Adamantium.Vulkan.Generator.Parser;

public class VulkanXmlParser : IMetadataProvider
{
    private readonly VulkanRegistry _registry = new();

    private readonly HashSet<string> _allowedPlatforms;
    private readonly IReadOnlyList<NamespaceMapping> _namespaceMappings;
    private TranslationUnit _translationUnit;
    private readonly bool _includeVideoExtensions;
    private readonly string _videoFilePath;
    private readonly VulkanParserSettings _settings;
    private readonly string _commonPlatformName = "core";
    private string _currentFileName;
    private HashSet<string> _requiredNames;

    public VulkanXmlParser(VulkanParserSettings settings)
    {
        _allowedPlatforms = new HashSet<string>(settings.AllowedPlatforms, StringComparer.OrdinalIgnoreCase);
        _allowedPlatforms.Add(_commonPlatformName);
        _namespaceMappings = settings.NamespaceMappings;
        _includeVideoExtensions = settings.IncludeVideoExtensions;
        _videoFilePath = settings.VideoExtensionsPath;
        _settings = settings;
    }

    public ParseResult Parse(TranslationUnit unit, string vulkanFile)
    {
        _translationUnit = unit;

        IEnumerable<string> sources = [vulkanFile];
        if (_includeVideoExtensions)
        {
            sources = [vulkanFile, _videoFilePath];
        }

        foreach (var source in sources)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException("File not found", source);

            _currentFileName = source;
            var doc = XDocument.Load(source, LoadOptions.SetLineInfo);
            ParseRegistry(doc);
        }

        CollapseToMasterNames();
        FilterUnsupportedTypes();
        MapModelsToDeclarations();

        return ParseResult.Success;
    }

    private void ParseRegistry(XDocument doc)
    {
        var rawNodes = doc.Descendants("type")
            .GroupBy(x => x.Attribute("category")?.Value)
            .ToDictionary(g => g.Key ?? "None", g => g.ToList());
        
        List<XElement> GetNodes(string category) => 
            rawNodes.TryGetValue(category, out var nodes) ? nodes : new List<XElement>();

        ParseVendorTags(doc);
        ParseExtensions(doc);
        BuildAllowList(doc);
        ParseBaseTypes(GetNodes("basetype"));
        ParseAllConstants(doc);
        BuildAliasGroups(doc);
        IdentifyForbiddenVideoTypes(doc);
        IdentifyVideoExtensions(doc);
        ParseEnums(doc);
        ParseExtensionEnums(doc);
        ParseFeatureEnums(doc);
        ParseEnumAliases(doc);
        ParseBitmasks(doc);
        ParseHandles(GetNodes("handle"));
        ParseStructs(GetNodes("struct"), false);
        ParseStructs(GetNodes("union"), true);
        ParseCommands(doc);
        ParseDelegates(doc);
    }

    private void IdentifyForbiddenVideoTypes(XDocument doc)
    {
        var videoNodes = doc.Descendants("type")
            .Where(t =>
            {
                var requires = t.Attribute("requires")?.Value;
                return requires != null && requires.StartsWith("vk_video/");
            });

        foreach (var node in videoNodes)
        {
            string name = node.Attribute("name")?.Value ?? node.Element("name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                _registry.ForbiddenVideoTypes.Add(name);
            }
        }
    }

    private void IdentifyVideoExtensions(XDocument doc)
    {
        var extensions = doc.Descendants("extension");
        foreach (var ext in extensions)
        {
            var extName = ext.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(extName)) continue;

            // Name contains "video" (most reliable Khronos marker for KHR/EXT)
            if (extName.Contains("_video_") || extName.Contains("video_encode") || extName.Contains("video_decode"))
            {
                _registry.VideoExtensions.Add(extName);
                continue;
            }

            // Extension requires types that are already forbidden (StdVideo)
            var requiredTypes = ext.Descendants("type").Select(t => t.Attribute("name")?.Value);
            if (requiredTypes.Any(t => t != null && _registry.ForbiddenVideoTypes.Contains(t)))
            {
                _registry.VideoExtensions.Add(extName);
            }
        }
    }

    private void ParseVendorTags(XDocument doc)
    {
        var tags = doc.Descendants("tags").Elements("tag");
        foreach (var tag in tags)
        {
            string name = tag.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                _registry.VendorTags.Add(name);
            }
        }
    }

    private void ParseBaseTypes(List<XElement> elements)
    {
        foreach (var node in elements)
        {
            var nameElement = node.Element("name");
            if (nameElement == null) continue;

            string name = nameElement.Value;
            
            if (name != null && !_requiredNames.Contains(name))
                continue;

            // Check for #ifdef in the raw content of the node for Apple/Metal.
            string rawContent = node.Value;
            if (rawContent.Contains("#ifdef") || rawContent.Contains("__OBJC__"))
            {
                _registry.BaseTypes[name] = "void*";
                _registry.Typedefs[name] = new VkTypedef
                {
                    Name = name,
                    BaseType = "void",
                    IsPointer = true,
                    IsOpaque = true,
                    LineInfo = node,
                    CurrentFileName = _currentFileName
                };
                continue;
            }

            // Build base type from all nodes except <name>
            var baseTypeBuilder = new StringBuilder();
            foreach (var innerNode in node.Nodes())
            {
                if (innerNode == nameElement) continue;

                if (innerNode is XElement el)
                {
                    baseTypeBuilder.Append(el.Value);
                }
                else if (innerNode is XText txt)
                {
                    baseTypeBuilder.Append(txt.Value);
                }
            }

            string baseType = baseTypeBuilder.ToString()
                .Replace("typedef", "")
                .Replace(";", "")
                .Trim();

            if (string.IsNullOrEmpty(baseType)) continue;

            _registry.BaseTypes[name] = baseType;

            // Remove system flags (VkFlags, VkFlags64)
            if (name != "VkFlags" && name != "VkFlags64")
            {
                bool isPointer = baseType.Contains("*");
                bool isStruct = baseType.Contains("struct");

                var ts = new VkTypedef
                {
                    Name = name,
                    BaseType = baseType,
                    IsPointer = isPointer || isStruct,
                    IsOpaque = isStruct || rawContent.Contains("@class"),
                    LineInfo = node,
                    CurrentFileName = _currentFileName
                };

                _registry.Typedefs[name] = ts;
            }
        }
    }

    public void ParseAllConstants(XDocument doc)
    {
        var unresolvedAliases = new Dictionary<string, (string target, bool isDeprecated)>();

        // Gathering all <enum> from the whole document (including <enums>, <feature>, <extension>)
        var allEnumNodes = doc.Descendants("enum")
            .Where(e => (e.Attribute("value") != null || e.Attribute("alias") != null)
                        && e.Attribute("offset") == null);

        foreach (var node in allEnumNodes)
        {
            string name = node.Attribute("name")?.Value;
            string value = node.Attribute("value")?.Value;
            string alias = node.Attribute("alias")?.Value;

            bool isDeprecated = node.Attribute("deprecated") != null;

            if (string.IsNullOrEmpty(name)) continue;

            if (value != null)
            {
                var classification = CleanAndClassify(value);
                _registry.Constants[name] = new VkConstant
                {
                    Name = name,
                    Value = classification.Value,
                    PrimitiveType = classification.PrimitiveType,
                    NumericValue = value.ParseVulkanNumeric(),
                    IsDeprecated = isDeprecated,
                    LineInfo = node,
                    CurrentFileName = _currentFileName
                };
            }
            else if (alias != null)
            {
                unresolvedAliases[name] = (alias, isDeprecated);
            }
        }
        
        var bitmaskNodes = doc.Descendants("enums")
            .Where(e => e.Attribute("type")?.Value == "bitmask")
            .Elements("enum")
            .Where(e => e.Attribute("value") != null || e.Attribute("bitpos") != null);

        foreach (var node in bitmaskNodes)
        {
            string name = node.Attribute("name")?.Value;
            if (name != null && !_registry.Constants.ContainsKey(name))
            {
                string val = node.Attribute("value")?.Value ?? node.Attribute("bitpos")?.Value;
                var classification = CleanAndClassify(val);
                _registry.Constants[name] = new VkConstant 
                { 
                    Name = name,
                    Value = classification.Value,
                    PrimitiveType = classification.PrimitiveType,
                    NumericValue = val.ParseVulkanNumeric(),
                    IsDeprecated = node.Attribute("deprecated") != null,
                    LineInfo = node,
                    CurrentFileName = _currentFileName
                };
            }
        }

        ParsePreprocessorDefines(doc);
        
        var aliasNodes = doc.Descendants("enum")
            .Where(e => e.Attribute("alias") != null);

        foreach (var node in aliasNodes)
        {
            string name = node.Attribute("name")?.Value;
            string aliasTarget = node.Attribute("alias")?.Value;
            bool isDeprecated = node.Attribute("deprecated") != null;

            if (name != null && aliasTarget != null)
            {
                unresolvedAliases[name] = (aliasTarget, isDeprecated);
            }
        }

        ResolveAliases(unresolvedAliases);
    }
    
    private (object Value, PrimitiveType PrimitiveType) CleanAndClassify(string rawValue)
    {
        if (string.IsNullOrEmpty(rawValue)) 
            return (null, PrimitiveType.Unknown);

        if (rawValue.Contains("\"")) 
        {
            return (rawValue.Replace("\"", "").Trim(), PrimitiveType.String);
        }

        // Remove outer brackets, but keep the inner content.
        // (~0U) -> ~0U
        string cleaned = rawValue.Trim();
        if (cleaned.StartsWith("(") && cleaned.EndsWith(")"))
        {
            cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
        }

        if (cleaned.Contains("f") || cleaned.Contains("."))
        {
            return (cleaned, PrimitiveType.Float);
        }
    
        if (cleaned.EndsWith("ULL") || (cleaned.EndsWith("U") && cleaned.Length > 10))
        {
            return (cleaned, PrimitiveType.UInt64);
        }

        if (cleaned.EndsWith("U"))
        {
            return (cleaned, PrimitiveType.UInt32);
        }

        return (cleaned, PrimitiveType.Int32);
    }

    private void ParsePreprocessorDefines(XDocument doc)
    {
        var defineNodes = doc.Descendants("type")
            .Where(t => t.Attribute("category")?.Value == "define");

        foreach (var def in defineNodes)
        {
            var nameElement = def.Element("name");
            if (nameElement == null) continue;

            string name = nameElement.Value;
            bool isDeprecated = def.Attribute("deprecated") != null;
            var remainingNodes = nameElement.NodesAfterSelf().ToList();
            string fullTail = string.Concat(remainingNodes.Select(n => n is XElement e ? e.Value : n.ToString()));
            // remove multiline comments
            if (fullTail.Contains("/*"))
            {
                int commentStart = fullTail.IndexOf("/*");
                fullTail = fullTail.Substring(0, commentStart).Trim();
            }
            // remove comments
            if (fullTail.Contains("//"))
            {
                fullTail = fullTail.Split("//")[0].Trim();
            }
            bool isFunctionLike = fullTail.TrimStart().StartsWith("(");;
            string rawValue = fullTail.Trim();

            if (!string.IsNullOrEmpty(rawValue))
            {
                var classification = CleanAndClassify(rawValue);
                
                if (rawValue.Contains("API_VERSION"))
                {
                    classification.PrimitiveType = PrimitiveType.UInt32;
                }
                
                _registry.Constants[name] = new VkConstant
                {
                    Name = name,
                    Value = classification.Value,
                    PrimitiveType = classification.PrimitiveType,
                    NumericValue = rawValue.ParseVulkanNumeric(),
                    IsDeprecated = isDeprecated,
                    IsFunctionLike = isFunctionLike,
                    LineInfo = def,
                    CurrentFileName = _currentFileName
                };
            }
        }
    }
    
    private void ResolveAliases(Dictionary<string, (string target, bool isDeprecated)> unresolvedAliases)
    {
        bool wasChanged;
    
        do
        {
            wasChanged = false;
            var keys = unresolvedAliases.Keys.ToList();

            foreach (var name in keys)
            {
                var info = unresolvedAliases[name];

                // Try resolving the alias to a constant.
                if (_registry.Constants.TryGetValue(info.target, out var targetConstant))
                {
                    _registry.Constants[name] = new VkConstant
                    {
                        Value = targetConstant.Value,
                        NumericValue = targetConstant.NumericValue,
                        PrimitiveType = targetConstant.PrimitiveType,
                        IsDeprecated = info.isDeprecated || targetConstant.IsDeprecated,
                        LineInfo = targetConstant.LineInfo,
                        CurrentFileName = _currentFileName
                    };
                
                    unresolvedAliases.Remove(name);
                    wasChanged = true;
                }
            }

        } while (wasChanged && unresolvedAliases.Count > 0);

        // If after the loop, there are still elements in the dictionary, it means that there are "bitwise" links
        // foreach (var fail in unresolvedAliases)
        // {
        //     Console.WriteLine($"[Error] Circular or missing alias: {fail.Key} -> {fail.Value.target}");
        // }
    }

    private void ParseBitmasks(XDocument doc)
    {
        var bitmasks = doc.Descendants("type")
            .Where(t => t.Attribute("category")?.Value == "bitmask");

        foreach (var node in bitmasks)
        {
            string flagsName = node.Attribute("name")?.Value ?? node.Element("name")?.Value;

            if (!_includeVideoExtensions && flagsName.IsVideoType())
                continue;

            if (string.IsNullOrEmpty(flagsName)) continue;

            // Checking if it's an alias (like VkMemoryUnmapFlagsKHR)'
            string alias = node.Attribute("alias")?.Value;
            if (!string.IsNullOrEmpty(alias))
            {
                _registry.Aliases[flagsName] = alias;
                RegisterAliasGroup(flagsName, alias);
                continue;
            }

            string flagBitsName = node.Attribute("requires")?.Value;
            string baseType = node.Element("type")?.Value;

            if (flagsName != null && flagBitsName != null)
            {
                _registry.FlagsToBitsMap[flagsName] = flagBitsName;
            }
            else
            {
                var ts = new VkTypedef
                {
                    Name = flagsName,
                    BaseType = (baseType == "VkFlags64") ? "ulong" : "uint",
                    LineInfo = node,
                    CurrentFileName = _currentFileName
                };
                try
                {
                    _registry.Typedefs[flagsName] = ts;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }
    }

    private void ParseEnums(XDocument doc)
    {
        var enumBlocks = doc.Descendants("enums")
            .Where(node =>
            {
                var api = node.Attribute("api")?.Value;
                return api == null || api.Split(',').Contains("vulkan");
            });

        foreach (var node in enumBlocks)
        {
            var name = node.Attribute("name")?.Value;
            
            if (name != null && !_requiredNames.Contains(name))
                continue;

            if (!_includeVideoExtensions && name.IsVideoType())
                continue;

            if (name == "API Constants") continue;

            if (node.Attribute("type") == null) continue;

            var en = new VkEnum
            {
                Name = name,
                IsFlags = node.Attribute("type")?.Value == "bitmask",
                Comment = node.Attribute("comment")?.Value,
                LineInfo = node,
                CurrentFileName = _currentFileName
            };

            if (en.IsFlags)
            {
                // Search in base types. For example, for VkAccessFlags we will find VkFlags
                // And for VkFlags in the dictionary lies uint32_t
                // But in vk.xml Flags often refer to the base VkFlags

                // The simplest way for Vulkan:
                if (node.Attribute("bitwidth")?.Value == "64")
                    en.BaseType = "ulong";
                else
                    en.BaseType = "uint";
            }
            else
            {
                en.BaseType = "int"; // Standard for regular Vulkan enums
            }

            foreach (var val in node.Elements("enum"))
            {
                if (!val.IsVulkanProp())
                    continue;

                var lineInfo = (IXmlLineInfo)val;

                // An ALIAS carries no value of its own - vk.xml writes it as name + alias only (e.g.
                // VK_STENCIL_FRONT_AND_BACK -> VK_STENCIL_FACE_FRONT_AND_BACK, deprecated="aliased"). Reading just
                // value/bitpos left it empty, and an empty value renders as 0: a member that looks like a legal flag,
                // compiles, and then makes every command that takes it fail on an empty mask. Take the target's value;
                // if the target is not in this enum, emit nothing at all rather than a zero that lies.
                var aliasOf = val.Attribute("alias")?.Value;
                if (aliasOf != null)
                {
                    var target = en.Values.FirstOrDefault(v => v.Name == aliasOf);
                    if (target == null) continue;

                    en.Values.Add(new VkEnumValue
                    {
                        Name = val.Attribute("name").Value,
                        Value = target.Value,
                        BitPos = target.BitPos,
                        Comment = val.Attribute("comment")?.Value,
                        LineInfo = lineInfo
                    });
                    continue;
                }

                var enumItem = new VkEnumValue
                {
                    Name = val.Attribute("name").Value,
                    Value = val.Attribute("value")?.Value,
                    BitPos = val.Attribute("bitpos")?.Value,
                    Comment = val.Attribute("comment")?.Value,
                    LineInfo = lineInfo
                };

                if (!string.IsNullOrEmpty(enumItem.BitPos))
                {
                    if (int.TryParse(enumItem.BitPos, out int bit))
                    {
                        ulong calculatedValue = 1UL << bit;
                        enumItem.Value = calculatedValue.ToString();
                    }
                }

                en.Values.Add(enumItem);
            }

            if (en.Name != null)
                _registry.Enums[en.Name] = en;
        }
    }

    private void ParseHandles(List<XElement> elements)
    {
        var handles = elements
            .Select(h => new VkHandle
            {
                Name = h.Element("name")?.Value ?? h.Attribute("name")?.Value,
                Parent = h.Attribute("parent")?.Value,
                IsDispatchable = h.Value.Contains("VK_DEFINE_HANDLE"),
                LineInfo = h,
                CurrentFileName = _currentFileName
            });
        
        foreach (var vkHandle in handles)
        {
            if (!_requiredNames.Contains(vkHandle.Name))
                continue;
            
            _registry.Handles[vkHandle.Name] = vkHandle;
            _registry.HandleParents[vkHandle.Name] = vkHandle.Parent;
        }
    }

    private void ParseStructs(List<XElement> elements, bool isUnion)
    {
        foreach (var sNode in elements)
        {
            if (!sNode.IsVulkanProp())
                continue;

            var name = sNode.Attribute("name").Value;

            if (sNode.Attribute("alias") != null)
                continue;
            
            if (name != null && !_requiredNames.Contains(name))
                continue;

            if (!_includeVideoExtensions && name.IsVideoType())
                continue;

            if (!_registry.IsTypeAllowed(_settings, _allowedPlatforms, name))
                continue;

            if (!_includeVideoExtensions)
            {
                bool hasIgnoredMembers = sNode.Elements("member").Any(m =>
                {
                    var typeName = m.Element("type")?.Value;
                    return typeName != null && typeName.StartsWith("StdVideo");
                });

                if (hasIgnoredMembers) continue;
            }
            
            var lineInfo = (IXmlLineInfo)sNode;

            var s = new VkStruct
            {
                Name = name, 
                IsUnion = isUnion, 
                LineInfo = lineInfo, 
                CurrentFileName = _currentFileName
            };

            foreach (var mNode in sNode.Elements("member"))
            {
                var member = new VkMember();
                var typeElement = mNode.Element("type");
                var nameElement = mNode.Element("name");
                
                var desc = mNode.Attribute("comment")?.Value;
                // If not in the attribute, look for a text node inside or nearby
                if (string.IsNullOrEmpty(desc))
                    desc = mNode.Nodes().OfType<XText>().LastOrDefault()?.Value?.Trim();

                member.Comment = desc;
                member.Name = nameElement.Value;
                member.Type = typeElement.Value;
                var rawLen = mNode.Attribute("len")?.Value;
                string altLen = mNode.Attribute("altlen")?.Value;

                string finalLen = rawLen;
                if (rawLen != null && rawLen.StartsWith("latexmath"))
                {
                    finalLen = altLen;
                }

                if (!string.IsNullOrEmpty(finalLen))
                {
                    if (finalLen == "null-terminated")
                    {
                        member.Len = finalLen;
                    }
                    else if (_registry.IsCompileTimeConstant(finalLen))
                    {
                        // Calculate the final number once and for all
                        var size = _registry.ResolveExpression(finalLen);
                        member.Len = size.ToString();
                        member.ArrayDimensions.Add((int)size);
                    }
                    else
                    {
                        // This is a runtime expression (e.g.: "codeSize / 4")
                        // 1. Extract the primary size source (the first encountered field)
                        var match = Regex.Match(finalLen, @"[a-zA-Z_][a-zA-Z0-9_]*");
                        string primaryField = "";

                        while (match.Success)
                        {
                            if (!_registry.Constants.ContainsKey(match.Value))
                            {
                                primaryField = match.Value;
                                break;
                            }

                            match = match.NextMatch();
                        }

                        member.Len = primaryField; // codeSize
                        // Save the original for the marshalling generator, 
                        // so it can write: for(int i=0; i < (codeSize / 4); i++)
                        member.LenExpression = altLen;
                    }
                }

                // 1. Extract pointer "depth" and constness
                // Text BEFORE the <type> tag usually contains 'const' or 'struct'
                string preType = "";
                foreach (var node in mNode.Nodes())
                {
                    if (node == typeElement) break;
                    preType += node.ToString();
                }

                // Text BETWEEN <type> and <name> contains asterisks '*'
                string midType = "";
                var currentNode = typeElement.NextNode;
                while (currentNode != null && currentNode != nameElement)
                {
                    midType += currentNode.ToString();
                    currentNode = currentNode.NextNode;
                }

                member.IsConst = preType.Contains("const") || midType.Contains("const");
                member.PointerDepth = midType.Count(c => c == '*');
                member.IsPointer = midType.Contains("*");
                if (member.IsPointer && member.IsFixedArray)
                {
                    member.ArrayDimensions.Clear();
                    member.Len = string.Empty;
                }

                // 2. Form FullType for generator convenience (e.g., "const VkDeviceSize*")
                member.FullType = $"{preType}{member.Type}{midType}".Trim();

                // 3. Semantics and default values
                member.DefaultValueType = mNode.Attribute("type")?.Value;
                member.DefaultValue = mNode.Attribute("values")?.Value;

                if (member.Name == "sType" && !string.IsNullOrEmpty(member.DefaultValue))
                {
                    member.Semantic = MemberSemantic.StructureType;
                }
                else if (member.Name == "pNext")
                {
                    member.Semantic = MemberSemantic.NextPointer;
                }

                string dimensionsRaw = "";

                while (currentNode != null)
                {
                    // Collect everything: both text and the content of <enum> tags
                    if (currentNode is XElement element && element.Name == "enum")
                    {
                        dimensionsRaw += element.Value;
                    }
                    else if (currentNode is XText text)
                    {
                        dimensionsRaw += text.Value;
                    }

                    currentNode = currentNode.NextNode;
                }

                if (!string.IsNullOrEmpty(dimensionsRaw))
                {
                    var matches = Regex.Matches(dimensionsRaw, @"\[([^\]]+)\]");
                    foreach (Match match in matches)
                    {
                        string dimValue = match.Groups[1].Value;
                        if (int.TryParse(dimValue, out int dim))
                            member.ArrayDimensions.Add(dim);
                        else
                            member.ArrayDimensions.Add((int)_registry.ResolveExpression(dimValue));
                    }
                }

                if (member.Type == "void" && member.IsPointer && member.PointerDepth == 1 && member.IsArray)
                {
                    member.Type = "byte";
                }

                s.Members.Add(member);
            }
            
            if (isUnion)
            {
                _registry.Unions[s.Name] = s;
            }
            else
            {
                _registry.Structs[s.Name] = s;
            }
        }
    }

    private void ParseEnumAliases(XDocument doc)
    {
        var enumAliases = doc.Descendants("type")
            .Where(t => t.Attribute("category")?.Value == "enum" && t.Attribute("alias") != null);

        foreach (var node in enumAliases)
        {
            string name = node.Attribute("name")?.Value;
            string aliasTarget = node.Attribute("alias")?.Value;

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(aliasTarget))
            {
                _registry.Aliases[name] = aliasTarget;
                RegisterAliasGroup(name, aliasTarget);
            }
        }
    }

    private void BuildAliasGroups(XDocument doc)
    {
        // Group structs and unions
        var typeNodes = doc.Descendants("type")
            .Where(t => (t.Attribute("category")?.Value == "struct" || t.Attribute("category")?.Value == "union")
                        && t.Attribute("alias") != null);
        
        foreach (var node in typeNodes)
        {
            RegisterAliasGroup(node.Attribute("name").Value, node.Attribute("alias").Value);
        }
        
        var handleNodes = doc.Descendants("type")
            .Where(t => t.Attribute("category")?.Value == "handle" && t.Attribute("alias") != null);

        foreach (var node in handleNodes)
        {
            RegisterAliasGroup(node.Attribute("name").Value, node.Attribute("alias").Value);
        }

        // Group commands (functions)
        var cmdNodes = doc.Descendants("command")
            .Where(c => c.Attribute("alias") != null);

        foreach (var node in cmdNodes)
        {
            RegisterAliasGroup(node.Attribute("name").Value, node.Attribute("alias").Value);
        }
    }

    private void RegisterAliasGroup(string name, string alias)
    {
        // Находим или создаем общую группу для обоих имен
        _registry.AliasGroups.TryGetValue(name, out var group1);
        _registry.AliasGroups.TryGetValue(alias, out var group2);

        var unifiedGroup = group1 ?? group2 ?? new HashSet<string>();
        unifiedGroup.Add(name);
        unifiedGroup.Add(alias);

        _registry.AliasGroups[name] = unifiedGroup;
        _registry.AliasGroups[alias] = unifiedGroup;
    }

    private void CollapseToMasterNames()
    {
        // 1. Create a "Name -> Canonical Master name" map
        var canonicalMap = new Dictionary<string, string>();
        _registry.AliasesForGlobalUsings.Clear();

        foreach (var groupEntry in _registry.AliasGroups)
        {
            var masterName = _registry.GetMasterName(groupEntry.Value);
            canonicalMap[groupEntry.Key] = masterName;

            if (groupEntry.Key != masterName)
            {
                _registry.AliasesForGlobalUsings[groupEntry.Key] = masterName;
            }
        }
        
        var handleKeys = _registry.Handles.Keys.ToList();
        foreach (var name in handleKeys)
        {
            if (canonicalMap.TryGetValue(name, out var master) && name != master)
            {
                _registry.AliasesForGlobalUsings[name] = master;
                _registry.Handles.Remove(name);
                _registry.HandleParents.Remove(name);
            }
        }

        var structKeys = _registry.Structs.Keys.ToList();
        foreach (var name in structKeys)
        {
            if (canonicalMap.TryGetValue(name, out var master) && name != master)
            {
                // Save to usings if not yet there
                _registry.AliasesForGlobalUsings[name] = master;
                // Remove the "secondary" struct
                _registry.Structs.Remove(name);
            }
        }

        // Same for Union
        var unionKeys = _registry.Unions.Keys.ToList();
        foreach (var name in unionKeys)
        {
            if (canonicalMap.TryGetValue(name, out var master) && name != master)
            {
                _registry.AliasesForGlobalUsings[name] = master;
                _registry.Unions.Remove(name);
            }
        }

        // 3. Collapse fields of structs and unions
        var allStructs = _registry.Structs.Values.Concat(_registry.Unions.Values).ToList();
        foreach (var s in allStructs)
        {
            foreach (var member in s.Members)
            {
                if (canonicalMap.TryGetValue(member.Type, out string masterType))
                {
                    member.Type = masterType;
                }
            }
        }

        var commandKeys = _registry.Commands.Keys.ToList();
        foreach (var name in commandKeys)
        {
            if (canonicalMap.TryGetValue(name, out var master) && name != master)
            {
                _registry.AliasesForGlobalUsings[name] = master;
                _registry.Commands.Remove(name);
            }
        }

        foreach (var cmd in _registry.Commands.Values)
        {
            foreach (var param in cmd.Params)
            {
                if (canonicalMap.TryGetValue(param.Type, out string masterType))
                {
                    param.Type = masterType;
                }
            }

            if (canonicalMap.TryGetValue(cmd.ReturnType, out string masterReturn))
            {
                cmd.ReturnType = masterReturn;
            }
        }

        var delegateKeys = _registry.Delegates.Keys.ToList();
        foreach (var name in delegateKeys)
        {
            var del = _registry.Delegates[name];
            if (!string.IsNullOrEmpty(del.CommandName) && canonicalMap.TryGetValue(del.CommandName, out var masterCmd))
            {
                var masterDelName = $"PFN_{masterCmd}";
                if (name != masterDelName)
                {
                    _registry.AliasesForGlobalUsings[name] = masterDelName;
                    _registry.Delegates.Remove(name);
                }
            }
        }

        // 4. Clean the registry from "duplicates" (optional)
        // If you want to end up with only Master objects in Structs/Commands:
        _registry.CanonicalTypeMap = canonicalMap;
    }

    private void ParseCommands(XDocument doc)
    {
        var commands = doc.Descendants("command").Where(x => x.IsVulkanProp())
            .Where(c => c.Element("proto") != null)
            .ToList();

        foreach (var cmdNode in commands)
        {
            var name = cmdNode.Element("proto").Element("name").Value;
            
            if (name != null && !_requiredNames.Contains(name))
                continue;
            
            var lineInfo = (IXmlLineInfo)cmdNode;

            var cmd = new VkCommand
            {
                Name = name,
                ReturnType = cmdNode.Element("proto").Element("type").Value,
                LineInfo = lineInfo,
                CurrentFileName = _currentFileName
            };

            if (!_registry.IsTypeAllowed(_settings, _allowedPlatforms, name))
                continue;

            foreach (var pNode in cmdNode.Elements("param"))
            {
                var api = pNode.Attribute("api")?.Value;
                if (api != null && !api.Split(',').Contains("vulkan"))
                    continue;

                var param = new VkParameter();
                param.Name = pNode.Element("name").Value;
                param.Type = pNode.Element("type").Value;
                param.FullType = pNode.Value.Replace(pNode.Element("name").Value, "").Trim();
                var rawLen = pNode.Attribute("len")?.Value;
                string altLen = pNode.Attribute("altlen")?.Value;
                
                param.Len = rawLen != null && rawLen.StartsWith("latexmath") ? altLen : rawLen;
                param.Len = ParserExtensions.SanitizeLengthExpression(param.Len);
                var optionalAttr = pNode.Attribute("optional")?.Value;
                if (!string.IsNullOrEmpty(optionalAttr))
                {
                    if (optionalAttr.Contains(","))
                    {
                        param.IsOptional = optionalAttr.Contains("true");
                    }
                    else
                    {
                        param.IsOptional = optionalAttr == "true";
                    }
                }
                param.IsPointer = pNode.Value.Contains("*");
                param.PointerDepth = pNode.Value.Count(c => c == '*');
                param.IsConst = pNode.Value.Contains("const");
                
                if (param.Type == "void" && param.IsPointer && param.PointerDepth == 1 && param.IsArray)
                {
                    param.Type = "byte";
                }
                else if (param.Type != "char" && param.IsPointer && param.PointerDepth == 2 && param.IsArray)
                {
                    param.Type = "void";
                }
                
                cmd.Params.Add(param);
            }

            _registry.Commands.Add(cmd.Name, cmd);
        }

        var aliases = doc.Descendants("command")
            .Where(x => x.IsVulkanProp())
            .Where(c => c.Attribute("alias") != null);

        foreach (var aliasNode in aliases)
        {
            string name = aliasNode.Attribute("name").Value;
            string target = aliasNode.Attribute("alias").Value;

            RegisterAliasGroup(name, target);
        }
    }

    private void ParseDelegates(XDocument doc)
    {
        var nodes = doc.Descendants("type")
            .Where(t => t.Attribute("category")?.Value == "funcpointer")
            .Where(x => x.IsVulkanProp());

        foreach (var node in nodes)
        {
            // 1. Name is always in the <name> tag
            var nameElement = node.Descendants("name").FirstOrDefault();
            if (nameElement == null) continue;
            
            if (!_requiredNames.Contains(nameElement.Value))
                continue;
            
            var fp = new VkDelegate();
            fp.Name = nameElement.Value;
            
            var lineInfo = (IXmlLineInfo)node;
            fp.LineInfo = lineInfo;
            fp.CurrentFileName = _currentFileName;

            // 2. Extract ReturnType (everything BEFORE <name>)
            var protoElement = node.Element("proto");
            string returnTypeRaw = "";

            if (protoElement != null)
            {
                // Если это новый формат SDK: берем весь текст внутри <proto> ДО тега <name>
                var typeEl = protoElement.Element("type");
                string preText = typeEl != null ? typeEl.Value : "";
    
                // Собираем текст и звездочки между <type> и <name> внутри <proto>
                string midText = "";
                var currentNode = typeEl?.NextNode;
                while (currentNode != null && currentNode != nameElement)
                {
                    midText += currentNode.ToString();
                    currentNode = currentNode.NextNode;
                }
                returnTypeRaw = $"{preText}{midText}".Trim();
            }
            else
            {
                // Откат для старых версий vk.xml (твой старый рабочий код)
                string preText = "";
                foreach (var n in node.Nodes())
                {
                    if (n == nameElement) break;
                    preText += n.ToString();
                }
                returnTypeRaw = preText;
            }

            // Clean up garbage: typedef, VKAPI_PTR, pointers and brackets
            fp.ReturnType = returnTypeRaw.Replace("typedef", "").Replace("VKAPI_PTR", "")
                .Replace("(", "").Replace("*", "").Trim();

            // 3. Parse parameters (look for <type> -> next text pairs)
            var paramNodes = node.Elements("param").ToList();

        if (paramNodes.Count > 0)
        {
            // New delegate parameters format
            foreach (var pNode in paramNodes)
            {
                var typeEl = pNode.Element("type");
                var nameEl = pNode.Element("name");
                if (typeEl == null || nameEl == null) continue;

                var param = new VkParameter
                {
                    Name = nameEl.Value.Trim(),
                    Type = typeEl.Value.Trim()
                };

                string fullParamText = pNode.Value;
                param.IsConst = fullParamText.Contains("const");

                string modifiers = fullParamText.Replace(param.Name, "").Replace(param.Type, "");
                param.IsPointer = modifiers.Contains("*");
                param.PointerDepth = modifiers.Count(c => c == '*');

                param.FullType = $"{(param.IsConst ? "const " : "")}{param.Type}{new string('*', param.PointerDepth)} {param.Name}";

                if (param.Type == "void" && param.IsPointer && param.PointerDepth == 1 && param.IsArray)
                {
                    param.Type = "byte";
                }
                else if (param.Type != "char" && param.IsPointer && param.PointerDepth == 2 && param.IsArray)
                {
                    param.Type = "void";
                }

                if (!string.IsNullOrEmpty(param.Name))
                    fp.Params.Add(param);
            }
        }
        else
        {
            // Old format
            var types = node.Elements("type").ToList();
            foreach (var typeEl in types)
            {
                if (node.Nodes().TakeWhile(n => n != nameElement).Contains(typeEl))
                    continue;

                var param = new VkParameter { Type = typeEl.Value.Trim() };

                if (typeEl.PreviousNode is XText preTextNode)
                {
                    string preTextStr = preTextNode.Value;
                    param.IsConst = preTextStr.Contains("const");
                }

                if (typeEl.NextNode is XText nameNode)
                {
                    string cleanRaw = nameNode.Value.Replace("\r", "").Replace("\n", "").Trim();
                    string raw = cleanRaw.Split(',', ')')[0].Trim();

                    param.Name = raw.Replace("*", "").Trim();
                    param.IsPointer = raw.Contains("*");
                    param.PointerDepth = raw.Count(c => c == '*');

                    if (raw.Contains("const")) param.IsConst = true;

                    param.FullType = $"{(param.IsConst ? "const " : "")}{param.Type}{new string('*', param.PointerDepth)} {param.Name}";
                }

                if (param.Type == "void" && param.IsPointer && param.PointerDepth == 1 && param.IsArray)
                {
                    param.Type = "byte";
                }
                else if (param.Type != "char" && param.IsPointer && param.PointerDepth == 2 && param.IsArray)
                {
                    param.Type = "void";
                }

                if (!string.IsNullOrEmpty(param.Name))
                    fp.Params.Add(param);
            }
        }
            /*
            var types = node.Elements("type").ToList();
            foreach (var typeEl in types)
            {
                // Skip if this <type> is inside return (happens in complex definitions)
                if (node.Nodes().TakeWhile(n => n != nameElement).Contains(typeEl))
                    continue;

                var param = new VkParameter { Type = typeEl.Value.Trim() };

                if (typeEl.PreviousNode is XText preTextNode)
                {
                    string preTextStr = preTextNode.Value;
                    param.IsConst = preTextStr.Contains("const");
                }

                // 2. Look for name and pointers AFTER the type
                if (typeEl.NextNode is XText nameNode)
                {
                    // Clean from garbage, commas and that very const, which can be for the NEXT parameter
                    // We take only the first part before a comma or bracket
                    string raw = nameNode.Value.Split(',', ')')[0].Trim();

                    param.Name = raw.Replace("*", "").Trim();
                    param.IsPointer = raw.Contains("*");
                    param.PointerDepth = raw.Count(c => c == '*');

                    // If const was written after the type (rare, but happens in C)
                    if (raw.Contains("const")) param.IsConst = true;

                    param.FullType =
                        $"{(param.IsConst ? "const " : "")}{param.Type}{new string('*', param.PointerDepth)} {param.Name}";
                }
                
                if (param.Type == "void" && param.IsPointer && param.PointerDepth == 1 && param.IsArray)
                {
                    param.Type = "byte";
                }
                else if (param.Type != "char" && param.IsPointer && param.PointerDepth == 2 && param.IsArray)
                {
                    param.Type = "void";
                }

                if (!string.IsNullOrEmpty(param.Name))
                    fp.Params.Add(param);
            }
            */

            _registry.Delegates[fp.Name] = fp;
        }

        foreach (var cmd in _registry.Commands.Values)
        {
            var pfnName = $"PFN_{cmd.Name}";

            var apiDelegate = new VkDelegate
            {
                Name = pfnName,
                CommandName = cmd.Name,
                ReturnType = cmd.ReturnType,
                Params = cmd.Params.ToList(),
                LineInfo = cmd.LineInfo,
                CurrentFileName = _currentFileName
            };

            _registry.Delegates[pfnName] = apiDelegate;

            if (_registry.ExtensionPlatformMap.TryGetValue(cmd.Name, out var platform))
            {
                _registry.ExtensionPlatformMap[pfnName] = platform;
            }
        }
    }
    
    private void BuildAllowList(XDocument doc)
    {
        _requiredNames = new HashSet<string>();

        // Take only Vulkan features (1.0, 1.1, 1.2, 1.3, 1.4, ...)
        // Ignore blocks with api="vulkansc"
        var vulkanFeatures = doc.Descendants("feature")
            .Where(f => f.Attribute("api")?.Value.Split(',').Contains("vulkan") == true);
    
        foreach (var feat in vulkanFeatures)
        {
            foreach (var req in feat.Elements("require"))
            {
                foreach (var type in req.Elements("type"))
                {
                    var typeName = type.Attribute("name")?.Value;
                    if (typeName != null) _requiredNames.Add(typeName);
                }
                foreach (var cmd in req.Elements("command"))
                {
                    var cmdName = cmd.Attribute("name")?.Value;
                    if (cmdName != null) _requiredNames.Add(cmdName);
                }
            }
        }

        foreach (var ext in _registry.Extensions.Values)
        {
            if (!_allowedPlatforms.Contains(ext.Platform)) 
            {
                continue;
            }
            
            foreach (var typeName in ext.RequiredTypes) _requiredNames.Add(typeName);
            foreach (var cmdName in ext.RequiredCommands) _requiredNames.Add(cmdName);
        }
    }

    private void ParseExtensions(XDocument doc)
    {
        var extensions = doc.Descendants("extensions").First();

        foreach (var extNode in extensions.Elements("extension"))
        {
            var extName = extNode.Attribute("name").Value;
            
            var typesInExtension = extNode.Descendants("type")
                .Select(t => t.Attribute("name")?.Value)
                .Where(n => n != null);

            bool isVideoExtension = typesInExtension.Any(t => _registry.ForbiddenVideoTypes.Contains(t));

            if (isVideoExtension)
            {
                _registry.VideoExtensions.Add(extName);
            }
            
            var supported = extNode.Attribute("supported")?.Value;
            if (supported != null)
            {
                var apis = supported.Split(',');
                if (!apis.Contains("vulkan")) 
                {
                    continue;
                }
            }
            
            var api = extNode.Attribute("api")?.Value;
            if (api != null && !api.Split(',').Contains("vulkan"))
            {
                continue;
            }

            var ext = new VkExtension
            {
                Name = extName,
                Type = extNode.Attribute("type")?.Value,
                Platform = extNode.Attribute("platform")?.Value ?? _commonPlatformName,
                ExtensionType = extNode.Attribute("type")?.Value == "instance"
                    ? ExtensionType.Instance
                    : ExtensionType.Device
            };
            
            foreach (var reqNode in extNode.Elements("require"))
            {
                // Check api attribute for specific require block
                var reqApi = reqNode.Attribute("api")?.Value;
                if (reqApi != null)
                {
                    var apis = reqApi.Split(',').Select(s => s.Trim());
                    if (!apis.Contains("vulkan"))
                        continue; // if block os for vulkansc - skip it
                }

                // Getting types and commands only from valid block
                var commands = reqNode.Elements("command")
                    .Select(c => c.Attribute("name")?.Value)
                    .Where(n => n != null);

                var types = reqNode.Elements("type")
                    .Select(t => t.Attribute("name")?.Value)
                    .Where(n => n != null);

                foreach (var type in types)
                {
                    if (!ext.RequiredTypes.Contains(type))
                        ext.RequiredTypes.Add(type);

                    _registry.TypeToExtensionMap[type] = extName;
                    if (!string.IsNullOrEmpty(ext.Platform))
                    {
                        _registry.ExtensionPlatformMap[type] = ext.Platform;
                    }
                }

                foreach (var cmd in commands)
                {
                    if (!ext.RequiredCommands.Contains(cmd))
                        ext.RequiredCommands.Add(cmd);

                    _registry.TypeToExtensionMap[cmd] = extName;
                    if (!string.IsNullOrEmpty(ext.Platform))
                    {
                        _registry.ExtensionPlatformMap[cmd] = ext.Platform;
                    }
                }
            }

            _registry.Extensions.Add(ext.Name, ext);
        }
    }

    private void ParseExtensionEnums(XDocument doc)
    {
        var extensions = doc.Descendants("extension");

        foreach (var extNode in extensions)
        {
            // Default extension number for the whole block
            var baseExtNumberAttr = extNode.Attribute("number")?.Value;
            int.TryParse(baseExtNumberAttr, out int baseExtNumber);

            var additions = extNode.Descendants("enum").Where(e => e.Attribute("extends") != null);

            foreach (var addNode in additions)
            {
                var targetEnumName = addNode.Attribute("extends").Value;
                var valueName = addNode.Attribute("name").Value;

                // 1. PRIORITY: check if a specific enum has its own extnumber
                var specificExtNumberAttr = addNode.Attribute("extnumber")?.Value;
                int currentExtNumber = baseExtNumber;

                if (!string.IsNullOrEmpty(specificExtNumberAttr))
                {
                    int.TryParse(specificExtNumberAttr, out currentExtNumber);
                }

                if (currentExtNumber <= 0) continue;

                if (_registry.Enums.TryGetValue(targetEnumName, out var targetEnum))
                {
                    // 2. ALIAS processing
                    var aliasAttr = addNode.Attribute("alias")?.Value;
                    if (!string.IsNullOrEmpty(aliasAttr))
                    {
                        // If it is an alias, just look for the original value
                        var original = targetEnum.Values.FirstOrDefault(v => v.Name == aliasAttr);
                        if (original != null)
                        {
                            targetEnum.Values.Add(new VkEnumValue { Name = valueName, Value = original.Value });
                        }

                        continue;
                    }

                    var bitposAttr = addNode.Attribute("bitpos")?.Value;
                    var offsetAttr = addNode.Attribute("offset")?.Value;
                    var dirAttr = addNode.Attribute("dir")?.Value;

                    if (!string.IsNullOrEmpty(bitposAttr) && int.TryParse(bitposAttr, out int bit))
                    {
                        if (targetEnum.Values.Any(v => v.Name == valueName))
                            continue;

                        ulong calculatedValue = 1UL << bit;

                        targetEnum.Values.Add(new VkEnumValue
                        {
                            Name = valueName,
                            Value = calculatedValue.ToString(),
                            LineInfo = addNode
                        });
                    }
                    else if (int.TryParse(offsetAttr, out int offset))
                    {
                        // Use the actual currentExtNumber (base or specific)
                        int value = 1000000000 + (currentExtNumber - 1) * 1000 + offset;
                        if (dirAttr == "-") value = -value;

                        if (targetEnum.Values.Any(v => v.Name == valueName))
                            continue;

                        targetEnum.Values.Add(new VkEnumValue
                        {
                            Name = valueName,
                            Value = value.ToString(),
                            LineInfo = addNode
                        });
                    }
                }
            }
        }
    }

    private void ParseFeatureEnums(XDocument doc)
    {
        // Look for <feature api="vulkan"> blocks
        var features = doc.Descendants("feature")
            .Where(f => f.Attribute("api")?.Value.Contains("vulkan") == true);

        foreach (var feature in features)
        {
            // Inside each version, look for enums that extend types
            var enums = feature.Descendants("enum")
                .Where(e => e.Attribute("extends") != null);

            foreach (var node in enums)
            {
                var targetEnum = node.Attribute("extends").Value;
                var name = node.Attribute("name").Value;

                if (_registry.Enums.TryGetValue(targetEnum, out var vkEnum))
                {
                    // In features, values are usually defined via 'offset' or 'value'
                    var bitposAttr = node.Attribute("bitpos")?.Value;
                    var offsetAttr = node.Attribute("offset")?.Value;
                    var valueAttr = node.Attribute("value")?.Value;
                    var extNumberAttr = node.Attribute("extnumber")?.Value;
                    
                    if (valueAttr != null)
                    {
                        if (vkEnum.Values.Any(v => v.Name == name))
                            continue;

                        vkEnum.Values.Add(new VkEnumValue { Name = name, Value = valueAttr, LineInfo = node });
                    }
                    if (!string.IsNullOrEmpty(bitposAttr) && int.TryParse(bitposAttr, out int bit))
                    {
                        if (vkEnum.Values.Any(v => v.Name == name))
                            continue;

                        ulong calculatedValue = 1UL << bit;

                        vkEnum.Values.Add(new VkEnumValue
                        {
                            Name = name,
                            Value = calculatedValue.ToString(),
                            LineInfo = node
                        });
                    }
                    else if (offsetAttr != null)
                    {
                        // For features, an extension number is also used if it is specified in the enum node
                        // or taken from the external context. 
                        // Formula: 1,000,000,000 + (extNumber - 1) * 1,000 + offset
                        if (int.TryParse(offsetAttr, out int offset) && int.TryParse(extNumberAttr, out int extNum))
                        {
                            int val = 1000000000 + (extNum - 1) * 1000 + offset;

                            if (vkEnum.Values.Any(v => v.Name == name))
                                continue;

                            vkEnum.Values.Add(new VkEnumValue { Name = name, Value = val.ToString(), LineInfo = node });
                        }
                    }
                }
            }
        }
    }
    
    private void FilterUnsupportedTypes()
    {
        var typedefsToRemove = _registry.Typedefs.Keys
            .Where(name => !_registry.IsTypeAllowed(_settings, _allowedPlatforms, name))
            .ToList();

        foreach (var name in typedefsToRemove)
        {
            _registry.Typedefs.Remove(name);
        }
        
        var enumsToRemove = _registry.Enums.Keys
            .Where(name => !_registry.IsTypeAllowed(_settings, _allowedPlatforms, name))
            .ToList();

        foreach (var name in enumsToRemove)
        {
            _registry.Enums.Remove(name);
        }
        
        var structsToRemove = _registry.Structs.Keys
            .Where(name => !_registry.IsTypeAllowed(_settings, _allowedPlatforms, name))
            .ToList();

        foreach (var name in structsToRemove)
        {
            _registry.Structs.Remove(name);
        }
        
        var unionsToRemove = _registry.Unions.Keys
            .Where(name => !_registry.IsTypeAllowed(_settings, _allowedPlatforms, name))
            .ToList();

        foreach (var name in unionsToRemove)
        {
            _registry.Unions.Remove(name);
        }
    }

    private void MapModelsToDeclarations()
    {
        MapConstantsToDeclarations();
        MapEnumsToDeclarations();
        MapTypedefsToDeclarations();
        MapHandlesToDeclarations();
        MapStructsToDeclarations();
        MapCommandsToDeclarations();
        MapDelegatesToDeclarations();
        GenerateGlobalUsings();
        GenerateDispatchTables();
    }

    private void MapConstantsToDeclarations()
    {
        foreach (var constant in _registry.Constants)
        {
            if (constant.Value.Value.ToString().StartsWith("VK_MAKE_VIDEO_STD_VERSION"))
            {
                constant.Value.PrimitiveType = PrimitiveType.UInt32;
            }
            
            var constDeclaration = new Macro()
            {
                Name = constant.Key,
                Value = constant.Value.Value.ToString(),
                IsFunctionLike = constant.Value.IsFunctionLike,
                PrimitiveType = constant.Value.PrimitiveType
            };

            AddDeclaration(constDeclaration);
            UpdateLineInfo(constDeclaration, constant.Value);
        }
    }

    private void MapTypedefsToDeclarations()
    {
        foreach (var typedef in _registry.Typedefs)
        {
            var @class = new Class()
            {
                ClassType = ClassType.Struct,
                Name = typedef.Value.Name,
                IsSimpleType = true,
            };

            PrimitiveType podType;
            if (typedef.Value.IsPointer || typedef.Value.IsOpaque)
            {
                podType = PrimitiveType.Nuint;
            }
            else
            {
                podType = GetPrimitiveType(typedef.Value.BaseType);
            }

            var builtinType = new BuiltinType();
            builtinType.Declaration = @class;
            builtinType.Type = podType;

            @class.UnderlyingNativeType = builtinType;

            var field = new Field();
            field.Name = "value";
            field.Type = builtinType;

            @class.AddField(field);

            var op = new Operator
            {
                Class = @class,
                FieldName = field.Name,
                Type = field.Type,
                TransformationKind = TransformationKind.FromClassToValue,
                OperatorKind = OperatorKind.Implicit
            };
            @class.Operators.Add(op);

            op = new Operator
            {
                Class = @class,
                FieldName = field.Name,
                Type = field.Type,
                TransformationKind = TransformationKind.FromValueToClass,
                OperatorKind = OperatorKind.Implicit
            };
            @class.Operators.Add(op);

            AddDeclaration(@class);
            UpdateLineInfo(@class, typedef.Value);
        }
    }

    private void MapEnumsToDeclarations()
    {
        foreach (var vkEnum in _registry.Enums)
        {
            var en = new Enumeration();
            en.Name = vkEnum.Value.Name;
            en.IsFlagEnum = vkEnum.Value.IsFlags;
            en.InheritanceType = vkEnum.Value.BaseType;
            en.Items = vkEnum.Value.Values.Select(v => new EnumerationItem
            {
                Name = v.Name,
                Value = _registry.ResolveExpression(v.Value)
            }).ToList();

            AddDeclaration(en);
            UpdateLineInfo(en, vkEnum.Value);
        }
    }

    private void MapHandlesToDeclarations()
    {
        foreach (var vkHandle in _registry.Handles)
        {
            CreateHandle(vkHandle.Value);
        }
    }

    private void MapStructsToDeclarations()
    {
        foreach (var vkStruct in _registry.Structs)
        {
            CreateStruct(vkStruct.Value);
        }

        foreach (var vkStruct in _registry.Unions)
        {
            CreateStruct(vkStruct.Value);
        }
    }

    private void MapCommandsToDeclarations()
    {
        foreach (var vkCommand in _registry.Commands)
        {
            var func = new Function();
            func.Name = vkCommand.Value.Name;
            func.EntryPoint = vkCommand.Value.Name;
            func.CallingConvention = CallingConvention.StdCall;

            var returnType = new VkParameter() { Type = vkCommand.Value.ReturnType };

            func.ReturnType = GetBindingType(returnType);

            foreach (var param in vkCommand.Value.Params)
            {
                var parameter = new Parameter
                {
                    Name = param.Name,
                    Type = GetBindingType(param),
                    IsOptional = param.IsOptional,
                };

                if (param.IsConst)
                {
                    parameter.ParameterKind = ParameterKind.Readonly;
                }
                else if (parameter.IsOptional && param.IsPointer)
                {
                    parameter.ParameterKind = ParameterKind.Ref;
                }
                else if (param.IsPointer && !param.IsConst)
                {
                    parameter.ParameterKind = ParameterKind.Out;
                }
                else
                {
                    parameter.ParameterKind = ParameterKind.In;
                }
                
                func.AddParameter(parameter);
            }

            AddDeclaration(func);
            UpdateLineInfo(func, vkCommand.Value);
        }
    }

    private void MapDelegatesToDeclarations()
    {
        foreach (var vkDelegate in _registry.Delegates)
        {
            var @delegate = new Delegate();
            @delegate.Name = vkDelegate.Value.Name;
            @delegate.CallingConvention = CallingConvention.StdCall;
            var returnType = new VkParameter() { Type = vkDelegate.Value.ReturnType };
            @delegate.ReturnType = GetBindingType(returnType);

            foreach (var param in vkDelegate.Value.Params)
            {
                var parameter = new Parameter
                {
                    Name = param.Name,
                    Type = GetBindingType(param),
                    IsOptional = param.IsOptional,
                };
                
                if (param.IsConst)
                {
                    parameter.ParameterKind = ParameterKind.Readonly;
                }
                else if (parameter.IsOptional && param.IsPointer)
                {
                    parameter.ParameterKind = ParameterKind.Ref;
                }
                else if (param.IsPointer && !param.IsConst)
                {
                    parameter.ParameterKind = ParameterKind.Out;
                }
                else
                {
                    parameter.ParameterKind = ParameterKind.In;
                }
                
                @delegate.AddParameter(parameter);
            }

            AddDeclaration(@delegate);
            UpdateLineInfo(@delegate, vkDelegate.Value);
        }
    }

    private void GenerateGlobalUsings()
    {
        var platformUsingsMap = new Dictionary<string, GlobalUsings>();
        foreach (var aliasEntry in _registry.AliasesForGlobalUsings)
        {
            var aliasName = aliasEntry.Key;
            var masterName = aliasEntry.Value;

            // 1. Check if the master type is allowed for this platform
            if (!_registry.IsTypeAllowed(_settings, _allowedPlatforms, masterName))
                continue;

            // 2. Check if the alias itself (KHR/EXT) is allowed for this platform
            if (!_registry.IsTypeAllowed(_settings, _allowedPlatforms, aliasName))
                continue;

            var masterDeclaration = _translationUnit.Declarations.FirstOrDefault(d => d.Name == masterName);
            if (masterDeclaration == null)
                continue;
            
            var platform = GetPlatformName(aliasName);
            if (!platformUsingsMap.TryGetValue(platform, out var platformUsings))
            {
                platformUsings = new GlobalUsings();
                platformUsings.Location = new FileLocation() { FileName = platform };
                platformUsingsMap[platform] = platformUsings;
            }

            if (masterDeclaration is Function func)
            {
                masterDeclaration = _translationUnit.Delegates.FirstOrDefault(d => d.Name == $"PFN_{masterName}");
                aliasName = $"PFN_{aliasName}";
            }

            var aliasDeclaration = (Declaration)masterDeclaration.Clone();
            aliasDeclaration.Name = aliasName;

            platformUsings.AddUsing(aliasDeclaration, masterDeclaration);
        }

        foreach (var platformUsings in platformUsingsMap)
        {
            _translationUnit.AddDeclaration(platformUsings.Value);
        }
    }

    private void GenerateDispatchTables()
    {
        var instance = _translationUnit.Classes.FirstOrDefault(f => f.Name == "VkInstance");
        var device = _translationUnit.Classes.FirstOrDefault(f => f.Name == "VkDevice");
        GenerateDispatchTable("InstanceDispatchTable", HandleScope.Instance, instance, "GetInstanceProcAddr");
        GenerateDispatchTable("DeviceDispatchTable", HandleScope.Device, device, "GetDeviceProcAddr");
    }

    private void GenerateDispatchTable(string tableName, HandleScope tableScope, Class owner, string functionName)
    {
        var dispatchTable = new DispatchTable();
        dispatchTable.Name = tableName;
        dispatchTable.TableOwner = owner;
        dispatchTable.Metadata.Set("Scope", tableScope);
        dispatchTable.FunctionName = functionName;
        dispatchTable.IgnoreFunction($"vk{functionName}");

        var names = new List<string>();
        
        foreach (var registryHandle in _registry.Handles)
        {
            var scope = DetermineScope(registryHandle.Value.Name);
            if (scope != tableScope)
                continue;
            
            if (!registryHandle.Value.IsDispatchable)
                continue;
            
            names.Add(registryHandle.Value.Name);
            var decl = _translationUnit.Classes.FirstOrDefault(f => f.Name == registryHandle.Value.Name);
            decl?.DispatchTable = dispatchTable;

            if (decl is { Name: "VkInstance" } && scope == HandleScope.Instance)
            {
                decl.DispatchTable = dispatchTable;
            }
            else if (decl is { Name: "VkDevice" } && scope == HandleScope.Device)
            {
                decl.DispatchTable = dispatchTable;
            }
            
            var dispatchField = new Field()
            {
                Name = decl.DispatchFieldName,
                Type = new DispatchTableType()
                {
                    Declaration = dispatchTable 
                },
                AccessSpecifier = AccessSpecifier.Internal
            };

            decl.AddField(dispatchField);
        }
        
        dispatchTable.Metadata.Set("Handles", names);
        
        AddDeclaration(dispatchTable);
    }

    private Class CreateHandle(VkHandle vkHandle)
    {
        var handle = new Class();
        handle.ClassType = ClassType.Class;
        handle.Name = vkHandle.Name;
        handle.IsDispatchable = vkHandle.IsDispatchable;

        var nativeStruct = CreateHandleStruct(vkHandle, handle);

        var field = new Field();
        field.AccessSpecifier = AccessSpecifier.Internal;
        field.Name = "__Instance";
        field.Type = new CustomType(handle.NativeStruct.Name);
        field.Type.Declaration = handle.NativeStruct;

        handle.AddField(field);
        var inputParameter = new Parameter(field.Name);
        inputParameter.Type = field.Type;
        inputParameter.Parent = handle;
        inputParameter.ParameterKind = ParameterKind.Readonly;

        var op = new Operator();
        op.Class = handle;
        op.FieldName = field.Name;
        op.Type = field.Type;
        op.TransformationKind = TransformationKind.FromClassToValue;
        op.OperatorKind = OperatorKind.Implicit;
        handle.Operators.Add(op);

        op = new Operator();
        op.Class = handle;
        op.FieldName = field.Name;
        op.Type = field.Type;
        op.TransformationKind = TransformationKind.FromValueToClass;
        op.OperatorKind = OperatorKind.Implicit;
        op.PassValueToConstructor = true;
        handle.Operators.Add(op);

        var defaultCtr = new Constructor() { Class = handle, IsDefault = true };
        handle.Constructors.Add(defaultCtr);

        var ctr = new Constructor() { Class = handle };
        ctr.InputParameters.Add(inputParameter);
        handle.Constructors.Add(ctr);

        AddDeclaration(nativeStruct);
        AddDeclaration(handle);
        UpdateLineInfo(handle, vkHandle);
        UpdateLineInfo(nativeStruct, vkHandle);

        return handle;
    }

    private Class CreateHandleStruct(VkHandle vkHandle, Class parent)
    {
        var nativeStruct = new Class();
        nativeStruct.ClassType = ClassType.Struct;
        nativeStruct.Name = $"{vkHandle.Name}_T";
        nativeStruct.LinkedTo = parent;
        parent.NativeStruct = nativeStruct;

        var nativeField = new Field();
        nativeField.AccessSpecifier = AccessSpecifier.Public;
        nativeField.Name = "pointer";
        nativeField.Type = new PointerType() { Pointee = new BuiltinType(PrimitiveType.Void) };

        nativeStruct.AddField(nativeField);
        var nativeInputParameter = new Parameter(nativeField.Name);
        nativeInputParameter.Type = nativeField.Type;
        nativeInputParameter.Parent = nativeStruct;
        nativeInputParameter.ParameterKind = ParameterKind.Readonly;

        return nativeStruct;
    }

    private Class CreateStruct(VkStruct vkStruct)
    {
        var nativeStruct = new Class();
        nativeStruct.ClassType = vkStruct.IsUnion ? ClassType.Union : ClassType.Struct;
        nativeStruct.Name = vkStruct.Name;
        
        foreach (var member in vkStruct.Members)
        {
            var fieldType = GetBindingType(member);
            var field = new Field
            {
                Name = member.Name,
                Type = fieldType,
            };

            if (member.Semantic == MemberSemantic.StructureType)
            {
                var declaration = _translationUnit.Enums.FirstOrDefault(e => e.Name == member.Type);
                var item = declaration?.Items.FirstOrDefault(i => i.Name == member.DefaultValue);
                var index = declaration?.Items.IndexOf(item) ?? 0;

                field.PredefinedInputSource = () => $"{declaration.Name}.{declaration.Items[index].Name}";
                field.UsePredefinedInputSource = true;
                field.PredefinedValue = member.DefaultValue;
                field.IsPredefinedValueReadOnly = true;
            }

            nativeStruct.AddField(field);
        }

        AddDeclaration(nativeStruct);
        UpdateLineInfo(nativeStruct, vkStruct);

        return nativeStruct;
    }

    private BindingType GetBindingType(VkTypeBase entry)
    {
        BindingType resultType = CreateBaseType(entry.Type);
        resultType.Qualifiers.IsConst = entry.IsConst;

        if (entry.Type == "char" && entry.Len == "null-terminated" && !entry.IsFixedArray)
        {
            for (int i = 0; i < entry.PointerDepth; i++)
            {
                resultType = new PointerType { Pointee = resultType };
                resultType.Qualifiers.IsConst = entry.IsConst;
            }

            return resultType;
        }

        if (entry.IsArray || entry.IsFixedArray)
        {
            if (entry.IsFixedArray && entry is VkMember member)
            {
                var arrayType = new ArrayType()
                    { ElementType = resultType, SizeType = ArraySizeType.Constant, Size = member.TotalArraySize };
                resultType = arrayType;
            }
            else
            {
                var parts = entry.Len.Split(',');

                var arrayType = new ArrayType();
                arrayType.ElementType = resultType;
                arrayType.ArraySizeSource = parts[0];
                arrayType.MathExpression = entry.LenExpression;
                arrayType.SizeType = ArraySizeType.Incomplete;
                resultType = arrayType;
            }
        }

        for (int i = 0; i < entry.PointerDepth; i++)
        {
            resultType = new PointerType { Pointee = resultType };
            resultType.Qualifiers.IsConst = entry.IsConst;
        }

        return resultType;
    }

    private string ResolveAlias(string name)
    {
        while (_registry.Aliases.TryGetValue(name, out var target))
        {
            name = target;
        }

        return name;
    }

    private BindingType CreateBaseType(string xmlType)
    {
        xmlType = ResolveAlias(xmlType);

        if (_registry.Typedefs.ContainsKey(xmlType))
        {
            return new CustomType(xmlType);
        }

        if (_registry.FlagsToBitsMap.TryGetValue(xmlType, out var flagBitsName))
        {
            if (_registry.Enums.ContainsKey(flagBitsName))
            {
                return new CustomType(flagBitsName);
            }
        }

        if (_registry.Handles.ContainsKey(xmlType))
            return new CustomType(xmlType);

        if (_registry.Structs.ContainsKey(xmlType) || _registry.Unions.ContainsKey(xmlType))
            return new CustomType(xmlType);

        if (_registry.Delegates.ContainsKey(xmlType))
            return new CustomType(xmlType);

        if (_registry.Enums.ContainsKey(xmlType))
            return new CustomType(xmlType);

        if (_registry.BaseTypes.TryGetValue(xmlType, out var underlyingType))
        {
            return CreateBaseType(underlyingType);
        }

        return MapPrimitiveType(xmlType);
    }

    private BindingType MapPrimitiveType(string xmlType)
    {
        var primitive = GetPrimitiveType(xmlType);

        return new BuiltinType(primitive);
    }

    private PrimitiveType GetPrimitiveType(string xmlType)
    {
        var primitive = xmlType switch
        {
            "void" => PrimitiveType.Void,
            "char" => PrimitiveType.SChar,
            "byte" => PrimitiveType.Byte,
            "float" => PrimitiveType.Float,
            "double" => PrimitiveType.Double,
            "uint8_t" => PrimitiveType.Byte,
            "uint16_t" => PrimitiveType.UInt16,
            "uint" => PrimitiveType.UInt32,
            "uint32_t" => PrimitiveType.UInt32,
            "ulong" => PrimitiveType.UInt64,
            "uint64_t" => PrimitiveType.UInt64,
            "int8_t" => PrimitiveType.Sbyte,
            "int16_t" => PrimitiveType.Int16,
            "int32_t" => PrimitiveType.Int32,
            "int64_t" => PrimitiveType.Int64,
            "size_t" => PrimitiveType.Nuint,
            "int" => PrimitiveType.Int32,
            "HWND" or "HINSTANCE" or "HANDLE" or "SECURITY_ATTRIBUTES" or "DWORD" or "LPCWSTR" or "HMONITOR" =>
                PrimitiveType.Nuint,
            // Wayland's display and surface are opaque structs, met only behind a pointer.
            "wl_display" or "wl_surface" => PrimitiveType.Void,
            _ => throw new Exception($"Unknown primitive type: {xmlType}")
        };

        return primitive;
    }

    private void AddDeclaration(Declaration decl)
    {
        SetSourceLocation(decl);

        _translationUnit.AddDeclaration(decl);
    }

    private void UpdateLineInfo(Declaration decl, VkDeclaration vkDecl)
    {
        decl.Location.LineNumber = (uint)vkDecl.LineInfo.LineNumber;
        decl.Location.Column = (uint)vkDecl.LineInfo.LinePosition;
        decl.Location.RealFileName = vkDecl.CurrentFileName;
    }
    
    private string GetPlatformName(string extensionName)
    {
        if (_registry.ExtensionPlatformMap.TryGetValue(extensionName, out var platform))
        {
            return $"vulkan_{platform}";
        }
        
        return "vulkan_core";
    }

    private void SetSourceLocation(Declaration decl)
    {
        decl.Location = _registry.ExtensionPlatformMap.TryGetValue(decl.Name, out var platform)
            ? new FileLocation() { FileName = $"vulkan_{platform}" }
            : new FileLocation() { FileName = "vulkan_core" };
    }
    
    public HandleScope DetermineScope(string handleName)
    {
        if (_registry.CanonicalTypeMap.TryGetValue(handleName, out var masterName))
        {
            handleName = masterName;
        }
        
        if (handleName == "VkDevice") return HandleScope.Device;
        if (handleName == "VkInstance") return HandleScope.Instance;
    
        if (handleName == "VkPhysicalDevice") return HandleScope.Instance;

        if (_registry.HandleParents.TryGetValue(handleName, out var parentName))
        {
            return DetermineScope(parentName);
        }

        return HandleScope.Instance;
    }

    public void Dispose()
    {
    }
}
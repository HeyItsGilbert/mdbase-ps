using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Mdbase.Core.Yaml;

/// <summary>
/// Converts a YamlDotNet <see cref="YamlNode"/> tree into mdbase's JSON data model
/// (spec Ch.06 "Data Model"): mapping -&gt; <see cref="OrderedDictionary"/>, sequence -&gt;
/// <c>object?[]</c>, scalar -&gt; <c>null</c>/<see cref="bool"/>/<see cref="long"/>/<see cref="double"/>/<see cref="string"/>.
///
/// Per #28's resolution, YamlDotNet's own high-level deserializer never attempts implicit
/// scalar typing (every plain scalar arrives as a raw string) — this converter owns 100% of
/// the null/bool/int/float decision itself, against the <see cref="YamlScalarNode.Style"/> and
/// raw <see cref="YamlScalarNode.Value"/> the <see cref="RepresentationModel"/> layer exposes
/// with zero implicit CLR typing (per docs/research/csharp-yaml-libraries.md).
/// </summary>
internal static class YamlJsonConverter
{
    private static readonly Regex IntPattern = new(@"^[-+]?[0-9]+$", RegexOptions.Compiled);

    private static readonly Regex FloatPattern = new(
        @"^[-+]?((\d+\.\d*|\.\d+)([eE][-+]?\d+)?|\d+[eE][-+]?\d+)$",
        RegexOptions.Compiled);

    private static readonly Regex NonFinitePattern = new(@"^[-+]?\.(nan|inf)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static object? Convert(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => ConvertScalar(scalar),
            YamlMappingNode mapping => ConvertMapping(mapping),
            YamlSequenceNode sequence => sequence.Children.Select(Convert).ToArray(),
            _ => throw new FrontmatterParseException($"Unsupported YAML node kind '{node.NodeType}'."),
        };
    }

    private static OrderedDictionary ConvertMapping(YamlMappingNode mapping)
    {
        var result = new OrderedDictionary();
        foreach (var entry in mapping.Children)
        {
            if (entry.Key is not YamlScalarNode keyScalar || keyScalar.Value is null)
            {
                throw new FrontmatterParseException("Mapping keys must be non-null scalars.");
            }

            result[keyScalar.Value] = Convert(entry.Value);
        }

        return result;
    }

    private static object? ConvertScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        var tag = scalar.Tag.IsEmpty ? null : scalar.Tag.Value;

        if (tag is not null)
        {
            return tag switch
            {
                "tag:yaml.org,2002:str" => value,
                "tag:yaml.org,2002:null" => null,
                "tag:yaml.org,2002:bool" => ResolveBool(value)
                    ?? throw new FrontmatterParseException($"'{value}' is not a valid boolean scalar."),
                "tag:yaml.org,2002:int" => ResolveInt(value)
                    ?? throw new FrontmatterParseException($"'{value}' is not a valid integer scalar."),
                "tag:yaml.org,2002:float" => ResolveFloat(value)
                    ?? throw new FrontmatterParseException($"'{value}' is not a valid float scalar."),
                _ => throw new FrontmatterParseException(
                    $"YAML tag '{tag}' is not part of the mdbase JSON data model; use an untagged scalar."),
            };
        }

        // Quoted/literal/folded scalars are never implicitly resolved — only plain scalars are.
        if (scalar.Style != ScalarStyle.Plain)
        {
            return value;
        }

        if (value.Length == 0 || value is "~" or "null" or "Null" or "NULL")
        {
            return null;
        }

        var asBool = ResolveBool(value);
        if (asBool is not null)
        {
            return asBool;
        }

        if (NonFinitePattern.IsMatch(value))
        {
            throw new FrontmatterParseException(
                $"'{value}' is a non-finite YAML scalar (NaN/Infinity), which has no JSON data-model representation.");
        }

        var asInt = ResolveInt(value);
        if (asInt is not null)
        {
            return asInt;
        }

        var asFloat = ResolveFloat(value);
        if (asFloat is not null)
        {
            return asFloat;
        }

        return value;
    }

    private static bool? ResolveBool(string value) => value switch
    {
        "true" or "True" or "TRUE" => true,
        "false" or "False" or "FALSE" => false,
        _ => null,
    };

    private static object? ResolveInt(string value)
    {
        if (!IntPattern.IsMatch(value))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return l;
        }

        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    private static object? ResolveFloat(string value)
    {
        if (!FloatPattern.IsMatch(value))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}

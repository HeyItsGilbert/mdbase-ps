using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Mdbase.Core.Yaml;

/// <summary>
/// Serializes mdbase's JSON data model back to a Markdown record's frontmatter block (spec
/// Ch.03 "Serialization"), sibling to <see cref="FrontmatterParser"/>. Preserves
/// <see cref="OrderedDictionary"/> insertion order and picks a scalar style that round-trips
/// through <see cref="YamlJsonConverter"/>'s own resolution rules — a string that would
/// otherwise resolve as bool/int/float/null on reload is quoted; every other plain scalar stays
/// unquoted (#41 point 37).
/// </summary>
internal static class FrontmatterWriter
{
    private static readonly Regex IntPattern = new(@"^[-+]?[0-9]+$", RegexOptions.Compiled);

    private static readonly Regex FloatPattern = new(
        @"^[-+]?((\d+\.\d*|\.\d+)([eE][-+]?\d+)?|\d+[eE][-+]?\d+)$",
        RegexOptions.Compiled);

    /// <summary>Renders a complete Markdown record document: `---`, serialized frontmatter, `---`, then <paramref name="body"/> verbatim.</summary>
    public static string Render(OrderedDictionary frontmatter, string body)
    {
        var yaml = SerializeYamlMapping(frontmatter);
        var sb = new StringBuilder();
        sb.Append("---\n");
        sb.Append(yaml);
        if (yaml.Length > 0 && !yaml.EndsWith('\n'))
        {
            sb.Append('\n');
        }

        sb.Append("---\n");
        sb.Append(body);
        return sb.ToString();
    }

    /// <summary>Serializes a mapping to a standalone YAML block (no `---` delimiters).</summary>
    public static string SerializeYamlMapping(OrderedDictionary mapping)
    {
        if (mapping.Count == 0)
        {
            return "{}\n";
        }

        var node = (YamlMappingNode)ToYamlNode(mapping)!;
        using var writer = new StringWriter();
        var emitter = new Emitter(writer, new EmitterSettings(bestIndent: 2, bestWidth: int.MaxValue, isCanonical: false, maxSimpleKeyLength: int.MaxValue, skipAnchorName: true));
        emitter.Emit(new StreamStart());
        emitter.Emit(new DocumentStart(null, null, isImplicit: true));
        EmitNode(emitter, node);
        emitter.Emit(new DocumentEnd(isImplicit: true));
        emitter.Emit(new StreamEnd());
        return writer.ToString();
    }

    private static void EmitNode(IEmitter emitter, YamlNode node)
    {
        switch (node)
        {
            case YamlScalarNode scalar:
                emitter.Emit(new Scalar(null, scalar.Tag.IsEmpty ? null : scalar.Tag.Value, scalar.Value ?? string.Empty, scalar.Style, scalar.Tag.IsEmpty, false));
                break;
            case YamlMappingNode mapping:
                emitter.Emit(new MappingStart(null, null, true, MappingStyle.Block));
                foreach (var entry in mapping.Children)
                {
                    EmitNode(emitter, entry.Key);
                    EmitNode(emitter, entry.Value);
                }

                emitter.Emit(new MappingEnd());
                break;
            case YamlSequenceNode sequence:
                emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Block));
                foreach (var item in sequence.Children)
                {
                    EmitNode(emitter, item);
                }

                emitter.Emit(new SequenceEnd());
                break;
            default:
                throw new NotSupportedException($"Unsupported YAML node kind '{node.NodeType}'.");
        }
    }

    private static YamlNode ToYamlNode(object? value) => value switch
    {
        null => new YamlScalarNode("null") { Style = ScalarStyle.Plain },
        bool b => new YamlScalarNode(b ? "true" : "false") { Style = ScalarStyle.Plain },
        long l => new YamlScalarNode(l.ToString(CultureInfo.InvariantCulture)) { Style = ScalarStyle.Plain },
        double d => new YamlScalarNode(d.ToString("R", CultureInfo.InvariantCulture)) { Style = ScalarStyle.Plain },
        string s => new YamlScalarNode(s) { Style = ScalarStyleFor(s) },
        OrderedDictionary map => ToYamlMapping(map),
        object?[] arr => ToYamlSequence(arr),
        _ => throw new NotSupportedException($"Value of type {value.GetType()} is not part of the mdbase JSON data model."),
    };

    private static YamlMappingNode ToYamlMapping(OrderedDictionary map)
    {
        var node = new YamlMappingNode();
        foreach (DictionaryEntry entry in map)
        {
            node.Add(new YamlScalarNode((string)entry.Key) { Style = ScalarStyle.Plain }, ToYamlNode(entry.Value));
        }

        return node;
    }

    private static YamlSequenceNode ToYamlSequence(object?[] arr)
    {
        var node = new YamlSequenceNode();
        foreach (var item in arr)
        {
            node.Add(ToYamlNode(item));
        }

        return node;
    }

    /// <summary>
    /// A string value is quoted whenever plain (unquoted) emission would round-trip through
    /// <see cref="YamlJsonConverter"/> as a different type — null, bool, int, or float — or
    /// would collide with a reserved plain-scalar spelling that resolves to one of those.
    /// </summary>
    private static ScalarStyle ScalarStyleFor(string value)
    {
        if (value.Length == 0)
        {
            return ScalarStyle.DoubleQuoted;
        }

        if (value is "~" or "null" or "Null" or "NULL"
            or "true" or "True" or "TRUE" or "false" or "False" or "FALSE")
        {
            return ScalarStyle.DoubleQuoted;
        }

        if (IntPattern.IsMatch(value) || FloatPattern.IsMatch(value))
        {
            return ScalarStyle.DoubleQuoted;
        }

        if (value.Contains('\n') || value.Contains('\t'))
        {
            return ScalarStyle.DoubleQuoted;
        }

        // A leading/trailing space, or a leading character with YAML block/flow significance,
        // is not safe as an unquoted plain scalar either.
        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            return ScalarStyle.DoubleQuoted;
        }

        if ("!&*-?|>%@`\"'#,[]{}:".Contains(value[0]) || value.Contains(": ") || value.Contains(" #") || value.EndsWith(':'))
        {
            return ScalarStyle.DoubleQuoted;
        }

        return ScalarStyle.Plain;
    }
}

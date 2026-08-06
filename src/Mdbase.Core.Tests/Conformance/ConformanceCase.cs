using System.Collections;
using System.Collections.Specialized;
using Mdbase.Core.Yaml;

namespace Mdbase.Core.Tests.Conformance;

/// <summary>
/// One `operation`/`input`/`expect` case from a vendored v0.3 conformance fixture group,
/// paired with its group's `setup` (spec `tests/v0.3/README.md` "Format").
/// </summary>
public sealed class ConformanceCase
{
    public required string GroupName { get; init; }

    public required string TestName { get; init; }

    public string? Id { get; init; }

    public required string Operation { get; init; }

    public required OrderedDictionary Input { get; init; }

    public required OrderedDictionary Expect { get; init; }

    public required string ConfigYaml { get; init; }

    public required IReadOnlyDictionary<string, string> Types { get; init; }

    public required IReadOnlyDictionary<string, string> Files { get; init; }

    public override string ToString() => Id ?? $"{GroupName}: {TestName}";

    /// <summary>Parses every case out of a vendored fixture-group YAML file's text.</summary>
    public static IReadOnlyList<ConformanceCase> LoadAll(string fixtureYamlText)
    {
        var root = FrontmatterParser.ParseYamlMapping(fixtureYamlText);
        var cases = new List<ConformanceCase>();

        foreach (var groupNode in (object?[])root["groups"]!)
        {
            var group = (OrderedDictionary)groupNode!;
            var groupName = (string)group["name"]!;
            var setup = group["setup"] as OrderedDictionary;

            var configYaml = setup?["config"] as string ?? "spec_version: \"0.3.0\"\n";
            var types = ToStringMap(setup?["types"] as OrderedDictionary);
            var files = ToStringMap(setup?["files"] as OrderedDictionary);

            foreach (var testNode in (object?[])group["tests"]!)
            {
                var test = (OrderedDictionary)testNode!;
                cases.Add(new ConformanceCase
                {
                    GroupName = groupName,
                    TestName = (string)test["name"]!,
                    Id = test["id"] as string,
                    Operation = (string)test["operation"]!,
                    Input = (OrderedDictionary)test["input"]!,
                    Expect = (OrderedDictionary)test["expect"]!,
                    ConfigYaml = configYaml,
                    Types = types,
                    Files = files,
                });
            }
        }

        return cases;
    }

    private static IReadOnlyDictionary<string, string> ToStringMap(OrderedDictionary? map)
    {
        var result = new Dictionary<string, string>();
        if (map is null)
        {
            return result;
        }

        foreach (DictionaryEntry entry in map)
        {
            result[(string)entry.Key] = (string)entry.Value!;
        }

        return result;
    }
}

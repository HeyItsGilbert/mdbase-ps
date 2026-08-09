using System.Collections.Specialized;
using Mdbase.Core.Json;
using Mdbase.Core.Yaml;

namespace Mdbase.Core.Tests;

public class FrontmatterWriterTests
{
    [Fact]
    public void Render_round_trips_every_scalar_kind_through_the_parser()
    {
        var frontmatter = new OrderedDictionary
        {
            ["title"] = "Hello World",
            ["looksLikeBool"] = "true",
            ["looksLikeInt"] = "42",
            ["looksLikeFloat"] = "3.14",
            ["looksLikeNull"] = "null",
            ["actualBool"] = true,
            ["actualInt"] = 42L,
            ["actualFloat"] = 3.14,
            ["actualNull"] = null,
            ["colonPhrase"] = "note: keep reading",
            ["nested"] = new OrderedDictionary { ["a"] = 1L, ["b"] = "two" },
            ["list"] = new object?[] { "x", "y", 3L },
        };

        var document = FrontmatterWriter.Render(frontmatter, "body text\n");
        var parsed = FrontmatterParser.Parse(document);

        Assert.True(JsonModel.DeepEquals(frontmatter, parsed.Frontmatter));
        Assert.Equal("body text\n", parsed.Body);

        // Order is preserved.
        var originalKeys = frontmatter.Keys.Cast<string>().ToArray();
        var parsedKeys = parsed.Frontmatter.Keys.Cast<string>().ToArray();
        Assert.Equal(originalKeys, parsedKeys);
    }

    [Fact]
    public void Render_quotes_a_string_that_would_otherwise_resolve_as_a_different_type()
    {
        var frontmatter = new OrderedDictionary { ["status"] = "false" };
        var document = FrontmatterWriter.Render(frontmatter, string.Empty);
        var parsed = FrontmatterParser.Parse(document);

        Assert.Equal("false", parsed.Frontmatter["status"]);
        Assert.IsType<string>(parsed.Frontmatter["status"]);
    }
}

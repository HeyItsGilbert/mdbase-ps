using Mdbase.Core.Yaml;

namespace Mdbase.Core.Tests;

public class YamlParsingTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("42", (long)42)]
    [InlineData("-7", (long)-7)]
    [InlineData("3.14", 3.14)]
    [InlineData("1e3", 1000.0)]
    [InlineData("~", null)]
    [InlineData("null", null)]
    [InlineData("", null)]
    public void Plain_scalars_resolve_to_the_mdbase_json_data_model(string yaml, object? expected)
    {
        var doc = FrontmatterParser.ParseYamlMapping($"value: {yaml}");
        Assert.Equal(expected, doc["value"]);
    }

    [Theory]
    [InlineData("\"42\"")]
    [InlineData("'true'")]
    [InlineData("\"null\"")]
    public void Quoted_scalars_are_never_implicitly_typed(string yaml)
    {
        var doc = FrontmatterParser.ParseYamlMapping($"value: {yaml}");
        Assert.IsType<string>(doc["value"]);
    }

    [Theory]
    [InlineData(".nan")]
    [InlineData(".inf")]
    [InlineData("-.inf")]
    public void Non_finite_scalars_are_rejected(string yaml)
    {
        Assert.Throws<FrontmatterParseException>(() => FrontmatterParser.ParseYamlMapping($"value: {yaml}"));
    }

    [Fact]
    public void A_file_with_no_opening_delimiter_has_no_frontmatter()
    {
        var doc = FrontmatterParser.Parse("Just body text.\n");
        Assert.Empty(doc.Frontmatter);
        Assert.Equal("Just body text.\n", doc.Body);
    }

    [Fact]
    public void An_unterminated_frontmatter_block_is_rejected()
    {
        Assert.Throws<FrontmatterParseException>(() => FrontmatterParser.Parse("---\ntitle: A\nNo closing delimiter."));
    }

    [Fact]
    public void Empty_frontmatter_parses_to_an_empty_mapping()
    {
        var doc = FrontmatterParser.Parse("---\n---\nBody.\n");
        Assert.Empty(doc.Frontmatter);
    }
}

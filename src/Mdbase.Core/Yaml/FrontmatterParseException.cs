namespace Mdbase.Core.Yaml;

/// <summary>Raised when a YAML document cannot be converted to the mdbase JSON data model (spec Ch.03/06).</summary>
public sealed class FrontmatterParseException : Exception
{
    public FrontmatterParseException(string message) : base(message)
    {
    }

    public FrontmatterParseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

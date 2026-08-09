using System.Collections.Specialized;

namespace Mdbase.Core.Write;

/// <summary>
/// A minimal mutable write draft (spec Ch.09/Ch.12; #11 point 3): a fresh <see cref="OrderedDictionary"/>
/// clone of input (or patched) frontmatter, plus an <see cref="Old"/> raw-frontmatter snapshot
/// populated only on update. Lifecycle actions mutate <see cref="Fields"/> directly and in
/// declared order, so a later assignment to the same field naturally overwrites an earlier one.
/// </summary>
internal sealed class MdbWriteDraft
{
    private MdbWriteDraft(OrderedDictionary fields, OrderedDictionary? old)
    {
        Fields = fields;
        Old = old;
    }

    public OrderedDictionary Fields { get; }

    /// <summary>The pre-patch, pre-lifecycle raw frontmatter — populated only on update.</summary>
    public OrderedDictionary? Old { get; }

    public static MdbWriteDraft ForCreate(OrderedDictionary input) => new(Clone(input), null);

    public static MdbWriteDraft ForUpdate(OrderedDictionary patched, OrderedDictionary old) => new(Clone(patched), Clone(old));

    public static OrderedDictionary Clone(OrderedDictionary source)
    {
        var clone = new OrderedDictionary();
        foreach (System.Collections.DictionaryEntry entry in source)
        {
            clone[entry.Key] = entry.Value;
        }

        return clone;
    }
}

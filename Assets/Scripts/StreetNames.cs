using System;
using System.Collections.Generic;
using System.Text;

// ---------------------------------------------------------------------------
// Street names in text (night step 3; Mansoor, 27 Sept: "just put a placeholder
// for now and have it where i can always edit the street names").
//
// Text that mentions a street stores a token, "{street:west}", never the name
// itself. The name is looked up when the text is shown, from District streets
// (Assets/Data/Resources/District streets.asset, see DistrictStreets). So a
// renamed street is renamed everywhere at once, including notebook lines Ace
// wrote before the rename.
//
// No Unity types: Tests/HomeRules compiles this file.
// ---------------------------------------------------------------------------
public static class StreetNames
{
    const string Open = "{street:";

    private static readonly Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The token to put in text for a street: "{street:west}".</summary>
    public static string Token(string id) => Open + (id ?? "").Trim() + "}";

    public static void Clear() => names.Clear();

    public static void Set(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        names[id.Trim()] = name ?? "";
    }

    /// <summary>
    /// The street's name. A street with no name set yet reads "the west street",
    /// so text is never left with a raw token or a blank.
    /// </summary>
    public static string Name(string id)
    {
        string key = (id ?? "").Trim();
        if (names.TryGetValue(key, out string name) && !string.IsNullOrWhiteSpace(name)) return name.Trim();
        return key.Length == 0 ? "the street" : "the " + key + " street";
    }

    /// <summary>The text with every "{street:id}" replaced by that street's name.</summary>
    public static string Resolve(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf(Open, StringComparison.Ordinal) < 0) return text ?? "";
        var result = new StringBuilder(text.Length + 16);
        int at = 0;
        while (at < text.Length)
        {
            int start = text.IndexOf(Open, at, StringComparison.Ordinal);
            int end = start >= 0 ? text.IndexOf('}', start + Open.Length) : -1;
            if (start < 0 || end < 0) { result.Append(text, at, text.Length - at); break; }
            result.Append(text, at, start - at);
            result.Append(Name(text.Substring(start + Open.Length, end - start - Open.Length)));
            at = end + 1;
        }
        return result.ToString();
    }
}

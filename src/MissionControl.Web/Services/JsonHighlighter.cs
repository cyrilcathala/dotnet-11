using System.Net;
using System.Text.RegularExpressions;

namespace MissionControl.Web.Services;

/// <summary>Coloration JSON pour les réponses affichées.</summary>
public static partial class JsonHighlighter
{
    [GeneratedRegex("""(?<k>"(?:[^"\\]|\\.)*"(?=\s*:))|(?<s>"(?:[^"\\]|\\.)*")|(?<n>-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)|(?<l>\btrue\b|\bfalse\b|\bnull\b)""")]
    private static partial Regex Tokens();

    public static string ToHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        // On colore sur le texte brut, puis on encode morceau par morceau.
        var result = new System.Text.StringBuilder();
        var last = 0;
        foreach (Match m in Tokens().Matches(text))
        {
            result.Append(WebUtility.HtmlEncode(text[last..m.Index]));
            var cls = m.Groups["k"].Success ? "json-key" : m.Groups["s"].Success ? "json-str"
                : m.Groups["n"].Success ? "json-num" : "json-lit";
            result.Append($"<span class=\"{cls}\">{WebUtility.HtmlEncode(m.Value)}</span>");
            last = m.Index + m.Length;
        }
        result.Append(WebUtility.HtmlEncode(text[last..]));
        return result.ToString();
    }
}

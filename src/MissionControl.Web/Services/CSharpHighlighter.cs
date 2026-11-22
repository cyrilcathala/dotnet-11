using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MissionControl.Web.Services;

/// <summary>Coloration C# minimale, hors ligne (pas de CDN sur scène) : commentaires, chaînes, mots-clés, nombres.</summary>
public static partial class CSharpHighlighter
{
    private static readonly HashSet<string> Keywords =
    [
        "abstract", "as", "async", "await", "base", "bool", "break", "case", "catch", "char", "class", "closed",
        "const", "continue", "default", "double", "else", "enum", "extension", "false", "finally", "for", "foreach",
        "if", "in", "init", "int", "interface", "internal", "is", "long", "namespace", "new", "not", "null",
        "object", "or", "and", "out", "override", "params", "partial", "private", "protected", "public", "readonly",
        "record", "required", "return", "sealed", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "union", "using", "var", "void", "when", "where", "while", "with", "yield", "get", "set",
        "nameof", "lock", "event", "byte",
    ];

    [GeneratedRegex("""(?<c>//.*$)|(?<s>@?\$?"(?:[^"\\\n]|\\.)*"|'(?:[^'\\\n]|\\.)')|(?<d>#:\w+.*$|^\s*\[[A-Z][^\]]*\]|<[A-Z][A-Za-z]*>)|(?<w>\b[A-Za-z_]\w*\b)|(?<n>\b\d[\d_.]*\b)""",
        RegexOptions.Multiline)]
    private static partial Regex Tokens();

    public static string ToHtml(string line)
    {
        var html = new StringBuilder();
        var last = 0;
        foreach (Match m in Tokens().Matches(line))
        {
            html.Append(WebUtility.HtmlEncode(line[last..m.Index]));
            var text = WebUtility.HtmlEncode(m.Value);
            string? cls =
                m.Groups["c"].Success ? "tk-c" :
                m.Groups["s"].Success ? "tk-s" :
                m.Groups["d"].Success ? "tk-d" :
                m.Groups["n"].Success ? "tk-n" :
                m.Groups["w"].Success && Keywords.Contains(m.Value) ? "tk-k" :
                m.Groups["w"].Success && char.IsUpper(m.Value[0]) ? "tk-t" : null;
            html.Append(cls is null ? text : $"<span class=\"{cls}\">{text}</span>");
            last = m.Index + m.Length;
        }
        html.Append(WebUtility.HtmlEncode(line[last..]));
        return html.ToString();
    }
}

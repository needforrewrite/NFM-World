using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace NFMWorldLibrary.Util;

/// <summary>Resolves simple element, class and ID stylesheet rules for the vector loader.</summary>
public static partial class SvgStyles
{
    private sealed record Declaration(string Name, string Value, bool Important);
    private sealed record Rule(string Selector, int Specificity, Declaration[] Declarations);
    private sealed record Value(string Text, bool Important, int Specificity);

    public static string ReadWithInlineStyles(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            CloseInput = false
        });
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        Inline(document);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    public static void Inline(XDocument document)
    {
        var stylesheets = document.Descendants().Where(e => e.Name.LocalName == "style").ToArray();
        if (stylesheets.Length == 0) return;
        var rules = new List<Rule>();
        foreach (var stylesheet in stylesheets)
        {
            var css = Comments().Replace(stylesheet.Value, "");
            foreach (Match block in Rules().Matches(css))
            {
                var declarations = ParseDeclarations(block.Groups[2].Value);
                foreach (var selector in block.Groups[1].Value.Split(','))
                {
                    var trimmed = selector.Trim();
                    if (trimmed.Length == 0 || !Selector().IsMatch(trimmed)) continue;
                    var specificity = trimmed.Count(c => c == '#') * 100 + trimmed.Count(c => c == '.') * 10;
                    if (trimmed[0] is not ('.' or '#' or '*')) specificity++;
                    rules.Add(new Rule(trimmed, specificity, declarations));
                }
            }
        }

        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName == "style") continue;
            var values = new Dictionary<string, Value>(StringComparer.OrdinalIgnoreCase);
            void Apply(Declaration declaration, int specificity)
            {
                if (values.TryGetValue(declaration.Name, out var previous))
                {
                    if (previous.Important && !declaration.Important) return;
                    if (previous.Important == declaration.Important && previous.Specificity > specificity) return;
                }
                values[declaration.Name] = new Value(declaration.Value, declaration.Important, specificity);
            }

            foreach (var rule in rules)
                if (Matches(element, rule.Selector))
                    foreach (var declaration in rule.Declarations)
                        Apply(declaration, rule.Specificity);
            var inline = (string?)element.Attribute("style");
            if (inline is not null)
                foreach (var declaration in ParseDeclarations(inline))
                    Apply(declaration, 1000);
            if (values.Count > 0)
                element.SetAttributeValue("style", string.Join(';', values.Select(v => $"{v.Key}:{v.Value.Text}")));
        }
    }

    private static Declaration[] ParseDeclarations(string text)
    {
        var result = new List<Declaration>();
        foreach (var entry in text.Split(';'))
        {
            var separator = entry.IndexOf(':');
            if (separator <= 0) continue;
            var name = entry[..separator].Trim().ToLowerInvariant();
            var value = entry[(separator + 1)..].Trim();
            var important = Important().IsMatch(value);
            if (important) value = Important().Replace(value, "").TrimEnd();
            if (name.Length > 0 && value.Length > 0)
                result.Add(new Declaration(name, value, important));
        }
        return result.ToArray();
    }

    private static bool Matches(XElement element, string selector)
    {
        var tagLength = selector.IndexOfAny(['.', '#']);
        if (tagLength < 0) tagLength = selector.Length;
        var tag = selector[..tagLength];
        if (tag.Length > 0 && tag != "*" && tag != element.Name.LocalName) return false;
        var classes = ((string?)element.Attribute("class") ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (Match token in SelectorParts().Matches(selector[tagLength..]))
        {
            var name = token.Groups[2].Value;
            if (token.Groups[1].Value == "#")
            {
                if ((string?)element.Attribute("id") != name) return false;
            }
            else if (!classes.Contains(name, StringComparer.Ordinal)) return false;
        }
        return true;
    }

    [GeneratedRegex(@"/\*[\s\S]*?\*/")]
    private static partial Regex Comments();
    [GeneratedRegex(@"([^{}]+)\{([^{}]*)\}")]
    private static partial Regex Rules();
    [GeneratedRegex(@"^(?:[a-zA-Z_][\w-]*|\*)?(?:[.#][a-zA-Z_][\w-]*)*$")]
    private static partial Regex Selector();
    [GeneratedRegex(@"([.#])([a-zA-Z_][\w-]*)")]
    private static partial Regex SelectorParts();
    [GeneratedRegex(@"!\s*important\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Important();
}

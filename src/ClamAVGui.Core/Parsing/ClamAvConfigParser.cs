using System.Text;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;

namespace ClamAVGui.Core.Parsing;

public sealed class ClamAvConfigParser : IClamAvConfigParser
{
    public IReadOnlyList<ClamAvConfigDirective> Parse(string content)
    {
        var result = new List<ClamAvConfigDirective>();
        if (string.IsNullOrWhiteSpace(content)) return result;

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.Equals("Example", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1)
            {
                var name = parts[0].Trim();
                var value = parts.Length > 1 ? parts[1].Trim().Trim('"') : string.Empty;
                result.Add(new ClamAvConfigDirective(name, value));
            }
        }

        return result;
    }

    public string? GetDirectiveValue(IEnumerable<ClamAvConfigDirective> directives, string directiveName)
    {
        return directives.FirstOrDefault(d => string.Equals(d.Name, directiveName, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    public string SetOrUpdateDirective(string originalContent, string directiveName, string newValue)
    {
        var lines = (originalContent ?? string.Empty).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
        var found = false;
        var directiveLine = $"{directiveName} {newValue}";

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('#') || trimmed.StartsWith(';') || string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var parts = trimmed.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && string.Equals(parts[0], directiveName, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = directiveLine;
                found = true;
                break;
            }
        }

        if (!found)
        {
            lines.Add(directiveLine);
        }

        return string.Join(Environment.NewLine, lines);
    }
}

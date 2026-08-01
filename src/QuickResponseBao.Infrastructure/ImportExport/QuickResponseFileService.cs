using System.Text;
using System.Text.Json;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Infrastructure.ImportExport;

public sealed record ImportResult(int Total, int Succeeded, int Failed, IReadOnlyList<string> Errors);

public sealed class QuickResponseFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task ExportJsonAsync(string path, IEnumerable<QuickResponse> items, CancellationToken token = default) =>
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(items, JsonOptions), Encoding.UTF8, token);

    public async Task<IReadOnlyList<QuickResponse>> ImportJsonAsync(string path, CancellationToken token = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<QuickResponse>>(stream, JsonOptions, token) ?? [];
    }

    public async Task ExportCsvAsync(string path, IEnumerable<QuickResponse> items, CancellationToken token = default)
    {
        var rows = new List<string> { "Summary,Content,Keywords,Category,Language,IsEnabled" };
        rows.AddRange(items.Select(x => string.Join(',', Csv(x.Summary), Csv(x.Content),
            Csv(string.Join(';', x.Keywords)), Csv(x.Category), Csv(x.Language), x.IsEnabled)));
        await File.WriteAllLinesAsync(path, rows, new UTF8Encoding(true), token);
    }

    public async Task<IReadOnlyList<QuickResponse>> ImportCsvAsync(string path, CancellationToken token = default)
    {
        var text = await File.ReadAllTextAsync(path, token);
        var rows = ParseCsv(text); if (rows.Count == 0) return [];
        var headers = rows[0].Select((name, index) => (name: name.Trim(), index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
        string Field(IReadOnlyList<string> row, string name) => headers.TryGetValue(name, out var i) && i < row.Count ? row[i] : string.Empty;
        var result = new List<QuickResponse>();
        foreach (var row in rows.Skip(1))
        {
            var summary = Field(row, "Summary"); var content = Field(row, "Content");
            if (summary.Trim().Length < 2 || string.IsNullOrWhiteSpace(content)) continue;
            result.Add(new QuickResponse
            {
                Summary = summary.Trim(), Content = content,
                Keywords = Field(row, "Keywords").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Category = string.IsNullOrWhiteSpace(Field(row, "Category")) ? "General" : Field(row, "Category"),
                Language = string.IsNullOrWhiteSpace(Field(row, "Language")) ? "English" : Field(row, "Language"),
                IsEnabled = !bool.TryParse(Field(row, "IsEnabled"), out var enabled) || enabled
            });
        }
        return result;
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { row.Add(field.ToString()); field.Clear(); }
            else if ((c == '\r' || c == '\n') && !quoted)
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear(); if (row.Any(x => x.Length > 0)) rows.Add(row); row = [];
            }
            else field.Append(c);
        }
        row.Add(field.ToString()); if (row.Any(x => x.Length > 0)) rows.Add(row); return rows;
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

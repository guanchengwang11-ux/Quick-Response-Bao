using System.Text;
using System.Text.Json;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Infrastructure.ImportExport;

public sealed record ImportResult(int Total, int Succeeded, int Failed, IReadOnlyList<string> Errors);

public sealed class QuickResponseFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public async Task ExportJsonAsync(string path, IEnumerable<QuickResponse> items, CancellationToken token = default) =>
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(items, JsonOptions), Encoding.UTF8, token);

    public async Task<IReadOnlyList<QuickResponse>> ImportJsonAsync(string path, CancellationToken token = default)
        => (await ImportJsonOutcomeAsync(path, token)).Items.Select(x => x.Response).ToList();

    public async Task ExportCsvAsync(string path, IEnumerable<QuickResponse> items, CancellationToken token = default)
    {
        var rows = new List<string> { "Summary,Content,Keywords,Category,Language,IsEnabled,SortOrder" };
        rows.AddRange(items.Select(x => string.Join(',', Csv(x.Summary), Csv(x.Content),
            Csv(string.Join(';', x.Keywords)), Csv(x.Category), Csv(x.Language), x.IsEnabled, x.SortOrder)));
        await File.WriteAllLinesAsync(path, rows, new UTF8Encoding(true), token);
    }

    public async Task<IReadOnlyList<QuickResponse>> ImportCsvAsync(string path, CancellationToken token = default)
        => (await ImportCsvOutcomeAsync(path, token)).Items.Select(x => x.Response).ToList();

    public Task<ImportPreview> PreviewJsonAsync(string path, int maximumPreviewRows = 20, CancellationToken token = default) =>
        PreviewJsonCoreAsync(path, maximumPreviewRows, token);

    public Task<ExcelImportOutcome> ImportJsonOutcomeAsync(string path, CancellationToken token = default) =>
        ImportJsonOutcomeCoreAsync(path, null, token);

    public Task<ExcelImportOutcome> ImportJsonOutcomeAsync(string path, ImportFieldMapping mapping, CancellationToken token = default) =>
        ImportJsonOutcomeCoreAsync(path, mapping, token);

    private static async Task<ExcelImportOutcome> ImportJsonOutcomeCoreAsync(string path, ImportFieldMapping? requestedMapping, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("JSON import root must be an array.");
        var items = new List<ExcelImportItem>(); var failures = new List<ImportFailure>(); var rowNumber = 0;
        foreach (var element in document.RootElement.EnumerateArray())
        {
            token.ThrowIfCancellationRequested(); rowNumber++;
            try
            {
                if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("ImportRowMustBeObject");
                var properties = element.EnumerateObject().ToList();
                var mapping = requestedMapping ?? QuickResponseImportParser.SuggestMapping(properties.Select(x => x.Name));
                EnsureRequired(mapping);
                string Field(QuickResponseField field)
                {
                    if (mapping.Get(field) is not { } name) return string.Empty;
                    var property = properties.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    return string.IsNullOrEmpty(property.Name) ? string.Empty : JsonValue(property.Value);
                }
                var includesSortOrder = mapping.Get(QuickResponseField.SortOrder) is not null;
                items.Add(new ExcelImportItem(rowNumber, QuickResponseImportParser.Create(Field(QuickResponseField.Summary),
                    Field(QuickResponseField.Content), Field(QuickResponseField.Keywords), Field(QuickResponseField.Category),
                    Field(QuickResponseField.Language), Field(QuickResponseField.IsEnabled), Field(QuickResponseField.SortOrder), includesSortOrder), includesSortOrder));
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException)
            { failures.Add(new ImportFailure(rowNumber, ex.Message)); }
        }
        return new ExcelImportOutcome(items, new DetailedImportResult(rowNumber, items.Count, failures.Count, 0, failures));
    }

    public Task<ImportPreview> PreviewCsvAsync(string path, int maximumPreviewRows = 20, CancellationToken token = default) =>
        PreviewCsvCoreAsync(path, maximumPreviewRows, token);

    public Task<ExcelImportOutcome> ImportCsvOutcomeAsync(string path, CancellationToken token = default) =>
        ImportCsvOutcomeCoreAsync(path, null, token);

    public Task<ExcelImportOutcome> ImportCsvOutcomeAsync(string path, ImportFieldMapping mapping, CancellationToken token = default) =>
        ImportCsvOutcomeCoreAsync(path, mapping, token);

    private static async Task<ExcelImportOutcome> ImportCsvOutcomeCoreAsync(string path, ImportFieldMapping? requestedMapping, CancellationToken token)
    {
        var rows = ParseCsv(await File.ReadAllTextAsync(path, token));
        if (rows.Count == 0) return new ExcelImportOutcome([], new DetailedImportResult(0, 0, 0, 0, []));
        var headers = rows[0]; var mapping = requestedMapping ?? QuickResponseImportParser.SuggestMapping(headers); EnsureRequired(mapping);
        var columns = headers.Select((name, index) => (normalized: QuickResponseImportParser.NormalizeHeader(name), index))
            .ToDictionary(x => x.normalized, x => x.index, StringComparer.Ordinal);
        string Field(IReadOnlyList<string> row, QuickResponseField field) => mapping.Get(field) is { } name &&
            columns.TryGetValue(QuickResponseImportParser.NormalizeHeader(name), out var index) && index < row.Count ? row[index] : string.Empty;
        var items = new List<ExcelImportItem>(); var failures = new List<ImportFailure>();
        for (var index = 1; index < rows.Count; index++)
        {
            token.ThrowIfCancellationRequested(); var row = rows[index]; var rowNumber = index + 1;
            try
            {
                var includesSortOrder = mapping.Get(QuickResponseField.SortOrder) is not null;
                items.Add(new ExcelImportItem(rowNumber, QuickResponseImportParser.Create(Field(row, QuickResponseField.Summary),
                    Field(row, QuickResponseField.Content), Field(row, QuickResponseField.Keywords), Field(row, QuickResponseField.Category),
                    Field(row, QuickResponseField.Language), Field(row, QuickResponseField.IsEnabled), Field(row, QuickResponseField.SortOrder), includesSortOrder), includesSortOrder));
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException)
            { failures.Add(new ImportFailure(rowNumber, ex.Message)); }
        }
        return new ExcelImportOutcome(items, new DetailedImportResult(rows.Count - 1, items.Count, failures.Count, 0, failures));
    }

    private static async Task<ImportPreview> PreviewCsvCoreAsync(string path, int maximumPreviewRows, CancellationToken token)
    {
        var rows = ParseCsv(await File.ReadAllTextAsync(path, token));
        if (rows.Count == 0) return new ImportPreview([], [], 0);
        var headers = rows[0];
        var previewRows = rows.Skip(1).Take(maximumPreviewRows).Select((row, index) => new ImportPreviewRow(index + 2,
            headers.Select((header, column) => (header, value: column < row.Count ? row[column] : string.Empty))
                .ToDictionary(x => x.header, x => x.value, StringComparer.OrdinalIgnoreCase))).ToList();
        return new ImportPreview(headers, previewRows, Math.Max(0, rows.Count - 1));
    }

    private static async Task<ImportPreview> PreviewJsonCoreAsync(string path, int maximumPreviewRows, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("JSON import root must be an array.");
        var objects = document.RootElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList();
        var headers = objects.SelectMany(x => x.EnumerateObject().Select(p => p.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var previewRows = objects.Take(maximumPreviewRows).Select((item, index) =>
        {
            var properties = item.EnumerateObject().ToDictionary(x => x.Name, x => JsonValue(x.Value), StringComparer.OrdinalIgnoreCase);
            return new ImportPreviewRow(index + 1, headers.ToDictionary(x => x, x => properties.TryGetValue(x, out var value) ? value : string.Empty, StringComparer.OrdinalIgnoreCase));
        }).ToList();
        return new ImportPreview(headers, previewRows, document.RootElement.GetArrayLength());
    }

    private static string JsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => string.Empty,
        _ => value.GetRawText()
    };

    private static void EnsureRequired(ImportFieldMapping mapping)
    {
        if (mapping.Get(QuickResponseField.Summary) is null || mapping.Get(QuickResponseField.Content) is null)
            throw new InvalidDataException("RequiredMapping");
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

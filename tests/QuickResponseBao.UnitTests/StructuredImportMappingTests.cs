using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.ImportExport;

namespace QuickResponseBao.UnitTests;

public sealed class StructuredImportMappingTests
{
    [Fact]
    public async Task CsvPreview_AllowsCustomFieldMapping()
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        try
        {
            await File.WriteAllTextAsync(path, "Subject,Message,Tags\r\nPayment update,Your payment is under review.,payment;review");
            var service = new QuickResponseFileService();
            var preview = await service.PreviewCsvAsync(path);
            var result = await service.ImportCsvOutcomeAsync(path, Mapping("Subject", "Message", "Tags"));
            Assert.Equal(new[] { "Subject", "Message", "Tags" }, preview.Headers);
            Assert.Equal("Payment update", Assert.Single(result.Items).Response.Summary);
            Assert.Equal(new[] { "payment", "review" }, result.Items[0].Response.Keywords);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task JsonPreview_AllowsCustomFieldMapping()
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        try
        {
            await File.WriteAllTextAsync(path, "[{\"Subject\":\"Refund update\",\"Message\":\"Your refund is confirmed.\",\"Tags\":\"refund,confirmed\"}]");
            var service = new QuickResponseFileService();
            var preview = await service.PreviewJsonAsync(path);
            var result = await service.ImportJsonOutcomeAsync(path, Mapping("Subject", "Message", "Tags"));
            Assert.Contains("Subject", preview.Headers);
            Assert.Equal("Your refund is confirmed.", Assert.Single(result.Items).Response.Content);
            Assert.Equal(2, result.Items[0].Response.Keywords.Count);
        }
        finally { File.Delete(path); }
    }

    private static ImportFieldMapping Mapping(string summary, string content, string keywords) => new(
        new Dictionary<QuickResponseField, string>
        {
            [QuickResponseField.Summary] = summary,
            [QuickResponseField.Content] = content,
            [QuickResponseField.Keywords] = keywords
        });
}

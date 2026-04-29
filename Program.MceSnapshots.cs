using System.Text;
using System.Text.Json;

internal static partial class Program
{
    private sealed record MceDataObject(
        string? Name,
        string? Initial,
        string? Unit,
        string? Note,
        int? DataType);

    private sealed record MceSnapshot(
        string ExportDir,
        MceDataObject[] DataObjects,
        string BlobText)
    {
        public MceDataObject[] FindDataObjects(string name)
            => DataObjects
                .Where(row => string.Equals(row.Name, name, StringComparison.Ordinal))
                .ToArray();

        public int CountBlobToken(string token)
            => CountLiteralOccurrences(BlobText, token);
    }

    private sealed record TokenDelta(
        string Token,
        int BeforeCount,
        int AfterCount,
        int Delta,
        bool Increased,
        bool PresentAfter);

    private static MceSnapshot ExportMceSnapshot(string project, string outDir)
    {
        MceExporter.Export(project, outDir);
        return LoadMceSnapshot(outDir);
    }

    private static MceSnapshot LoadMceSnapshot(string exportDir)
    {
        var dataObjects = LoadMceDataObjects(Path.Combine(exportDir, "data.json"));
        var blobPath = Path.Combine(exportDir, "blob_strings.json");
        var blobText = File.Exists(blobPath) ? File.ReadAllText(blobPath, Encoding.UTF8) : "";
        return new MceSnapshot(exportDir, dataObjects, blobText);
    }

    private static MceDataObject[] LoadMceDataObjects(string dataJsonPath)
    {
        if (!File.Exists(dataJsonPath)) return Array.Empty<MceDataObject>();
        using var doc = JsonDocument.Parse(File.ReadAllText(dataJsonPath, Encoding.UTF8));
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<MceDataObject>();

        return doc.RootElement.EnumerateArray()
            .Select(row => new MceDataObject(
                JsonString(row, "strName"),
                JsonString(row, "strInitValue"),
                JsonString(row, "strUnit"),
                JsonString(row, "strNote"),
                JsonInt(row, "iDataType")))
            .ToArray();
    }

    private static string? JsonString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.GetRawText();
    }

    private static int? JsonInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value)) return value;
        return int.TryParse(JsonString(element, name), out var parsed) ? parsed : null;
    }

    private static int CountLiteralOccurrences(string text, string token)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token)) return 0;
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static TokenDelta[] BuildTokenDeltas(MceSnapshot before, MceSnapshot after, IEnumerable<string> tokens)
        => tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .Select(token =>
            {
                var beforeCount = before.CountBlobToken(token);
                var afterCount = after.CountBlobToken(token);
                return new TokenDelta(token, beforeCount, afterCount, afterCount - beforeCount,
                    afterCount > beforeCount, afterCount > 0);
            })
            .ToArray();

    private static int? RealtimeTypeCode(string rawType)
    {
        return rawType.Trim().ToLowerInvariant() switch
        {
            "switch" or "bool" or "boolean" or "bit" or "开关" => 1,
            "numeric" or "number" or "float" or "int" or "数值" => 2,
            _ => null
        };
    }

    private static bool SameOptionalText(string? actual, string expected)
        => string.Equals(actual ?? "", expected ?? "", StringComparison.Ordinal);
}

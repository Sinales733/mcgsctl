using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static partial class Program
{
    private static int MceNormalizedDiff(string[] args)
    {
        var baseline = FullPath(Required(args, "--baseline"));
        var candidate = FullPath(Required(args, "--candidate"));
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);

        if (!Directory.Exists(baseline))
            return Fail("Baseline export directory does not exist: " + baseline);
        if (!Directory.Exists(candidate))
            return Fail("Candidate export directory does not exist: " + candidate);

        var result = WriteMceNormalizedDiff(baseline, candidate, outDir);
        Console.WriteLine("mce normalized-diff: " + outDir);
        return result.Status == "PASS" ? 0 : 2;
    }

    private static MceNormalizedDiffResult WriteMceNormalizedDiff(string baseline, string candidate, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var files = new[]
        {
            "summary.json",
            "schema.json",
            "data.json",
            "blob_strings.json",
            "blob_geometry.json"
        };
        var comparisons = new List<MceNormalizedFileDiff>();
        var parseErrors = new List<string>();
        foreach (var file in files)
        {
            try
            {
                comparisons.Add(CompareMceExportFile(baseline, candidate, file));
            }
            catch (Exception ex)
            {
                parseErrors.Add(file + ": " + ex.Message);
                comparisons.Add(new MceNormalizedFileDiff
                {
                    File = file,
                    ExistsInBaseline = File.Exists(Path.Combine(baseline, file)),
                    ExistsInCandidate = File.Exists(Path.Combine(candidate, file)),
                    Equivalent = false,
                    Error = ex.Message
                });
            }
        }

        var result = new MceNormalizedDiffResult
        {
            Status = parseErrors.Count == 0 ? "PASS" : "UNKNOWN",
            Baseline = baseline,
            Candidate = candidate,
            CreatedAt = DateTimeOffset.Now.ToString("O"),
            Equivalent = parseErrors.Count == 0 && comparisons.All(c => c.Equivalent),
            ChangedFileCount = comparisons.Count(c => !c.Equivalent),
            ParseErrors = parseErrors,
            Files = comparisons,
            EditorContextOnly = IsEditorContextOnlyDiff(comparisons),
            Note = "summary.json project path is normalized away; _source.MCE is intentionally ignored. Entry diffs are sanitized hashes/counts, not raw private blob payloads."
        };
        File.WriteAllText(Path.Combine(outDir, "mce-normalized-diff.json"),
            JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
        return result;
    }

    private static bool IsEditorContextOnlyDiff(List<MceNormalizedFileDiff> comparisons)
    {
        var changed = comparisons.Where(c => !c.Equivalent).ToArray();
        return changed.Length == 1 &&
               changed[0].File.Equals("blob_strings.json", StringComparison.OrdinalIgnoreCase) &&
               changed[0].EntryDiffs.Count > 0 &&
               changed[0].EntryDiffs.All(e => e.Key.StartsWith("System|lbContext|", StringComparison.OrdinalIgnoreCase));
    }

    private static MceNormalizedFileDiff CompareMceExportFile(string baseline, string candidate, string file)
    {
        var baselinePath = Path.Combine(baseline, file);
        var candidatePath = Path.Combine(candidate, file);
        var result = new MceNormalizedFileDiff
        {
            File = file,
            ExistsInBaseline = File.Exists(baselinePath),
            ExistsInCandidate = File.Exists(candidatePath)
        };
        if (!result.ExistsInBaseline || !result.ExistsInCandidate)
        {
            result.Equivalent = result.ExistsInBaseline == result.ExistsInCandidate;
            return result;
        }

        using var baselineDoc = JsonDocument.Parse(File.ReadAllText(baselinePath, Encoding.UTF8));
        using var candidateDoc = JsonDocument.Parse(File.ReadAllText(candidatePath, Encoding.UTF8));
        var baselineCanonical = CanonicalJson(baselineDoc.RootElement, file);
        var candidateCanonical = CanonicalJson(candidateDoc.RootElement, file);
        result.BaselineSha256 = Sha256Text(baselineCanonical);
        result.CandidateSha256 = Sha256Text(candidateCanonical);
        result.Equivalent = result.BaselineSha256.Equals(result.CandidateSha256, StringComparison.OrdinalIgnoreCase);
        if (!result.Equivalent && baselineDoc.RootElement.ValueKind == JsonValueKind.Array &&
            candidateDoc.RootElement.ValueKind == JsonValueKind.Array)
        {
            result.EntryDiffs = DiffJsonArrayEntries(baselineDoc.RootElement, candidateDoc.RootElement, file);
            if (result.EntryDiffs.Count == 0)
                result.Equivalent = true;
        }
        return result;
    }

    private static List<MceNormalizedEntryDiff> DiffJsonArrayEntries(JsonElement baseline, JsonElement candidate, string file)
    {
        var left = baseline.EnumerateArray()
            .Select((e, i) => (Key: MceEntryKey(e, i), Element: e, Hash: Sha256Text(CanonicalJson(e, file))))
            .ToDictionary(e => e.Key, e => e, StringComparer.OrdinalIgnoreCase);
        var right = candidate.EnumerateArray()
            .Select((e, i) => (Key: MceEntryKey(e, i), Element: e, Hash: Sha256Text(CanonicalJson(e, file))))
            .ToDictionary(e => e.Key, e => e, StringComparer.OrdinalIgnoreCase);

        var keys = left.Keys.Concat(right.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        var diffs = new List<MceNormalizedEntryDiff>();
        foreach (var key in keys)
        {
            var hasLeft = left.TryGetValue(key, out var l);
            var hasRight = right.TryGetValue(key, out var r);
            if (hasLeft && hasRight && l.Hash.Equals(r.Hash, StringComparison.OrdinalIgnoreCase))
                continue;
            if (hasLeft && hasRight && MceEntriesSemanticallyEquivalent(file, l.Element, r.Element))
                continue;
            diffs.Add(new MceNormalizedEntryDiff
            {
                Key = key,
                Status = hasLeft && hasRight ? "changed" : hasLeft ? "removed" : "added",
                Baseline = hasLeft ? MceEntrySummary(l.Element, l.Hash) : null,
                Candidate = hasRight ? MceEntrySummary(r.Element, r.Hash) : null,
                StringDiffs = hasLeft && hasRight ? DiffMceStringArrays(l.Element, r.Element) : new()
            });
        }
        return diffs;
    }

    private static bool MceEntriesSemanticallyEquivalent(string file, JsonElement baseline, JsonElement candidate)
    {
        if (!file.Equals("blob_geometry.json", StringComparison.OrdinalIgnoreCase))
            return false;
        return MceGeometrySignature(baseline).Equals(MceGeometrySignature(candidate), StringComparison.Ordinal);
    }

    private static string MceGeometrySignature(JsonElement element)
    {
        var parts = new List<string>
        {
            "table=" + (MceJsonString(element, "table") ?? ""),
            "column=" + (MceJsonString(element, "column") ?? ""),
            "rowKey=" + (MceJsonString(element, "rowKey") ?? ""),
            "rowLabelSha256=" + (MceJsonString(element, "rowLabelSha256") ?? "")
        };
        AddSignatureArray(parts, element, "classOccurrences", e =>
            (MceJsonString(e, "className") ?? ""));
        AddSignatureArray(parts, element, "candidateRectangles", e =>
            string.Join("|",
                MceJsonString(e, "encoding") ?? "",
                MceJsonString(e, "pattern") ?? "",
                MceJsonInt(e, "x")?.ToString() ?? "",
                MceJsonInt(e, "y")?.ToString() ?? "",
                MceJsonInt(e, "width")?.ToString() ?? "",
                MceJsonInt(e, "height")?.ToString() ?? ""));
        AddSignatureArray(parts, element, "textAnchors", e =>
            string.Join("|",
                MceJsonString(e, "encoding") ?? "",
                MceJsonInt(e, "byteLength")?.ToString() ?? "",
                MceJsonInt(e, "charLength")?.ToString() ?? "",
                MceJsonString(e, "sha256") ?? ""));
        return string.Join("\n", parts);
    }

    private static void AddSignatureArray(List<string> parts, JsonElement element, string property, Func<JsonElement, string> selector)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            parts.Add(property + ":");
            return;
        }

        var values = array.EnumerateArray()
            .Select(selector)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();
        parts.Add(property + ":" + string.Join(";", values));
    }

    private static List<MceNormalizedStringDiff> DiffMceStringArrays(JsonElement baseline, JsonElement candidate)
    {
        if (!baseline.TryGetProperty("strings", out var baselineStrings) ||
            !candidate.TryGetProperty("strings", out var candidateStrings) ||
            baselineStrings.ValueKind != JsonValueKind.Array ||
            candidateStrings.ValueKind != JsonValueKind.Array)
        {
            return new List<MceNormalizedStringDiff>();
        }

        var left = baselineStrings.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.GetRawText())
            .ToArray();
        var right = candidateStrings.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.GetRawText())
            .ToArray();
        var diffs = new List<MceNormalizedStringDiff>();
        var count = Math.Max(left.Length, right.Length);
        for (var i = 0; i < count; i++)
        {
            var hasLeft = i < left.Length;
            var hasRight = i < right.Length;
            var l = hasLeft ? left[i] : "";
            var r = hasRight ? right[i] : "";
            if (hasLeft && hasRight && string.Equals(l, r, StringComparison.Ordinal))
                continue;
            diffs.Add(new MceNormalizedStringDiff
            {
                Index = i,
                Status = hasLeft && hasRight ? "changed" : hasLeft ? "removed" : "added",
                BaselineSha256 = hasLeft ? Sha256Text(l) : "",
                CandidateSha256 = hasRight ? Sha256Text(r) : "",
                BaselineUtf16Length = hasLeft ? l.Length : null,
                CandidateUtf16Length = hasRight ? r.Length : null,
                BaselineUtf8Bytes = hasLeft ? Encoding.UTF8.GetByteCount(l) : null,
                CandidateUtf8Bytes = hasRight ? Encoding.UTF8.GetByteCount(r) : null
            });
        }
        return diffs;
    }

    private static string MceEntryKey(JsonElement element, int index)
    {
        var table = MceJsonString(element, "table");
        var column = MceJsonString(element, "column");
        var rowKey = MceJsonString(element, "rowKey");
        if (!string.IsNullOrWhiteSpace(table) || !string.IsNullOrWhiteSpace(column) || !string.IsNullOrWhiteSpace(rowKey))
            return $"{table}|{column}|{rowKey}";
        var name = MceJsonString(element, "name");
        return string.IsNullOrWhiteSpace(name) ? "index:" + index.ToString("D6") : "name:" + name;
    }

    private static object MceEntrySummary(JsonElement element, string canonicalHash)
    {
        return new
        {
            sha256 = MceJsonString(element, "sha256") ?? canonicalHash,
            canonicalSha256 = canonicalHash,
            bytes = MceJsonInt(element, "bytes"),
            rowLabelSha256 = MceJsonString(element, "rowLabelSha256"),
            stringCount = MceJsonArrayCount(element, "strings"),
            classOccurrenceCount = MceJsonArrayCount(element, "classOccurrences"),
            candidateRectangleCount = MceJsonArrayCount(element, "candidateRectangles"),
            textAnchorCount = MceJsonArrayCount(element, "textAnchors")
        };
    }

    private static string CanonicalJson(JsonElement element, string file)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            WriteCanonicalJson(writer, element, file, root: true);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element, string file, bool root)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject()
                             .Where(p => !(root && file.Equals("summary.json", StringComparison.OrdinalIgnoreCase) &&
                                           p.NameEquals("project")))
                             .OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(prop.Name);
                    WriteCanonicalJson(writer, prop.Value, file, root: false);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonicalJson(writer, item, file, root: false);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string? MceJsonString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? MceJsonInt(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out var parsed)
            ? parsed
            : null;
    }

    private static int? MceJsonArrayCount(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : null;
    }

    private sealed class MceNormalizedFileDiff
    {
        [JsonPropertyName("file")]
        public string File { get; set; } = "";
        [JsonPropertyName("existsInBaseline")]
        public bool ExistsInBaseline { get; set; }
        [JsonPropertyName("existsInCandidate")]
        public bool ExistsInCandidate { get; set; }
        [JsonPropertyName("equivalent")]
        public bool Equivalent { get; set; }
        [JsonPropertyName("baselineSha256")]
        public string BaselineSha256 { get; set; } = "";
        [JsonPropertyName("candidateSha256")]
        public string CandidateSha256 { get; set; } = "";
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";
        [JsonPropertyName("entryDiffs")]
        public List<MceNormalizedEntryDiff> EntryDiffs { get; set; } = new();
    }

    private sealed class MceNormalizedDiffResult
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
        [JsonPropertyName("baseline")]
        public string Baseline { get; set; } = "";
        [JsonPropertyName("candidate")]
        public string Candidate { get; set; } = "";
        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = "";
        [JsonPropertyName("equivalent")]
        public bool Equivalent { get; set; }
        [JsonPropertyName("changedFileCount")]
        public int ChangedFileCount { get; set; }
        [JsonPropertyName("editorContextOnly")]
        public bool EditorContextOnly { get; set; }
        [JsonPropertyName("parseErrors")]
        public List<string> ParseErrors { get; set; } = new();
        [JsonPropertyName("files")]
        public List<MceNormalizedFileDiff> Files { get; set; } = new();
        [JsonPropertyName("note")]
        public string Note { get; set; } = "";
    }

    private sealed class MceNormalizedEntryDiff
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
        [JsonPropertyName("baseline")]
        public object? Baseline { get; set; }
        [JsonPropertyName("candidate")]
        public object? Candidate { get; set; }
        [JsonPropertyName("stringDiffs")]
        public List<MceNormalizedStringDiff> StringDiffs { get; set; } = new();
    }

    private sealed class MceNormalizedStringDiff
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
        [JsonPropertyName("baselineSha256")]
        public string BaselineSha256 { get; set; } = "";
        [JsonPropertyName("candidateSha256")]
        public string CandidateSha256 { get; set; } = "";
        [JsonPropertyName("baselineUtf16Length")]
        public int? BaselineUtf16Length { get; set; }
        [JsonPropertyName("candidateUtf16Length")]
        public int? CandidateUtf16Length { get; set; }
        [JsonPropertyName("baselineUtf8Bytes")]
        public int? BaselineUtf8Bytes { get; set; }
        [JsonPropertyName("candidateUtf8Bytes")]
        public int? CandidateUtf8Bytes { get; set; }
    }
}

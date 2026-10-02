using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace LocalisationStudio.Core;

public static class DocumentFiles
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public static TranslationDocument Load(string path) =>
        Parse(File.ReadAllText(path, Encoding.UTF8), Path.GetExtension(path));

    public static TranslationDocument Parse(string text, string extension)
    {
        var document = extension.ToLowerInvariant() switch
        {
            ".json" => JsonSerializer.Deserialize<TranslationDocument>(text.TrimStart('\uFEFF'), JsonOptions)
                ?? throw new InvalidDataException("Le JSON ne contient pas de document."),
            ".csv" => CsvCodec.Read(text),
            ".xml" => ReadXml(text),
            _ => throw new InvalidDataException("Format attendu : CSV, JSON ou XML.")
        };
        document.Validate();
        return document;
    }

    public static string Serialize(TranslationDocument document, string extension)
    {
        document.Validate();
        return extension.ToLowerInvariant() switch
        {
            ".json" => JsonSerializer.Serialize(document, JsonOptions),
            ".csv" => CsvCodec.Write(document),
            ".xml" => WriteXml(document),
            _ => throw new InvalidDataException("Format attendu : CSV, JSON ou XML.")
        };
    }

    private static TranslationDocument ReadXml(string text)
    {
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var root = XDocument.Load(reader, LoadOptions.PreserveWhitespace).Root;
        if (root?.Name != "localization" || (string?)root.Attribute("version") != "1")
            throw new InvalidDataException("XML : racine localization et version=1 attendues.");
        var languageNodes = root.Element("languages")
            ?? throw new InvalidDataException("XML : élément languages absent.");
        var entries = root.Element("entries")
            ?? throw new InvalidDataException("XML : élément entries absent.");
        var document = new TranslationDocument
        { Languages = languageNodes.Elements("language").Select(e => (string?)e.Attribute("code") ?? "").ToList() };
        foreach (var node in entries.Elements("entry"))
        {
            var entry = new TranslationEntry { Id = (string?)node.Attribute("id") ?? "" };
            foreach (var value in node.Elements("value"))
                if (!entry.Values.TryAdd((string?)value.Attribute("language") ?? "", value.Value))
                    throw new InvalidDataException("XML : traduction dupliquée pour une langue.");
            document.Entries.Add(entry);
        }
        return document;
    }

    private static string WriteXml(TranslationDocument document)
    {
        var root = new XElement("localization", new XAttribute("version", 1),
            new XElement("languages", document.Languages.Select(l =>
                new XElement("language", new XAttribute("code", l)))),
            new XElement("entries", document.Entries.Select(e =>
                new XElement("entry", new XAttribute("id", e.Id), document.Languages.Select(l =>
                    new XElement("value", new XAttribute("language", l), document.Value(e, l)))))));
        var buffer = new StringBuilder();
        using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings
        { Indent = true, OmitXmlDeclaration = true, NewLineHandling = NewLineHandling.Entitize }))
            root.Save(writer);
        return buffer.ToString();
    }

    // Prepare all bytes before replacing files; restore previous files on ordinary I/O failure.
    // This is not a filesystem transaction in case of power loss.
    public static void WriteBatch(IReadOnlyDictionary<string, string> files)
    {
        var staged = new List<(string Path, string Temp, byte[]? Previous)>();
        var written = new List<(string Path, byte[]? Previous)>();
        try
        {
            foreach (var pair in files)
            {
                string path = Path.GetFullPath(pair.Key);
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                var previous = File.Exists(path) ? File.ReadAllBytes(path) : null;
                staged.Add((path, temp, previous));
                // BOM helps Excel recognize accented characters in CSV.
                File.WriteAllText(temp, pair.Value, new UTF8Encoding(Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)));
            }
            foreach (var item in staged)
            {
                File.Move(item.Temp, item.Path, true);
                written.Add((item.Path, item.Previous));
            }
        }
        catch (Exception original)
        {
            var failures = new List<Exception> { original };
            foreach (var item in written.AsEnumerable().Reverse())
            {
                try
                {
                    if (item.Previous is null) File.Delete(item.Path);
                    else File.WriteAllBytes(item.Path, item.Previous);
                }
                catch (Exception restore) { failures.Add(restore); }
            }
            if (failures.Count > 1) throw new AggregateException("Échec d’écriture et de restauration de certains fichiers.", failures);
            throw;
        }
        finally
        {
            foreach (var item in staged)
                try { File.Delete(item.Temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}

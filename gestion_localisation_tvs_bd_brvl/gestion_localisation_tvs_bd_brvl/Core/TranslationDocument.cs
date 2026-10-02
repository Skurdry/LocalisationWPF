using System.Xml;

namespace LocalisationStudio.Core;

public sealed class TranslationEntry
{
    public string Id { get; set; } = "";
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
}

public sealed class TranslationDocument
{
    public int Version { get; set; } = 1;
    public List<string> Languages { get; set; } = ["fr", "en"];
    public List<TranslationEntry> Entries { get; set; } = [];

    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("Version de document non prise en charge.");
        if (Languages is null || Languages.Count == 0)
            throw new InvalidDataException("Le document doit contenir au moins une langue.");
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in Languages)
        {
            if (string.IsNullOrWhiteSpace(language) || language != language.Trim() ||
                language.Equals("ID", StringComparison.OrdinalIgnoreCase) || !languages.Add(language))
                throw new InvalidDataException("Les langues doivent être uniques, non vides et différentes de ID.");
            VerifyText(language);
        }
        if (Entries is null) throw new InvalidDataException("La liste des traductions est absente.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
                throw new InvalidDataException("Chaque ligne doit avoir un ID non vide et unique (sensible à la casse).");
            VerifyText(entry.Id);
            if (entry.Values is null) throw new InvalidDataException($"Valeurs absentes : {entry.Id}.");
            foreach (var pair in entry.Values)
            {
                if (!Languages.Contains(pair.Key, StringComparer.Ordinal) || pair.Value is null)
                    throw new InvalidDataException($"Langue inconnue ou valeur nulle : {entry.Id} / {pair.Key}.");
                VerifyText(pair.Value);
            }
        }
    }

    private static void VerifyText(string value)
    {
        try { XmlConvert.VerifyXmlChars(value); }
        catch (XmlException ex)
        { throw new InvalidDataException("Un texte contient un caractère incompatible avec XML 1.0.", ex); }
    }

    public string Value(TranslationEntry entry, string language) =>
        entry.Values.TryGetValue(language, out var value) ? value : "";
}

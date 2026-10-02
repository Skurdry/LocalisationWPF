using System.Text;

namespace LocalisationStudio.Core;

internal static class CsvCodec
{
    public static TranslationDocument Read(string text)
    {
        text = text.TrimStart('\uFEFF');
        char delimiter = DetectDelimiter(text);
        var rows = Parse(text, delimiter);
        if (rows.Count == 0 || rows[0].Count < 2 || rows[0][0] != "ID")
            throw new InvalidDataException("CSV : en-tête attendu : ID;fr;en (au moins une langue).");
        var document = new TranslationDocument { Languages = rows[0].Skip(1).ToList() };
        foreach (var row in rows.Skip(1))
        {
            if (row.Count != rows[0].Count)
                throw new InvalidDataException($"CSV : {row.Count} colonnes trouvées, {rows[0].Count} attendues.");
            var entry = new TranslationEntry { Id = row[0] };
            for (int i = 0; i < document.Languages.Count; i++)
                if (!entry.Values.TryAdd(document.Languages[i], row[i + 1]))
                    throw new InvalidDataException("CSV : langue dupliquée.");
            document.Entries.Add(entry);
        }
        return document;
    }

    public static string Write(TranslationDocument document)
    {
        var text = new StringBuilder();
        void Row(IEnumerable<string> values) => text.AppendJoin(';', values.Select(Escape)).Append("\r\n");
        Row(new[] { "ID" }.Concat(document.Languages));
        foreach (var entry in document.Entries)
            Row(new[] { entry.Id }.Concat(document.Languages.Select(l => document.Value(entry, l))));
        return text.ToString();
    }

    private static string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static char DetectDelimiter(string text)
    {
        var counts = new Dictionary<char, int> { [';'] = 0, [','] = 0, ['\t'] = 0 };
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') i++;
                else quoted = !quoted;
            }
            else if (!quoted)
            {
                if (c is '\r' or '\n') break;
                if (counts.ContainsKey(c)) counts[c]++;
            }
        }
        return counts.OrderByDescending(p => p.Value).First().Key;
    }

    private static List<List<string>> Parse(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false, closed = false, started = false;
        void EndField() { row.Add(field.ToString()); field.Clear(); closed = false; started = false; }
        void EndRow() { EndField(); rows.Add(row); row = []; }
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c != '"') field.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else { quoted = false; closed = true; }
                continue;
            }
            if (c == delimiter) { EndField(); continue; }
            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRow(); continue;
            }
            if (closed) throw new InvalidDataException("CSV : caractère inattendu après un guillemet fermant.");
            if (c == '"')
            {
                if (started) throw new InvalidDataException("CSV : guillemet inattendu dans un champ non délimité.");
                quoted = true; started = true;
            }
            else { field.Append(c); started = true; }
        }
        if (quoted) throw new InvalidDataException("CSV : guillemet non fermé.");
        if (started || closed || row.Count > 0) EndRow();
        return rows;
    }
}

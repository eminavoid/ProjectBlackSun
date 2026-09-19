using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

public static class XlsxReader
{
    public class Sheet
    {
        public string Name;
        public Dictionary<(int row, int col), string> Cells = new Dictionary<(int, int), string>();
        public int MaxRow;
        public int MaxCol;

        public string Get(int row, int col)
        {
            return Cells.TryGetValue((row, col), out var v) ? v : null;
        }

        public string Get(string a1)
        {
            var (r, c) = ParseA1(a1);
            return Get(r, c);
        }

        public static (int row, int col) ParseA1(string a1)
        {
            int i = 0;
            int col = 0;
            while (i < a1.Length && char.IsLetter(a1[i]))
            {
                col = col * 26 + (char.ToUpper(a1[i]) - 'A' + 1);
                i++;
            }
            int row = int.Parse(a1.Substring(i));
            return (row, col);
        }

        public static string ColumnLetter(int col)
        {
            string s = "";
            while (col > 0)
            {
                int rem = (col - 1) % 26;
                s = (char)('A' + rem) + s;
                col = (col - 1) / 26;
            }
            return s;
        }
    }

    public static List<Sheet> ReadSheets(string path)
    {
        var result = new List<Sheet>();

        using (var archive = ZipFile.OpenRead(path))
        {
            var sharedStrings = ReadSharedStrings(archive);
            var (sheetOrder, relIdToTarget) = ReadWorkbookMeta(archive);

            foreach (var (name, relId) in sheetOrder)
            {
                if (!relIdToTarget.TryGetValue(relId, out var target)) continue;
                string normalizedTarget = target.TrimStart('/');
                string entryPath = normalizedTarget.StartsWith("xl/") ? normalizedTarget : "xl/" + normalizedTarget;
                var entry = archive.GetEntry(entryPath);
                if (entry == null) continue;

                var sheet = new Sheet { Name = name };
                using (var stream = entry.Open())
                {
                    var doc = XDocument.Load(stream);
                    XNamespace ns = doc.Root.Name.Namespace;
                    foreach (var rowEl in doc.Descendants(ns + "row"))
                    {
                        foreach (var cellEl in rowEl.Elements(ns + "c"))
                        {
                            string cellRef = cellEl.Attribute("r")?.Value;
                            if (string.IsNullOrEmpty(cellRef)) continue;

                            string type = cellEl.Attribute("t")?.Value;
                            var vEl = cellEl.Element(ns + "v");
                            string value = null;

                            if (type == "s")
                            {
                                if (vEl != null && int.TryParse(vEl.Value, out int idx) && idx >= 0 && idx < sharedStrings.Count)
                                    value = sharedStrings[idx];
                            }
                            else if (type == "inlineStr")
                            {
                                var isEl = cellEl.Element(ns + "is");
                                value = isEl != null ? string.Concat(isEl.Descendants(ns + "t").Select(t => t.Value)) : null;
                            }
                            else
                            {
                                value = vEl?.Value;
                            }

                            if (value == null) continue;

                            var (row, col) = Sheet.ParseA1(cellRef);
                            sheet.Cells[(row, col)] = value;
                            if (row > sheet.MaxRow) sheet.MaxRow = row;
                            if (col > sheet.MaxCol) sheet.MaxCol = col;
                        }
                    }
                }
                result.Add(sheet);
            }
        }

        return result;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var list = new List<string>();
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return list;

        using (var stream = entry.Open())
        {
            var doc = XDocument.Load(stream);
            XNamespace ns = doc.Root.Name.Namespace;
            foreach (var si in doc.Root.Elements(ns + "si"))
            {
                string text = string.Concat(si.Descendants(ns + "t").Select(t => t.Value));
                list.Add(text);
            }
        }

        return list;
    }

    private static (List<(string name, string relId)>, Dictionary<string, string>) ReadWorkbookMeta(ZipArchive archive)
    {
        var sheetOrder = new List<(string, string)>();
        var relMap = new Dictionary<string, string>();

        var wbEntry = archive.GetEntry("xl/workbook.xml");
        using (var stream = wbEntry.Open())
        {
            var doc = XDocument.Load(stream);
            XNamespace ns = doc.Root.Name.Namespace;
            XNamespace rns = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            foreach (var sheetEl in doc.Descendants(ns + "sheet"))
            {
                string name = sheetEl.Attribute("name")?.Value;
                string rid = sheetEl.Attribute(rns + "id")?.Value;
                sheetOrder.Add((name, rid));
            }
        }

        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        using (var stream = relsEntry.Open())
        {
            var doc = XDocument.Load(stream);
            XNamespace ns = doc.Root.Name.Namespace;
            foreach (var rel in doc.Root.Elements(ns + "Relationship"))
            {
                string id = rel.Attribute("Id")?.Value;
                string target = rel.Attribute("Target")?.Value;
                if (id != null && target != null) relMap[id] = target;
            }
        }

        return (sheetOrder, relMap);
    }
}

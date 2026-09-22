using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

public class EventExcelImporterWindow : EditorWindow
{
    private string xlsxPath = "";
    private readonly List<string> log = new List<string>();
    private Vector2 scroll;

    private static readonly Dictionary<string, Resource> ResourceAliases = new Dictionary<string, Resource>(StringComparer.OrdinalIgnoreCase)
    {
        { "Wealth", Resource.Wealth },
        { "Zeal", Resource.Zeal },
        { "Authority", Resource.Authority },
        { "Flock", Resource.Flock },
        { "Happiness", Resource.Happiness },
        { "Bliss", Resource.Happiness },
        { "Materials", Resource.Materials },
        { "Secrets", Resource.Secrets },
    };

    [MenuItem("Tools/Import Events From Excel")]
    public static void Open()
    {
        GetWindow<EventExcelImporterWindow>("Event Importer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Excel Event Importer", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        xlsxPath = EditorGUILayout.TextField("XLSX Path", xlsxPath);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string picked = EditorUtility.OpenFilePanel("Select Events Excel", "", "xlsx");
            if (!string.IsNullOrEmpty(picked)) xlsxPath = picked;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (GUILayout.Button("Import New Events"))
        {
            log.Clear();
            RunImport();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(300));
        foreach (var line in log)
        {
            EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunImport()
    {
        if (string.IsNullOrEmpty(xlsxPath) || !System.IO.File.Exists(xlsxPath))
        {
            log.Add("ERROR: XLSX file not found at path.");
            return;
        }

        List<XlsxReader.Sheet> sheets;
        try
        {
            sheets = XlsxReader.ReadSheets(xlsxPath);
        }
        catch (Exception e)
        {
            log.Add($"ERROR: Failed to open XLSX: {e.Message}");
            return;
        }

        PlayerStats sharedPlayerStats = FindSharedPlayerStats();
        if (sharedPlayerStats == null)
        {
            log.Add("ERROR: Could not find an existing PlayerStats asset in the project. Aborting.");
            return;
        }

        int created = 0;
        int skipped = 0;

        foreach (var sheet in sheets)
        {
            string category = sheet.Name.Trim();
            if (category.Equals("TEMPLATE", StringComparison.OrdinalIgnoreCase)) continue;

            var events = ScanSheet(sheet);
            foreach (var evt in events)
            {
                string folder = $"Assets/Events/NUEVOS/{category}/{evt.id}";
                if (AssetDatabase.IsValidFolder(folder))
                {
                    log.Add($"Skipped {evt.id} ({category}): already exists.");
                    skipped++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(evt.title))
                {
                    log.Add($"WARNING: {evt.id} ({category}) has a blank title.");
                }
                if (string.IsNullOrWhiteSpace(evt.description))
                {
                    log.Add($"WARNING: {evt.id} ({category}) has a blank description.");
                }
                foreach (var opt in evt.options)
                {
                    if (string.IsNullOrWhiteSpace(opt.title))
                    {
                        log.Add($"WARNING: {evt.id} option {opt.letter} ({category}) has a blank title.");
                    }
                    if (opt.outcomes.Count == 0)
                    {
                        log.Add($"WARNING: {evt.id} option {opt.letter} ({category}) has zero outcomes.");
                    }
                }

                CreateSeedAsset(category, evt, sharedPlayerStats);
                log.Add($"Created {evt.id} ({category}) with {evt.options.Count} option(s).");
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.Add($"Done. Created: {created}, Skipped (already existed): {skipped}.");
    }

    private PlayerStats FindSharedPlayerStats()
    {
        string[] guids = AssetDatabase.FindAssets("t:PlayerStats");
        if (guids.Length == 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<PlayerStats>(path);
    }

    private class ParsedOutcome
    {
        public string text;
        public float weight;
        public string effectRaw;
    }

    private class ParsedOption
    {
        public string letter;
        public string title;
        public string costRaw;
        public List<ParsedOutcome> outcomes = new List<ParsedOutcome>();
    }

    private class ParsedEvent
    {
        public string id;
        public string title;
        public string description;
        public List<ParsedOption> options = new List<ParsedOption>();
    }

    private string FindFirstNonBlank(XlsxReader.Sheet sheet, int row, int fromCol, int toCol)
    {
        for (int c = fromCol; c <= toCol; c++)
        {
            string val = sheet.Get(row, c);
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
        }
        return "";
    }

    private List<ParsedEvent> ScanSheet(XlsxReader.Sheet sheet)
    {
        var events = new List<ParsedEvent>();
        int startCol = 1;

        while (true)
        {
            string id = sheet.Get(1, startCol);
            if (string.IsNullOrWhiteSpace(id)) break;

            var evt = new ParsedEvent
            {
                id = id.Trim(),
                title = FindFirstNonBlank(sheet, 1, startCol + 1, startCol + 4),
                description = FindFirstNonBlank(sheet, 2, startCol, startCol + 4)
            };

            string[] letters = { "A", "B", "C", "D" };
            for (int i = 0; i < 4; i++)
            {
                int groupRow = 3 + 15 * (i / 2);
                int colOffset = (i % 2 == 0) ? 0 : 3;
                int dataCol = startCol + colOffset + 1;

                string optTitle = sheet.Get(groupRow, dataCol);
                if (string.IsNullOrWhiteSpace(optTitle)) continue;

                var option = new ParsedOption
                {
                    letter = letters[i],
                    title = optTitle.Trim(),
                    costRaw = sheet.Get(groupRow + 1, dataCol)
                };

                for (int o = 0; o < 4; o++)
                {
                    int textRow = groupRow + 2 + 3 * o;
                    string text = sheet.Get(textRow, dataCol);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    string weightRaw = sheet.Get(textRow + 1, dataCol);
                    string effectRaw = sheet.Get(textRow + 2, dataCol);

                    float weight;
                    if (!float.TryParse(weightRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out weight))
                    {
                        weight = 0f;
                    }

                    option.outcomes.Add(new ParsedOutcome { text = text.Trim(), weight = weight, effectRaw = effectRaw });
                }

                evt.options.Add(option);
            }

            if (evt.options.Count > 0) events.Add(evt);
            startCol += 6;
        }

        return events;
    }

    private struct ResourceAmount
    {
        public Resource resource;
        public int amount;
    }

    private bool IsEmptyCost(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        string trimmed = raw.Trim();
        if (trimmed.Equals("0") || trimmed.Equals("0.0")) return true;
        if (trimmed.Equals("No cost", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private List<ResourceAmount> ParseResourceClauses(string raw)
    {
        var result = new List<ResourceAmount>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        string cleaned = raw.Replace("!!!", "");
        string[] clauses = Regex.Split(cleaned, @"[&/]");

        foreach (var rawClause in clauses)
        {
            string clause = rawClause.Trim();
            if (string.IsNullOrEmpty(clause)) continue;
            if (clause.Equals("0") || clause.Equals("0.0")) continue;

            Match m = Regex.Match(clause, @"^([+-]?\d+)\s+(\w+)");
            if (!m.Success) continue;

            int amount = int.Parse(m.Groups[1].Value);
            string resourceName = m.Groups[2].Value;

            if (!ResourceAliases.TryGetValue(resourceName, out Resource res)) continue;

            result.Add(new ResourceAmount { resource = res, amount = amount });
        }

        return result;
    }

    private void CreateSeedAsset(string category, ParsedEvent evt, PlayerStats sharedPlayerStats)
    {
        string eventFolder = $"Assets/Events/NUEVOS/{category}/{evt.id}";
        string optionsFolder = $"{eventFolder}/Options";

        EnsureFolder("Assets/Events/NUEVOS", category);
        EnsureFolder($"Assets/Events/NUEVOS/{category}", evt.id);
        EnsureFolder(eventFolder, "Options");

        var createdOptions = new List<Option>();

        foreach (var parsedOption in evt.options)
        {
            Option option = ScriptableObject.CreateInstance<Option>();
            string optionAssetPath = $"{optionsFolder}/{evt.id}.outcome{parsedOption.letter}.asset";
            AssetDatabase.CreateAsset(option, optionAssetPath);

            SerializedObject so = new SerializedObject(option);
            so.FindProperty("playerStats").objectReferenceValue = sharedPlayerStats;
            so.FindProperty("title").stringValue = parsedOption.title;

            bool hasCost = !IsEmptyCost(parsedOption.costRaw);
            so.FindProperty("description").stringValue = hasCost ? $"COST: {parsedOption.costRaw.Trim()}" : "COST: No cost";
            so.FindProperty("endsQuestline").boolValue = true;

            List<ResourceAmount> costClauses = hasCost ? ParseResourceClauses(parsedOption.costRaw) : new List<ResourceAmount>();

            SerializedProperty modulesProp = so.FindProperty("modules");
            modulesProp.arraySize = costClauses.Count + 1;

            for (int i = 0; i < costClauses.Count; i++)
            {
                SerializedProperty elem = modulesProp.GetArrayElementAtIndex(i);
                elem.managedReferenceValue = new RequireResource();
                elem.FindPropertyRelative("resource").enumValueIndex = (int)costClauses[i].resource;
                elem.FindPropertyRelative("required").intValue = Mathf.Abs(costClauses[i].amount);
                elem.FindPropertyRelative("consumeResource").boolValue = true;
            }

            SerializedProperty weighedElem = modulesProp.GetArrayElementAtIndex(costClauses.Count);
            weighedElem.managedReferenceValue = new WeighedChoice();

            SerializedProperty weighedModulesProp = weighedElem.FindPropertyRelative("modules");
            weighedModulesProp.arraySize = parsedOption.outcomes.Count;

            for (int i = 0; i < parsedOption.outcomes.Count; i++)
            {
                var outcome = parsedOption.outcomes[i];
                SerializedProperty weightedElement = weighedModulesProp.GetArrayElementAtIndex(i);

                weightedElement.FindPropertyRelative("weight").intValue = Mathf.RoundToInt(outcome.weight);
                weightedElement.FindPropertyRelative("statUsed").enumValueIndex = 0;
                weightedElement.FindPropertyRelative("output").stringValue = outcome.text;

                List<ResourceAmount> effects = ParseResourceClauses(outcome.effectRaw);
                SerializedProperty innerModulesProp = weightedElement.FindPropertyRelative("module");
                innerModulesProp.arraySize = effects.Count;

                for (int j = 0; j < effects.Count; j++)
                {
                    SerializedProperty innerElem = innerModulesProp.GetArrayElementAtIndex(j);
                    innerElem.managedReferenceValue = new ChangeResource();
                    innerElem.FindPropertyRelative("resource").enumValueIndex = (int)effects[j].resource;
                    innerElem.FindPropertyRelative("minAmount").intValue = effects[j].amount;
                    innerElem.FindPropertyRelative("maxAmount").intValue = effects[j].amount;
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(option);
            createdOptions.Add(option);
        }

        Seed seed = ScriptableObject.CreateInstance<Seed>();
        string seedAssetPath = $"{eventFolder}/{evt.id}.asset";
        AssetDatabase.CreateAsset(seed, seedAssetPath);

        SerializedObject seedSo = new SerializedObject(seed);
        seedSo.FindProperty("title").stringValue = evt.title;
        seedSo.FindProperty("description").stringValue = evt.description;
        seedSo.FindProperty("ticks").intValue = 1;
        seedSo.FindProperty("eventType").enumValueIndex = 0;
        seedSo.FindProperty("difficulty").enumValueIndex = 0;

        SerializedProperty optionsProp = seedSo.FindProperty("<Options>k__BackingField");
        optionsProp.arraySize = createdOptions.Count;
        for (int i = 0; i < createdOptions.Count; i++)
        {
            optionsProp.GetArrayElementAtIndex(i).objectReferenceValue = createdOptions[i];
        }

        seedSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(seed);
    }

    private void EnsureFolder(string parent, string newFolderName)
    {
        string combined = $"{parent}/{newFolderName}";
        if (!AssetDatabase.IsValidFolder(combined))
        {
            AssetDatabase.CreateFolder(parent, newFolderName);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        EditorGUILayout.HelpBox(
            "Creates the events that don't exist yet and re-imports the ones that differ from the Excel. " +
            "Unchanged events are skipped. Fields the Excel doesn't have (ticks, type, difficulty, icons, follow-ups) are kept.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview Changes"))
        {
            log.Clear();
            RunImport(false);
        }
        if (GUILayout.Button("Import / Update Events"))
        {
            log.Clear();
            RunImport(true);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(300));
        foreach (var line in log)
        {
            EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunImport(bool apply)
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
        int updated = 0;
        int unchanged = 0;

        foreach (var sheet in sheets)
        {
            string category = sheet.Name.Trim();
            if (category.Equals("TEMPLATE", StringComparison.OrdinalIgnoreCase)) continue;

            Region region = ParseRegion(category);
            if (region == Region.None) log.Add($"WARNING: sheet \"{category}\" isn't named after a region; its events get none.");

            var events = ScanSheet(sheet);
            foreach (var evt in events)
            {
                evt.region = region;
                Seed existing = FindExistingSeed(evt.id);
                List<string> differences = null;
                if (existing != null)
                {
                    differences = FindDifferences(evt, existing);
                    if (differences.Count == 0)
                    {
                        log.Add($"Skipped {evt.id} ({category}): matches the Excel.");
                        unchanged++;
                        continue;
                    }
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

                if (apply) WriteEvent(category, evt, existing, sharedPlayerStats);

                if (existing == null)
                {
                    log.Add($"{(apply ? "Created" : "Would create")} {evt.id} ({category}) with {evt.options.Count} option(s).");
                    created++;
                }
                else
                {
                    log.Add($"{(apply ? "Updated" : "Would update")} {evt.id} ({category}):");
                    foreach (string difference in differences) log.Add($"    - {difference}");
                    updated++;
                }
            }
        }

        if (apply)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        log.Add($"Done. Created: {created}, Updated: {updated}, Unchanged: {unchanged}." + (apply ? "" : " Preview only, nothing was written."));
    }

    private static string EventFolder(string category, string id) => $"Assets/Events/NUEVOS/{category}/{id}";

    // Sheets are named after the regions: "Agora", "Pink Quarter", "The Warrens"...
    private static Region ParseRegion(string sheetName)
    {
        string name = sheetName.Trim();
        if (name.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) name = name.Substring(4);
        name = name.Replace(" ", "");

        return Enum.TryParse(name, true, out Region region) && Enum.IsDefined(typeof(Region), region) ? region : Region.None;
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
        public Region region;
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

            // A and C are labeled in the event's first column, B and D three columns to the right.
            ScanOptions(sheet, startCol, evt.options);
            ScanOptions(sheet, startCol + 3, evt.options);
            evt.options.Sort((a, b) => string.CompareOrdinal(a.letter, b.letter));

            if (evt.options.Count > 0) events.Add(evt);
            startCol += 6;
        }

        return events;
    }

    // Events don't all have the same height (some options have 2 outcomes, the template has room for 4),
    // so rows are found through the labels column: "A" starts an option, "COST" follows it,
    // and "1A", "2A"... start an outcome whose weight ("%") and effect ("$") are the two rows below.
    private void ScanOptions(XlsxReader.Sheet sheet, int labelCol, List<ParsedOption> options)
    {
        int dataCol = labelCol + 1;
        ParsedOption option = null;

        for (int row = 3; row <= sheet.MaxRow; row++)
        {
            string label = (sheet.Get(row, labelCol) ?? "").Trim();

            if (label.Length == 1 && label[0] >= 'A' && label[0] <= 'D')
            {
                string title = sheet.Get(row, dataCol);
                option = string.IsNullOrWhiteSpace(title) ? null : new ParsedOption
                {
                    letter = label,
                    title = title.Trim(),
                    costRaw = sheet.Get(row + 1, dataCol)
                };

                if (option != null) options.Add(option);
                continue;
            }

            if (option == null || label.Length == 0 || !char.IsDigit(label[0])) continue;

            string text = sheet.Get(row, dataCol);
            if (string.IsNullOrWhiteSpace(text)) continue;

            float weight;
            if (!float.TryParse(sheet.Get(row + 1, dataCol), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out weight))
            {
                weight = 0f;
            }

            option.outcomes.Add(new ParsedOutcome { text = StripResourceSummary(text.Trim()), weight = weight, effectRaw = sheet.Get(row + 2, dataCol) });
        }
    }

    private struct ResourceAmount
    {
        public Resource resource;
        public int amount;
        public bool refund;
    }

    // Outcome texts end with a "[-25 Zeal & +50 Wealth]" summary; the result window shows it with icons now.
    // Only a bracket closing the text is removed; "[" also counts as closing because some rows have that typo.
    private static string StripResourceSummary(string text)
    {
        string trimmed = text.TrimEnd();
        if (trimmed.Length < 2 || (!trimmed.EndsWith("]") && !trimmed.EndsWith("["))) return text;

        int open = trimmed.LastIndexOf('[', trimmed.Length - 2);
        return open < 0 ? text : trimmed.Substring(0, open).TrimEnd();
    }

    private static bool IsEmptyCost(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        string trimmed = raw.Trim();
        if (trimmed.Equals("0") || trimmed.Equals("0.0")) return true;
        if (trimmed.Equals("No cost", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static List<ResourceAmount> ParseResourceClauses(string raw)
    {
        var result = new List<ResourceAmount>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        // Lists come as "25 Flock, 25 Zeal & 10 Flock!!!"; "!!!" marks the clause that refunds the option's cost.
        string[] clauses = Regex.Split(raw, @"[&/,]");

        foreach (var rawClause in clauses)
        {
            bool refund = rawClause.Contains("!!!");
            string clause = rawClause.Replace("!!!", "").Trim();
            if (string.IsNullOrEmpty(clause)) continue;
            if (clause.Equals("0") || clause.Equals("0.0")) continue;

            Match m = Regex.Match(clause, @"^([+-]?\d+)\s*([A-Za-z]+)");
            if (!m.Success) continue;

            int amount = int.Parse(m.Groups[1].Value);
            string resourceName = m.Groups[2].Value;

            if (!ResourceAliases.TryGetValue(resourceName, out Resource res)) continue;

            result.Add(new ResourceAmount { resource = res, amount = amount, refund = refund });
        }

        return result;
    }

    private static string CostText(string costRaw)
    {
        return IsEmptyCost(costRaw) ? "COST: No cost" : $"COST: {costRaw.Trim()}";
    }

    private static List<ResourceAmount> CostClauses(string costRaw)
    {
        return IsEmptyCost(costRaw) ? new List<ResourceAmount>() : ParseResourceClauses(costRaw);
    }

    // Imported events live in NUEVOS/{category}/{id}, but a folder may have been renamed since, so they're found by asset name.
    private static Seed FindExistingSeed(string id)
    {
        const string root = "Assets/Events/NUEVOS";
        if (!AssetDatabase.IsValidFolder(root)) return null;

        foreach (string guid in AssetDatabase.FindAssets($"t:Seed {id}", new[] { root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == id) return AssetDatabase.LoadAssetAtPath<Seed>(path);
        }

        return null;
    }

    #region Comparing

    // One value the Excel defines ("Option A outcome 2" / "weight" / "75"). Both the Excel and the game
    // describe an event as a list of these, so comparing them is comparing the lists.
    private struct EventField
    {
        public string scope;
        public string name;
        public string value;

        public string Key => $"{scope}|{name}";
    }

    private static void AddField(List<EventField> fields, string scope, string name, string value)
    {
        fields.Add(new EventField { scope = scope, name = name, value = (value ?? "").Replace("\r\n", "\n") });
    }

    private static List<string> FindDifferences(ParsedEvent evt, Seed seed)
    {
        List<EventField> expected = DescribeExcelEvent(evt);
        List<EventField> actual = DescribeGameEvent(seed, evt.id);

        var actualValues = new Dictionary<string, string>();
        foreach (EventField field in actual) actualValues[field.Key] = field.value;
        var expectedScopes = new HashSet<string>(expected.Select(field => field.scope));
        var actualScopes = new HashSet<string>(actual.Select(field => field.scope));

        var differences = new List<string>();
        var reportedScopes = new List<string>();

        foreach (EventField field in expected)
        {
            if (!actualScopes.Contains(field.scope))
            {
                ReportScope(field.scope, "new in the Excel", differences, reportedScopes);
                continue;
            }

            actualValues.TryGetValue(field.Key, out string current);
            if (current != field.value) differences.Add($"{field.scope} {field.name}: {DescribeChange(current, field.value)}");
        }

        foreach (EventField field in actual)
        {
            if (!expectedScopes.Contains(field.scope))
            {
                ReportScope(field.scope, "not in the Excel anymore, it gets unlinked from the event", differences, reportedScopes);
            }
        }

        return differences;
    }

    // A whole option or outcome added/removed is one line, not one per field (nor per outcome of a new option).
    private static void ReportScope(string scope, string message, List<string> differences, List<string> reportedScopes)
    {
        if (reportedScopes.Any(reported => scope == reported || scope.StartsWith(reported + " "))) return;
        reportedScopes.Add(scope);
        differences.Add($"{scope}: {message}");
    }

    private static List<EventField> DescribeExcelEvent(ParsedEvent evt)
    {
        var fields = new List<EventField>();
        AddField(fields, "Event", "title", evt.title);
        AddField(fields, "Event", "description", evt.description);
        AddField(fields, "Event", "region", evt.region.ToString());
        AddField(fields, "Event", "options", string.Join(", ", evt.options.Select(option => option.letter)));

        foreach (ParsedOption option in evt.options)
        {
            DescribeExcelOption(fields, option);
        }

        return fields;
    }

    private static void DescribeExcelOption(List<EventField> fields, ParsedOption option)
    {
        string scope = $"Option {option.letter}";
        AddField(fields, scope, "title", option.title);
        AddField(fields, scope, "cost text", CostText(option.costRaw));
        AddField(fields, scope, "cost", JoinOrNone(CostClauses(option.costRaw).Select(cost => $"{Mathf.Abs(cost.amount)} {cost.resource}")));
        AddField(fields, scope, "outcome tables", "1");

        for (int i = 0; i < option.outcomes.Count; i++)
        {
            ParsedOutcome outcome = option.outcomes[i];
            string outcomeScope = $"{scope} outcome {i + 1}";
            AddField(fields, outcomeScope, "weight", Mathf.RoundToInt(outcome.weight).ToString());
            AddField(fields, outcomeScope, "text", outcome.text);
            AddField(fields, outcomeScope, "resources", JoinOrNone(ParseResourceClauses(outcome.effectRaw)
                .Select(effect => FormatChange(effect.resource, effect.amount, effect.amount, effect.refund))));
        }
    }

    // Only what the importer writes is described: modules someone added by hand (DoNothing, zone influence...)
    // are kept on re-import, so they must not count as differences either.
    private static List<EventField> DescribeGameEvent(Seed seed, string id)
    {
        var fields = new List<EventField>();
        var seedObject = new SerializedObject(seed);
        List<Option> options = seed.Options ?? new List<Option>();

        AddField(fields, "Event", "title", seedObject.FindProperty("title").stringValue);
        AddField(fields, "Event", "description", seedObject.FindProperty("description").stringValue);
        AddField(fields, "Event", "region", ((Region)seedObject.FindProperty("region").intValue).ToString());
        AddField(fields, "Event", "options", string.Join(", ", options.Select(option => option != null ? OptionLetter(option, id) : "(empty)")));

        foreach (Option option in options)
        {
            if (option != null) DescribeGameOption(fields, option, OptionLetter(option, id));
        }

        return fields;
    }

    private static void DescribeGameOption(List<EventField> fields, Option option, string letter)
    {
        string scope = $"Option {letter}";
        var optionObject = new SerializedObject(option);
        AddField(fields, scope, "title", optionObject.FindProperty("title").stringValue);
        AddField(fields, scope, "cost text", optionObject.FindProperty("description").stringValue);

        var costs = new List<string>();
        SerializedProperty outcomes = null;
        int outcomeTables = 0;

        SerializedProperty modules = optionObject.FindProperty("modules");
        for (int i = 0; i < modules.arraySize; i++)
        {
            SerializedProperty module = modules.GetArrayElementAtIndex(i);
            switch (ModuleType(module))
            {
                case nameof(RequireResource):
                    string cost = $"{module.FindPropertyRelative("required").intValue} {(Resource)module.FindPropertyRelative("resource").intValue}";
                    costs.Add(module.FindPropertyRelative("consumeResource").boolValue ? cost : cost + " (not spent)");
                    break;
                case nameof(WeighedChoice):
                    if (outcomeTables++ == 0) outcomes = module.FindPropertyRelative("modules");
                    break;
            }
        }

        AddField(fields, scope, "cost", JoinOrNone(costs));
        AddField(fields, scope, "outcome tables", outcomeTables.ToString());
        if (outcomes == null) return;

        for (int i = 0; i < outcomes.arraySize; i++)
        {
            SerializedProperty outcome = outcomes.GetArrayElementAtIndex(i);
            string outcomeScope = $"{scope} outcome {i + 1}";
            AddField(fields, outcomeScope, "weight", outcome.FindPropertyRelative("weight").intValue.ToString());
            AddField(fields, outcomeScope, "text", outcome.FindPropertyRelative("output").stringValue);

            var effects = new List<string>();
            SerializedProperty effectModules = outcome.FindPropertyRelative("module");
            for (int j = 0; j < effectModules.arraySize; j++)
            {
                SerializedProperty effect = effectModules.GetArrayElementAtIndex(j);
                if (ModuleType(effect) != nameof(ChangeResource)) continue;

                effects.Add(FormatChange(
                    (Resource)effect.FindPropertyRelative("resource").intValue,
                    effect.FindPropertyRelative("minAmount").intValue,
                    effect.FindPropertyRelative("maxAmount").intValue,
                    effect.FindPropertyRelative("refund").boolValue));
            }

            AddField(fields, outcomeScope, "resources", JoinOrNone(effects));
        }
    }

    private static bool OptionMatches(Option option, ParsedOption parsedOption)
    {
        var expected = new List<EventField>();
        var actual = new List<EventField>();
        DescribeExcelOption(expected, parsedOption);
        DescribeGameOption(actual, option, parsedOption.letter);
        return expected.Select(field => field.Key + "=" + field.value).SequenceEqual(actual.Select(field => field.Key + "=" + field.value));
    }

    private static string OptionLetter(Option option, string id)
    {
        string prefix = $"{id}.outcome";
        return option.name.StartsWith(prefix) ? option.name.Substring(prefix.Length) : option.name;
    }

    // "Assembly-CSharp Namespace.ClassName" -> "ClassName"; empty for a null reference.
    private static string ModuleType(SerializedProperty module)
    {
        string fullName = module.managedReferenceFullTypename;
        if (string.IsNullOrEmpty(fullName)) return "";

        string typeName = fullName.Substring(fullName.LastIndexOf(' ') + 1);
        return typeName.Substring(typeName.LastIndexOf('.') + 1);
    }

    private static string FormatChange(Resource resource, int minAmount, int maxAmount, bool refund)
    {
        string amount = minAmount == maxAmount ? Signed(minAmount) : $"{Signed(minAmount)}..{Signed(maxAmount)}";
        return $"{amount} {resource}" + (refund ? " (refund)" : "");
    }

    private static string Signed(int amount) => amount > 0 ? $"+{amount}" : amount.ToString();

    private static string JoinOrNone(IEnumerable<string> values)
    {
        string joined = string.Join(", ", values);
        return joined.Length > 0 ? joined : "none";
    }

    // Short "old -> new" for the log. Long texts only show the stretch where they start to differ.
    private static string DescribeChange(string current, string expected)
    {
        if (current == null) return $"(none) -> {Quote(expected)}";

        const int shortLength = 70;
        if (current.Length <= shortLength && expected.Length <= shortLength) return $"{Quote(current)} -> {Quote(expected)}";

        int firstDifference = 0;
        while (firstDifference < current.Length && firstDifference < expected.Length && current[firstDifference] == expected[firstDifference])
        {
            firstDifference++;
        }

        int from = Mathf.Max(0, firstDifference - 25);
        return $"{Quote(Excerpt(current, from, 50))} -> {Quote(Excerpt(expected, from, 50))}";
    }

    private static string Excerpt(string text, int from, int length)
    {
        if (from >= text.Length) return from > 0 ? "..." : "";

        string part = text.Substring(from, Mathf.Min(length, text.Length - from));
        return (from > 0 ? "..." : "") + part + (from + length < text.Length ? "..." : "");
    }

    // Tabs and line breaks are made visible, otherwise "trailing tab" differences look identical in the log.
    private static string Quote(string value) => "\"" + value.Replace("\n", "\\n").Replace("\t", "\\t") + "\"";

    #endregion

    #region Writing

    private static readonly FieldInfo OptionModulesField = typeof(Option).GetField("modules", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo OutcomesField = typeof(WeighedChoice).GetField("modules", BindingFlags.Instance | BindingFlags.NonPublic);

    // Creates the event, or updates it in place so whatever references its assets (event pools...) keeps working.
    // Only what the Excel defines is overwritten: ticks, type, difficulty, icons, follow-ups and hand-added modules stay.
    private static void WriteEvent(string category, ParsedEvent evt, Seed seed, PlayerStats sharedPlayerStats)
    {
        bool isNewEvent = seed == null;
        string eventFolder = isNewEvent
            ? EventFolder(category, evt.id)
            : System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(seed)).Replace('\\', '/');
        string optionsFolder = $"{eventFolder}/Options";

        if (isNewEvent)
        {
            EnsureFolder("Assets/Events/NUEVOS", category);
            EnsureFolder($"Assets/Events/NUEVOS/{category}", evt.id);
        }
        EnsureFolder(eventFolder, "Options");

        List<Option> currentOptions = isNewEvent || seed.Options == null ? new List<Option>() : seed.Options;
        var options = new List<Option>();

        foreach (ParsedOption parsedOption in evt.options)
        {
            string optionName = $"{evt.id}.outcome{parsedOption.letter}";
            Option option = currentOptions.FirstOrDefault(current => current != null && current.name == optionName);
            if (option == null) option = AssetDatabase.LoadAssetAtPath<Option>($"{optionsFolder}/{optionName}.asset");

            bool isNewOption = option == null;
            if (isNewOption)
            {
                option = ScriptableObject.CreateInstance<Option>();
                AssetDatabase.CreateAsset(option, $"{optionsFolder}/{optionName}.asset");
            }

            // Options that already match are left alone, so their files don't change for nothing.
            if (isNewOption || !OptionMatches(option, parsedOption)) WriteOption(option, parsedOption, isNewOption, sharedPlayerStats);
            options.Add(option);
        }

        if (isNewEvent)
        {
            seed = ScriptableObject.CreateInstance<Seed>();
            AssetDatabase.CreateAsset(seed, $"{eventFolder}/{evt.id}.asset");
        }

        var seedObject = new SerializedObject(seed);
        seedObject.FindProperty("title").stringValue = evt.title;
        seedObject.FindProperty("description").stringValue = evt.description;
        seedObject.FindProperty("region").intValue = (int)evt.region;

        if (isNewEvent)
        {
            seedObject.FindProperty("ticks").intValue = 1;
            seedObject.FindProperty("eventType").enumValueIndex = 0;
            seedObject.FindProperty("difficulty").enumValueIndex = 0;
        }

        // Options that left the Excel are only unlinked; their assets stay in the folder.
        SerializedProperty optionsProp = seedObject.FindProperty("<Options>k__BackingField");
        optionsProp.arraySize = options.Count;
        for (int i = 0; i < options.Count; i++)
        {
            optionsProp.GetArrayElementAtIndex(i).objectReferenceValue = options[i];
        }

        if (seedObject.ApplyModifiedProperties()) EditorUtility.SetDirty(seed);
    }

    // What an outcome has besides the Excel's data: the stat that boosts it and any module that isn't a resource change.
    private struct OutcomeExtras
    {
        public int statUsed;
        public List<OptionModule> modules;
    }

    private static void WriteOption(Option option, ParsedOption parsedOption, bool isNewOption, PlayerStats sharedPlayerStats)
    {
        List<ResourceAmount> costs = CostClauses(parsedOption.costRaw);

        // The cost requirements and the outcome table are the Excel's; other modules stay where they were.
        List<OptionModule> currentModules = OptionModulesField.GetValue(option) as List<OptionModule> ?? new List<OptionModule>();
        WeighedChoice weighedChoice = currentModules.OfType<WeighedChoice>().FirstOrDefault();
        List<OutcomeExtras> outcomeExtras = ReadOutcomeExtras(weighedChoice);
        if (weighedChoice == null) weighedChoice = new WeighedChoice();

        List<OptionModule> requirements = costs.Select(_ => (OptionModule)new RequireResource()).ToList();
        var modules = new List<OptionModule>();
        bool requirementsPlaced = false;

        foreach (OptionModule module in currentModules)
        {
            if (module is RequireResource)
            {
                if (!requirementsPlaced) modules.AddRange(requirements);
                requirementsPlaced = true;
            }
            else if (module == weighedChoice || (module != null && !(module is WeighedChoice)))
            {
                modules.Add(module);
            }
        }

        if (!requirementsPlaced) modules.InsertRange(0, requirements);
        if (!modules.Contains(weighedChoice)) modules.Add(weighedChoice);
        OptionModulesField.SetValue(option, modules);

        var optionObject = new SerializedObject(option);
        SerializedProperty playerStats = optionObject.FindProperty("playerStats");
        if (playerStats.objectReferenceValue == null) playerStats.objectReferenceValue = sharedPlayerStats;
        optionObject.FindProperty("title").stringValue = parsedOption.title;
        optionObject.FindProperty("description").stringValue = CostText(parsedOption.costRaw);
        if (isNewOption) optionObject.FindProperty("endsQuestline").boolValue = true;

        SerializedProperty modulesProp = optionObject.FindProperty("modules");
        for (int i = 0; i < costs.Count; i++)
        {
            SerializedProperty requirement = modulesProp.GetArrayElementAtIndex(modules.IndexOf(requirements[i]));
            requirement.FindPropertyRelative("resource").enumValueIndex = (int)costs[i].resource;
            requirement.FindPropertyRelative("required").intValue = Mathf.Abs(costs[i].amount);
            requirement.FindPropertyRelative("consumeResource").boolValue = true;
        }

        SerializedProperty outcomesProp = modulesProp.GetArrayElementAtIndex(modules.IndexOf(weighedChoice)).FindPropertyRelative("modules");
        outcomesProp.arraySize = parsedOption.outcomes.Count;

        for (int i = 0; i < parsedOption.outcomes.Count; i++)
        {
            ParsedOutcome outcome = parsedOption.outcomes[i];
            OutcomeExtras extras = i < outcomeExtras.Count ? outcomeExtras[i] : new OutcomeExtras { modules = new List<OptionModule>() };
            SerializedProperty outcomeProp = outcomesProp.GetArrayElementAtIndex(i);

            outcomeProp.FindPropertyRelative("weight").intValue = Mathf.RoundToInt(outcome.weight);
            outcomeProp.FindPropertyRelative("statUsed").enumValueIndex = extras.statUsed;
            outcomeProp.FindPropertyRelative("output").stringValue = outcome.text;

            List<ResourceAmount> effects = ParseResourceClauses(outcome.effectRaw);
            SerializedProperty effectsProp = outcomeProp.FindPropertyRelative("module");
            effectsProp.arraySize = effects.Count + extras.modules.Count;

            for (int j = 0; j < effects.Count; j++)
            {
                SerializedProperty effect = effectsProp.GetArrayElementAtIndex(j);
                effect.managedReferenceValue = new ChangeResource();
                effect.FindPropertyRelative("resource").enumValueIndex = (int)effects[j].resource;
                effect.FindPropertyRelative("minAmount").intValue = effects[j].amount;
                effect.FindPropertyRelative("maxAmount").intValue = effects[j].amount;
                effect.FindPropertyRelative("refund").boolValue = effects[j].refund;
            }

            for (int j = 0; j < extras.modules.Count; j++)
            {
                effectsProp.GetArrayElementAtIndex(effects.Count + j).managedReferenceValue = extras.modules[j];
            }
        }

        optionObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(option);
    }

    // Outcomes are matched by position: the stat of outcome 2 stays on outcome 2.
    private static List<OutcomeExtras> ReadOutcomeExtras(WeighedChoice weighedChoice)
    {
        var extras = new List<OutcomeExtras>();
        if (weighedChoice == null || !(OutcomesField.GetValue(weighedChoice) is IList outcomes)) return extras;

        foreach (object outcome in outcomes)
        {
            Type outcomeType = outcome.GetType();
            var modules = outcomeType.GetField("module").GetValue(outcome) as List<OptionModule>;

            extras.Add(new OutcomeExtras
            {
                statUsed = Convert.ToInt32(outcomeType.GetField("statUsed").GetValue(outcome)),
                modules = modules?.Where(module => module != null && !(module is ChangeResource)).ToList() ?? new List<OptionModule>()
            });
        }

        return extras;
    }

    #endregion

    private static void EnsureFolder(string parent, string newFolderName)
    {
        string combined = $"{parent}/{newFolderName}";
        if (!AssetDatabase.IsValidFolder(combined))
        {
            AssetDatabase.CreateFolder(parent, newFolderName);
        }
    }
}

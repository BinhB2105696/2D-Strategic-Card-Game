using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;

// Explicit asset-only operation. Preserves existing image metadata/GUIDs and card rules.
public static class CardArtworkImportTool
{
    private const string Template = "Assets/Art/Cards/Monsters/Stone_Guadian.png";
    private const string ArtRoot = "Assets/Art/Cards";
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static string SourceRoot => Path.Combine(ProjectRoot, "Artworks", "Cards");
    private static readonly string[] Approved =
    {
        "Aegis_Sentinel", "Ambush_Lancer", "Ambush", "Arcane_Archivist", "Arcane_Insight",
        "Bastion_Keeper", "Battle_Herald", "Battlefield_Medicine", "Bloodfang_Raider",
        "Change_of_Plans", "Dawnwing_Fairy", "Defensive_Outpost", "Dispel_Magic",
        "Divine_Reversal", "Elite_Summons", "Emergency_Reinforcements", "Energy_Reallocation",
        "Energy_Reserve", "Energy_Venom", "Energy_Wisp", "Field_Medic", "Fire_Dragon",
        "Guardian_Shift", "Iron_Blade", "Logistics_Squire", "Power_Transfer", "Quick_Strike",
        "Recover_the_Fallen", "Scar_Duelist", "Siegebreaker", "Static_Leech", "Stop_Defense",
        "Temporal_Lock", "Test_Field", "Thorn_Bastion", "Tide_Sprite", "Veil_Strategist"
    };
    private static readonly HashSet<string> Replaceable = new HashSet<string>(StringComparer.Ordinal)
        { "Dawnwing_Fairy", "Fire_Dragon", "Tide_Sprite" };

    private sealed class Entry
    {
        public CardData Card;
        public string Source, Target, TargetAbsolute, OriginalGuid;
    }

    [MenuItem("Card Game/Art/Import and Assign 37 Approved Card Artworks")]
    public static void Import()
    {
        RequireEditMode();
        if (Application.platform != RuntimePlatform.WindowsEditor)
            throw new InvalidOperationException("This import uses Windows Recycle Bin; run it in Windows Editor.");
        TextureImporter template = GetTemplate();
        var settings = new TextureImporterSettings();
        template.ReadTextureSettings(settings);
        var entries = BuildEntries();
        var oldImages = new List<string>();
        foreach (Entry entry in entries)
        {
            if (EditorUtility.IsDirty(entry.Card))
                throw new InvalidOperationException("Save your pending card edits first: " + entry.Card.CardName);
            if (!File.Exists(entry.Source) && !File.Exists(entry.TargetAbsolute))
                throw new FileNotFoundException("Missing approved artwork: " + entry.Source);
            if (File.Exists(entry.Source) && File.Exists(entry.TargetAbsolute))
            {
                if (!Replaceable.Contains(Path.GetFileNameWithoutExtension(entry.Source)))
                    throw new InvalidOperationException("Refusing to overwrite an unexpected existing image: " + entry.Target);
                oldImages.Add(entry.TargetAbsolute);
            }
            // Validate platform-setting access before moving any files.
            if (File.Exists(entry.TargetAbsolute))
            {
                var importer = AssetImporter.GetAtPath(entry.Target) as TextureImporter;
                if (importer == null || new SerializedObject(importer).FindProperty("m_PlatformSettings") == null)
                    throw new InvalidOperationException("Cannot read target texture settings: " + entry.Target);
            }
        }

        // Only PNG contents go to Recycle Bin. Existing .meta files retain sprite GUIDs.
        RecycleImages(oldImages);
        foreach (Entry entry in entries)
            if (File.Exists(entry.Source)) File.Move(entry.Source, entry.TargetAbsolute);

        foreach (Entry entry in entries)
        {
            AssetDatabase.ImportAsset(entry.Target, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(entry.Target) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Texture importer missing: " + entry.Target);
            importer.SetTextureSettings(settings);
            importer.maxTextureSize = template.maxTextureSize;
            importer.textureCompression = template.textureCompression;
            importer.compressionQuality = template.compressionQuality;
            importer.crunchedCompression = template.crunchedCompression;
            var sourceData = new SerializedObject(template);
            var targetData = new SerializedObject(importer);
            targetData.CopyFromSerializedProperty(sourceData.FindProperty("m_PlatformSettings"));
            targetData.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
            if (!string.IsNullOrEmpty(entry.OriginalGuid) &&
                AssetDatabase.AssetPathToGUID(entry.Target) != entry.OriginalGuid)
                throw new InvalidOperationException("Existing image GUID unexpectedly changed: " + entry.Target);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.Target);
            if (sprite == null) throw new InvalidOperationException("Single Sprite import failed: " + entry.Target);
            var cardData = new SerializedObject(entry.Card);
            SerializedProperty artwork = cardData.FindProperty("artwork");
            if (artwork.objectReferenceValue != sprite)
            {
                artwork.objectReferenceValue = sprite;
                cardData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(entry.Card);
            }
        }
        VerifyEntries(entries, template);
        UpdateProgress(entries);
        Debug.Log("Card artwork import PASS: 37 images moved and assigned (17 Monsters, 17 Spells, 3 Traps); " +
            oldImages.Count + " old PNGs sent to Windows Recycle Bin; sprite/platform settings match Stone Guardian. " +
            "Existing metadata/GUIDs and the four excluded cards were preserved.");
    }

    [MenuItem("Card Game/Art/Verify 37 Approved Card Artworks")]
    public static void Verify()
    {
        RequireEditMode();
        VerifyEntries(BuildEntries(), GetTemplate());
        Debug.Log("Card artwork verification PASS: 37 correct category paths, sprite bindings and Stone Guardian import settings.");
    }

    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Stop Play Mode and wait for Unity compilation/import to finish first.");
    }

    private static TextureImporter GetTemplate()
    {
        var importer = AssetImporter.GetAtPath(Template) as TextureImporter;
        if (importer == null || importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single)
            throw new InvalidOperationException("Stone Guardian must have an existing Single Sprite importer: " + Template);
        if (EditorUtility.IsDirty(importer))
            throw new InvalidOperationException("Apply your pending Stone Guardian import settings first.");
        if (new SerializedObject(importer).FindProperty("m_PlatformSettings") == null)
            throw new InvalidOperationException("Cannot read Stone Guardian platform import settings.");
        return importer;
    }

    private static List<Entry> BuildEntries()
    {
        var cards = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Cards" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(card => card != null)
            .ToLookup(card => card.CardName.Replace(' ', '_'), StringComparer.Ordinal);
        var entries = new List<Entry>();
        foreach (string name in Approved)
        {
            CardData[] matches = cards[name].ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Need exactly one matching card: " + name);
            CardData card = matches[0];
            string folder;
            switch (card.Category)
            {
                case CardCategory.Monster: folder = "Monsters"; break;
                case CardCategory.Spell: folder = "Spells"; break;
                case CardCategory.Trap: folder = "Traps"; break;
                default: throw new InvalidOperationException("Unsupported category: " + card.CardName);
            }
            string directory = ArtRoot + "/" + folder;
            if (!AssetDatabase.IsValidFolder(directory))
                throw new DirectoryNotFoundException("Missing artwork folder: " + directory);
            string target = directory + "/" + name + ".png";
            string source = Path.GetFullPath(Path.Combine(SourceRoot, name + ".png"));
            string absolute = Path.GetFullPath(Path.Combine(ProjectRoot, target));
            RequireInside(source, SourceRoot);
            RequireInside(absolute, Path.Combine(ProjectRoot, ArtRoot));
            entries.Add(new Entry { Card = card, Source = source, Target = target,
                TargetAbsolute = absolute, OriginalGuid = AssetDatabase.AssetPathToGUID(target) });
        }
        if (entries.Count != 37 || entries.Count(e => e.Card.Category == CardCategory.Monster) != 17 ||
            entries.Count(e => e.Card.Category == CardCategory.Spell) != 17 ||
            entries.Count(e => e.Card.Category == CardCategory.Trap) != 3)
            throw new InvalidOperationException("Approved artwork/category counts differ from 17/17/3.");
        return entries;
    }

    private static void RequireInside(string path, string root)
    {
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Artwork path outside the intended directory: " + path);
    }

    private static void VerifyEntries(List<Entry> entries, TextureImporter template)
    {
        var expected = new TextureImporterSettings();
        template.ReadTextureSettings(expected);
        foreach (Entry entry in entries)
        {
            if (!File.Exists(entry.TargetAbsolute) || File.Exists(entry.Source))
                throw new InvalidOperationException("Artwork move incomplete: " + entry.Target);
            var importer = AssetImporter.GetAtPath(entry.Target) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing texture importer: " + entry.Target);
            var actual = new TextureImporterSettings();
            importer.ReadTextureSettings(actual);
            var differences = new List<string>();
            CompareSettings(expected, actual, "Sprite", differences);
            CompareSettings(template.GetDefaultPlatformTextureSettings(),
                importer.GetDefaultPlatformTextureSettings(), "Default platform", differences);
            foreach (string platform in PlatformNames(template).Union(PlatformNames(importer)))
            {
                TextureImporterPlatformSettings sourcePlatform = template.GetPlatformTextureSettings(platform);
                TextureImporterPlatformSettings targetPlatform = importer.GetPlatformTextureSettings(platform);
                if (sourcePlatform.overridden != targetPlatform.overridden)
                    differences.Add(platform + ".overridden");
                else if (sourcePlatform.overridden)
                    CompareSettings(sourcePlatform, targetPlatform, platform, differences);
            }
            if (differences.Count > 0)
                throw new InvalidOperationException("Importer differs from Stone Guardian: " + entry.Target +
                    "\n" + string.Join("\n", differences));
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.Target);
            if (sprite == null || entry.Card.Artwork != sprite || AssetDatabase.GetAssetPath(entry.Card.Artwork) != entry.Target)
                throw new InvalidOperationException("Card artwork binding differs: " + entry.Card.CardName);
        }
    }

    private static void CompareSettings(object expected, object actual, string label, List<string> differences)
    {
        // Compare supported public configuration values, not native backing fields or array ordering.
        foreach (PropertyInfo property in expected.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0 ||
                Attribute.IsDefined(property, typeof(ObsoleteAttribute))) continue;
            object wanted = property.GetValue(expected, null);
            object found = property.GetValue(actual, null);
            bool equal = wanted is float wantedFloat && found is float foundFloat
                ? Mathf.Approximately(wantedFloat, foundFloat) : Equals(wanted, found);
            if (!equal) differences.Add(label + "." + property.Name + ": expected " + wanted + ", found " + found);
        }
    }

    private static IEnumerable<string> PlatformNames(TextureImporter importer)
    {
        SerializedProperty platforms = new SerializedObject(importer).FindProperty("m_PlatformSettings");
        for (int i = 0; i < platforms.arraySize; i++)
        {
            string name = platforms.GetArrayElementAtIndex(i).FindPropertyRelative("m_BuildTarget").stringValue;
            if (name != "DefaultTexturePlatform") yield return name;
        }
        // Unity may materialize non-overridden platform entries on import. Those inherit the default
        // configuration and must not fail verification merely because their stored records differ.
    }

    private static void UpdateProgress(List<Entry> entries)
    {
        string path = Path.Combine(ProjectRoot, "Artworks", "ARTWORK_PROGRESS.md");
        if (!File.Exists(path)) return;
        string text = File.ReadAllText(path);
        foreach (Entry entry in entries)
            text = text.Replace("File: Cards/" + Path.GetFileName(entry.Source), "File: " + entry.Target);
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileOperation
    {
        public IntPtr window;
        public uint operation;
        [MarshalAs(UnmanagedType.LPWStr)] public string from;
        [MarshalAs(UnmanagedType.LPWStr)] public string to;
        public ushort flags;
        [MarshalAs(UnmanagedType.Bool)] public bool aborted;
        public IntPtr mappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string title;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref FileOperation operation);

    private static void RecycleImages(List<string> paths)
    {
        if (paths.Count == 0) return;
        foreach (string path in paths) RequireInside(path, Path.Combine(ProjectRoot, ArtRoot, "Monsters"));
        var operation = new FileOperation { operation = 3,
            from = string.Join("\0", paths) + "\0\0", flags = 0x0454 };
        int result = SHFileOperation(ref operation);
        if (result != 0 || operation.aborted || paths.Any(File.Exists))
            throw new IOException("Could not send all old artwork PNGs to Windows Recycle Bin (code " + result + ").");
    }
}

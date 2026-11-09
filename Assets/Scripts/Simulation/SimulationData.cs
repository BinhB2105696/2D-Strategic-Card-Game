using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

[Serializable]
public sealed class SimulationConfig
{
    public int schemaVersion = 1;
    public string projectRoot;
    public int seedCount = 5;
    public int firstSeed = 1;
    public int warmupMatches = 2;
    public int maxTurns = 200;
    public int maxActions = 10000;
    public double maxMatchSeconds = 60;
    public double decisionWarningMs = 1000;
    public int detailedMatches = 4;
    public SimulationMatchup[] matchups;
}

[Serializable]
public sealed class SimulationMatchup
{
    public string player0Bot = "Normal", player1Bot = "Easy";
    public int deck0, deck1;
    public bool swapBots = true;
}

public static class SimulationJson
{
    public static string Encode(object value)
    {
        var text = new StringBuilder(); Write(text, value, 0); return text.ToString();
    }

    private static void Write(StringBuilder text, object value, int depth)
    {
        if (depth > 32) throw new InvalidOperationException("Snapshot contains a cycle or exceeds depth 32.");
        if (value == null) { text.Append("null"); return; }
        if (value is string || value is Enum) { Quote(text, value.ToString()); return; }
        if (value is bool) { text.Append((bool)value ? "true" : "false"); return; }
        if (value is IConvertible && !(value is char))
        {
            string number = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (number == "NaN" || number.Contains("Infinity")) throw new InvalidOperationException("Non-finite metric.");
            text.Append(number); return;
        }
        if (value is IDictionary map)
        {
            text.Append('{'); bool first = true;
            foreach (string key in map.Keys.Cast<string>().OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!first) text.Append(','); first = false; Quote(text, key); text.Append(':'); Write(text, map[key], depth + 1);
            }
            text.Append('}'); return;
        }
        if (value is IEnumerable list)
        {
            text.Append('['); bool first = true;
            foreach (object item in list) { if (!first) text.Append(','); first = false; Write(text, item, depth + 1); }
            text.Append(']'); return;
        }
        var properties = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (property.CanRead && property.GetIndexParameters().Length == 0) properties[property.Name] = property.GetValue(value);
        foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            properties[field.Name] = field.GetValue(value);
        // Modifier parameters are private immutable fields, not Unity objects.
        if (value is IContinuousModifier)
        {
            properties["implementation"] = value.GetType().FullName;
            foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                properties[field.Name] = field.GetValue(value);
        }
        Write(text, properties, depth + 1);
    }

    private static void Quote(StringBuilder text, string value)
    {
        text.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\n': text.Append("\\n"); break;
                case '\r': text.Append("\\r"); break;
                case '\t': text.Append("\\t"); break;
                default: if (c < 32) text.Append("\\u" + ((int)c).ToString("x4")); else text.Append(c); break;
            }
        }
        text.Append('"');
    }

    public static Dictionary<string, object> Object(params object[] pairs)
    {
        var result = new Dictionary<string, object>();
        for (int i = 0; i < pairs.Length; i += 2) result.Add((string)pairs[i], pairs[i + 1]);
        return result;
    }

    public static object Action(GameAction action) => Object("type", action.Type.ToString(), "player", action.PlayerIndex,
        "source", action.SourceInstanceId, "targets", action.TargetInstanceIds.ToArray(), "tributes", action.TributeInstanceIds.ToArray(),
        "zone", action.ZoneIndex, "amount", action.Amount, "position", action.BattlePosition.ToString());

    public static string Hash(string text)
    {
        using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }

    public static void Atomic(string path, object value)
    {
        string pending = path + ".tmp";
        File.WriteAllText(pending, Encode(value), new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(pending, path, null); else File.Move(pending, path);
    }

    public static string ResultsRoot(string projectRoot)
    {
        string root = Path.GetFullPath(projectRoot ?? "");
        if (!string.Equals(Path.GetPathRoot(root), @"D:\", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(Path.Combine(root, "Assets")))
            throw new InvalidOperationException("projectRoot must identify this Unity project on drive D.");
        return Path.Combine(root, "SimulationResults");
    }
}

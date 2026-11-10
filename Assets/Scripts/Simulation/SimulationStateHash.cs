using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

// Diagnostic only. No field writes, getters on gameplay objects, or policy input.
// Reflection is confined to the Windows/Mono simulator's pure state graph.
public static class SimulationStateHash
{
    public const int Schema = 1;
    private static readonly Dictionary<Type, FieldInfo[]> Fields = new Dictionary<Type, FieldInfo[]>();

    public static string Capture(GameState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        var writer = new Writer();
        writer.Text.Append("game-state-graph/").Append(Schema).Append(';');
        writer.Write(state, 0);
        return SimulationJson.Hash(writer.Text.ToString());
    }

    private sealed class Identity : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }

    private sealed class Writer
    {
        public readonly StringBuilder Text = new StringBuilder();
        private readonly Dictionary<object, int> seen = new Dictionary<object, int>(new Identity());

        private void Token(string value) => Text.Append(value.Length).Append(':').Append(value).Append(';');

        public void Write(object value, int depth)
        {
            if (depth > 128) throw new InvalidOperationException("State graph exceeds supported depth.");
            if (value == null) { Text.Append("null;"); return; }
            Type type = value.GetType();
            Token(type.FullName);
            if (value is string text) { Token(text); return; }
            if (type.IsPrimitive || type.IsEnum || value is decimal)
            {
                if (value is double d && (double.IsNaN(d) || double.IsInfinity(d)) ||
                    value is float f && (float.IsNaN(f) || float.IsInfinity(f)))
                    throw new InvalidOperationException("Non-finite state value.");
                Token(type.IsEnum ? Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture) :
                    value is double number ? number.ToString("R", CultureInfo.InvariantCulture) :
                    value is float single ? single.ToString("R", CultureInfo.InvariantCulture) :
                    Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            if (seen.TryGetValue(value, out int reference)) { Text.Append("ref:").Append(reference).Append(';'); return; }
            seen.Add(value, seen.Count + 1);
            Text.Append("id:").Append(seen.Count).Append(';');
            if (value is IDictionary dictionary)
            {
                Comparer(type, value);
                var keys = dictionary.Keys.Cast<object>().Select(key => new { Key = key, Sort = ScalarKey(key) })
                    .OrderBy(key => key.Sort, StringComparer.Ordinal).ToArray();
                Text.Append(keys.Length).Append('{');
                foreach (var key in keys) { Write(key.Key, depth + 1); Write(dictionary[key.Key], depth + 1); }
                Text.Append('}'); return;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                Comparer(type, value);
                var entries = ((IEnumerable)value).Cast<object>().OrderBy(ScalarKey, StringComparer.Ordinal).ToArray();
                Text.Append(entries.Length).Append('{');
                foreach (object item in entries) Write(item, depth + 1);
                Text.Append('}'); return;
            }
            if (value is IList list)
            {
                // Deck/hand/zone/chain/modifier order is intentionally preserved.
                Text.Append(list.Count).Append('[');
                foreach (object item in list) Write(item, depth + 1);
                Text.Append(']'); return;
            }
            if (type.Assembly != typeof(GameState).Assembly || value is UnityEngine.Object ||
                (!type.IsSerializable && !(value is IContinuousModifier)))
                throw new InvalidOperationException("Unsupported state type; update hash schema/adapter: " + type.FullName);
            FieldInfo[] fields;
            lock (Fields)
            {
                if (!Fields.TryGetValue(type, out fields))
                {
                    var collected = new List<FieldInfo>();
                    for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
                        collected.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
                    fields = collected.OrderBy(field => field.DeclaringType.FullName + "/" + field.Name, StringComparer.Ordinal).ToArray();
                    Fields.Add(type, fields);
                }
            }
            Text.Append(fields.Length).Append('{');
            foreach (FieldInfo field in fields)
            {
                Token(field.DeclaringType.FullName + "/" + field.Name);
                Write(field.GetValue(value), depth + 1);
            }
            Text.Append('}');
        }

        private void Comparer(Type type, object value)
        {
            object comparer = type.GetProperty("Comparer").GetValue(value);
            Type key = type.GetGenericArguments()[0];
            object standard = typeof(EqualityComparer<>).MakeGenericType(key).GetProperty("Default").GetValue(null);
            if (ReferenceEquals(comparer, standard)) Token("default-comparer");
            else if (ReferenceEquals(comparer, StringComparer.Ordinal)) Token("ordinal-comparer");
            else if (ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)) Token("ordinal-ignore-case-comparer");
            else throw new InvalidOperationException("Unsupported state collection comparer: " + comparer.GetType().FullName);
        }

        private static string ScalarKey(object key)
        {
            if (key == null || !(key is string || key.GetType().IsPrimitive || key.GetType().IsEnum))
                throw new InvalidOperationException("Only scalar state collection keys are supported.");
            return key.GetType().FullName + ":" + Convert.ToString(key, CultureInfo.InvariantCulture);
        }
    }
}

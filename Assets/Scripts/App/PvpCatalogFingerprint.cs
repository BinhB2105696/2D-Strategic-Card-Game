using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

// Immutable rule definitions, including modifier parameters. No runtime state or Unity asset IDs.
public static class PvpCatalogFingerprint
{
    public static string Compute(IEnumerable<CardDefinition> cards)
    {
        var text = new StringBuilder("pvp-contract/2;");
        Append(text, MatchHeader.CurrentRulesVersion, 0);
        Append(text, MatchHeader.CurrentCardCatalogVersion, 0);
        Append(text, GameConfig.CreateDefault(), 0);
        Append(text, cards.OrderBy(card => card.CardId, StringComparer.Ordinal).ToArray(), 0);
        using (SHA256 hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())))
                .Replace("-", "").ToLowerInvariant();
    }

    private static void Append(StringBuilder text, object value, int depth)
    {
        if (depth > 24) throw new InvalidOperationException("Unsupported cyclic rule definition.");
        if (value == null) { text.Append("null;"); return; }
        Type type = value.GetType();
        text.Append(type.FullName).Append(':');
        if (value is string word)
        { text.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(word))).Append(';'); return; }
        if (type.IsEnum || type.IsPrimitive || value is decimal)
        { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
        if (value is IEnumerable sequence)
        {
            text.Append('[');
            foreach (object item in sequence) Append(text, item, depth + 1);
            text.Append("];"); return;
        }
        // Fail closed on an unsupported definition instead of claiming compatibility.
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(item => item.CanRead && item.GetIndexParameters().Length == 0)
            .OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        var configFields = new List<FieldInfo>();
        if (value is IContinuousModifier)
        {
            // Existing continuous modifiers keep their immutable amounts private.
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
                configFields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            if (configFields.Any(field => !field.IsInitOnly))
                throw new InvalidOperationException("Mutable continuous modifier configuration is unsupported.");
        }
        else configFields.AddRange(type.GetFields(BindingFlags.Instance | BindingFlags.Public));
        var fields = configFields.OrderBy(item => item.DeclaringType.FullName + "." + item.Name,
            StringComparer.Ordinal).ToArray();
        if (properties.Length + fields.Length == 0)
            throw new InvalidOperationException("Unsupported public rule definition: " + type.FullName);
        text.Append('{');
        foreach (PropertyInfo property in properties)
        { text.Append(property.Name).Append('='); Append(text, property.GetValue(value), depth + 1); }
        foreach (FieldInfo field in fields)
        { text.Append(field.Name).Append('='); Append(text, field.GetValue(value), depth + 1); }
        text.Append("};");
    }
}

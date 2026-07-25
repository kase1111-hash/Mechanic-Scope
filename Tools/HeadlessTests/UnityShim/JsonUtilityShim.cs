// UnityEngine.JsonUtility stand-in for the headless test harness.
//
// Reproduces the subset of Unity's JsonUtility semantics that the project relies on:
//
//   * Binds JSON object keys to *fields* (never properties), matching by exact name.
//   * Serializes public instance fields, plus private fields marked [SerializeField].
//   * Skips [NonSerialized], static, const and readonly fields.
//   * Ignores unknown JSON keys instead of throwing.
//   * Leaves fields absent from the JSON at their default / initializer value.
//   * Throws ArgumentException on malformed JSON (Unity does the same).
//   * "null" as the whole document deserializes to null.
//
// Known, deliberate divergences from Unity — none are reachable by the current tests:
//   * An explicit JSON null for an array/List field yields null here; Unity is inconsistent
//     across versions. Every consumer in this project null-checks, so behaviour matches either way.
//   * Enums are accepted as both number and string; Unity only writes numbers.
//   * No support for Dictionary (Unity does not support it either — both ignore such fields).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace UnityEngine
{
    public static class JsonUtility
    {
        private const BindingFlags FieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static T FromJson<T>(string json)
        {
            object result = FromJson(json, typeof(T));
            return result == null ? default : (T)result;
        }

        public static object FromJson(string json, Type type)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            object parsed = JsonParser.Parse(json);
            if (parsed == null) return null;

            return Bind(parsed, type);
        }

        public static void FromJsonOverwrite(string json, object target)
        {
            if (target == null) return;
            object parsed = JsonParser.Parse(json);
            if (parsed is Dictionary<string, object> map)
            {
                PopulateFields(map, target, target.GetType());
            }
        }

        public static string ToJson(object obj) => ToJson(obj, false);

        public static string ToJson(object obj, bool prettyPrint)
        {
            if (obj == null) return "{}";

            var sb = new StringBuilder();
            WriteValue(sb, obj, prettyPrint, 0);
            return sb.ToString();
        }

        // === Deserialization ===

        private static object Bind(object parsed, Type type)
        {
            if (parsed == null) return null;

            if (type == typeof(string)) return parsed as string ?? Convert.ToString(parsed, CultureInfo.InvariantCulture);
            if (type == typeof(bool)) return parsed is bool b ? b : Convert.ToBoolean(parsed, CultureInfo.InvariantCulture);
            if (type.IsEnum) return BindEnum(parsed, type);
            if (IsNumeric(type)) return ConvertNumber(parsed, type);

            if (type.IsArray)
            {
                if (!(parsed is List<object> items)) return null;
                Type element = type.GetElementType();
                Array array = Array.CreateInstance(element, items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    array.SetValue(Bind(items[i], element), i);
                }
                return array;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (!(parsed is List<object> items)) return null;
                Type element = type.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(type);
                foreach (object item in items)
                {
                    list.Add(Bind(item, element));
                }
                return list;
            }

            if (parsed is Dictionary<string, object> map)
            {
                object instance = Activator.CreateInstance(type);
                PopulateFields(map, instance, type);
                return instance;
            }

            return null;
        }

        private static void PopulateFields(Dictionary<string, object> map, object instance, Type type)
        {
            foreach (FieldInfo field in SerializableFields(type))
            {
                if (!map.TryGetValue(field.Name, out object raw)) continue; // absent -> keep default

                if (raw == null)
                {
                    if (!field.FieldType.IsValueType) field.SetValue(instance, null);
                    continue;
                }

                try
                {
                    field.SetValue(instance, Bind(raw, field.FieldType));
                }
                catch (Exception)
                {
                    // Unity silently drops values it cannot coerce rather than failing the parse.
                }
            }
        }

        private static IEnumerable<FieldInfo> SerializableFields(Type type)
        {
            var seen = new HashSet<string>();
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(FieldFlags | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsLiteral || field.IsInitOnly) continue;
                    if (field.IsDefined(typeof(NonSerializedAttribute), false)) continue;

                    // Public fields serialize by default; private ones need [SerializeField].
                    bool serialized = field.IsPublic || field.IsDefined(typeof(SerializeField), false);
                    if (!serialized) continue;

                    if (seen.Add(field.Name)) yield return field;
                }
            }
        }

        private static object BindEnum(object parsed, Type type)
        {
            if (parsed is string s)
            {
                return Enum.TryParse(type, s, true, out object value)
                    ? value
                    : Enum.ToObject(type, 0);
            }
            return Enum.ToObject(type, Convert.ToInt64(parsed, CultureInfo.InvariantCulture));
        }

        private static bool IsNumeric(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal) || type == typeof(char);
        }

        private static object ConvertNumber(object parsed, Type type)
        {
            if (parsed is bool flag) parsed = flag ? 1 : 0;
            if (parsed is string text)
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedText))
                {
                    return Activator.CreateInstance(type);
                }
                parsed = parsedText;
            }
            return Convert.ChangeType(parsed, type, CultureInfo.InvariantCulture);
        }

        // === Serialization ===

        private static void WriteValue(StringBuilder sb, object value, bool pretty, int depth)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    return;
                case string s:
                    WriteString(sb, s);
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case Enum e:
                    sb.Append(Convert.ToInt64(e, CultureInfo.InvariantCulture));
                    return;
            }

            Type type = value.GetType();

            if (IsNumeric(type))
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IEnumerable sequence && !(value is IDictionary))
            {
                WriteArray(sb, sequence, pretty, depth);
                return;
            }

            WriteObject(sb, value, type, pretty, depth);
        }

        private static void WriteArray(StringBuilder sb, IEnumerable sequence, bool pretty, int depth)
        {
            sb.Append('[');
            bool first = true;
            foreach (object item in sequence)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, pretty, depth + 1);
                WriteValue(sb, item, pretty, depth + 1);
            }
            if (!first) NewLine(sb, pretty, depth);
            sb.Append(']');
        }

        private static void WriteObject(StringBuilder sb, object value, Type type, bool pretty, int depth)
        {
            sb.Append('{');
            bool first = true;
            foreach (FieldInfo field in SerializableFields(type))
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, pretty, depth + 1);
                WriteString(sb, field.Name);
                sb.Append(pretty ? ": " : ":");
                WriteValue(sb, field.GetValue(value), pretty, depth + 1);
            }
            if (!first) NewLine(sb, pretty, depth);
            sb.Append('}');
        }

        private static void NewLine(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n').Append(' ', depth * 4);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>
    /// Recursive-descent JSON reader producing Dictionary&lt;string, object&gt; / List&lt;object&gt; /
    /// string / double / bool / null. Throws ArgumentException on malformed input, as Unity does.
    /// </summary>
    internal static class JsonParser
    {
        public static object Parse(string json)
        {
            int index = 0;
            SkipWhitespace(json, ref index);
            object value = ParseValue(json, ref index);
            SkipWhitespace(json, ref index);

            if (index != json.Length)
            {
                throw new ArgumentException($"Invalid JSON: unexpected trailing content at position {index}");
            }
            return value;
        }

        private static object ParseValue(string json, ref int index)
        {
            if (index >= json.Length) throw new ArgumentException("Invalid JSON: unexpected end of input");

            char c = json[index];
            switch (c)
            {
                case '{': return ParseObject(json, ref index);
                case '[': return ParseArray(json, ref index);
                case '"': return ParseString(json, ref index);
                case 't': return ParseLiteral(json, ref index, "true", true);
                case 'f': return ParseLiteral(json, ref index, "false", false);
                case 'n': return ParseLiteral(json, ref index, "null", null);
                default: return ParseNumber(json, ref index);
            }
        }

        private static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var map = new Dictionary<string, object>();
            index++; // '{'
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == '}') { index++; return map; }

            while (true)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != '"')
                {
                    throw new ArgumentException($"Invalid JSON: expected object key at position {index}");
                }

                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);

                if (index >= json.Length || json[index] != ':')
                {
                    throw new ArgumentException($"Invalid JSON: expected ':' at position {index}");
                }
                index++;

                SkipWhitespace(json, ref index);
                map[key] = ParseValue(json, ref index);
                SkipWhitespace(json, ref index);

                if (index >= json.Length) throw new ArgumentException("Invalid JSON: unterminated object");
                if (json[index] == ',') { index++; continue; }
                if (json[index] == '}') { index++; return map; }

                throw new ArgumentException($"Invalid JSON: expected ',' or '}}' at position {index}");
            }
        }

        private static List<object> ParseArray(string json, ref int index)
        {
            var items = new List<object>();
            index++; // '['
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ']') { index++; return items; }

            while (true)
            {
                SkipWhitespace(json, ref index);
                items.Add(ParseValue(json, ref index));
                SkipWhitespace(json, ref index);

                if (index >= json.Length) throw new ArgumentException("Invalid JSON: unterminated array");
                if (json[index] == ',') { index++; continue; }
                if (json[index] == ']') { index++; return items; }

                throw new ArgumentException($"Invalid JSON: expected ',' or ']' at position {index}");
            }
        }

        private static string ParseString(string json, ref int index)
        {
            index++; // opening quote
            var sb = new StringBuilder();

            while (true)
            {
                if (index >= json.Length) throw new ArgumentException("Invalid JSON: unterminated string");

                char c = json[index++];
                if (c == '"') return sb.ToString();

                if (c != '\\') { sb.Append(c); continue; }

                if (index >= json.Length) throw new ArgumentException("Invalid JSON: unterminated escape");
                char escape = json[index++];
                switch (escape)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length) throw new ArgumentException("Invalid JSON: bad \\u escape");
                        sb.Append((char)Convert.ToInt32(json.Substring(index, 4), 16));
                        index += 4;
                        break;
                    default:
                        throw new ArgumentException($"Invalid JSON: unknown escape '\\{escape}'");
                }
            }
        }

        private static object ParseNumber(string json, ref int index)
        {
            int start = index;
            if (index < json.Length && (json[index] == '-' || json[index] == '+')) index++;

            while (index < json.Length &&
                   (char.IsDigit(json[index]) || json[index] == '.' ||
                    json[index] == 'e' || json[index] == 'E' ||
                    json[index] == '+' || json[index] == '-'))
            {
                index++;
            }

            string token = json.Substring(start, index - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new ArgumentException($"Invalid JSON: '{token}' is not a valid value at position {start}");
            }
            return value;
        }

        private static object ParseLiteral(string json, ref int index, string literal, object value)
        {
            if (index + literal.Length > json.Length ||
                string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
            {
                throw new ArgumentException($"Invalid JSON: unexpected token at position {index}");
            }
            index += literal.Length;
            return value;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }
    }
}

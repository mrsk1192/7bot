using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace mnetSevenDaysBridge
{
    public sealed class BridgeJson
    {
        public string Serialize(object value)
        {
            var builder = new StringBuilder(256);
            WriteValue(builder, value);
            return builder.ToString();
        }

        public Dictionary<string, object> DeserializeObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("JSON input was empty.");
            }

            var parser = new JsonParser(json);
            var root = parser.ParseValue() as Dictionary<string, object>;
            if (root == null)
            {
                throw new InvalidOperationException("JSON root must be an object.");
            }

            return root;
        }

        public CommandRequest DeserializeCommandRequest(Stream inputStream)
        {
            using (var reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                var body = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(body))
                {
                    throw new InvalidOperationException("Request body was empty.");
                }

                var root = DeserializeObject(body);

                if (!TryGetCaseInsensitive(root, "Command", out var commandValue) || commandValue == null)
                {
                    throw new InvalidOperationException("Command request did not contain a command.");
                }

                var command = commandValue.ToString();
                if (string.IsNullOrWhiteSpace(command))
                {
                    throw new InvalidOperationException("Command request did not contain a command.");
                }

                Dictionary<string, object> arguments = null;
                if (TryGetCaseInsensitive(root, "Arguments", out var argumentsValue) && argumentsValue is Dictionary<string, object> parsedArguments)
                {
                    arguments = parsedArguments;
                }

                return new CommandRequest
                {
                    Command = command,
                    Arguments = arguments
                };
            }
        }

        private static bool TryGetCaseInsensitive(Dictionary<string, object> values, string key, out object value)
        {
            foreach (var pair in values)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static void WriteValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is string stringValue)
            {
                WriteString(builder, stringValue);
                return;
            }

            if (value is bool boolValue)
            {
                builder.Append(boolValue ? "true" : "false");
                return;
            }

            if (IsNumeric(value))
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary dictionary)
            {
                WriteDictionary(builder, dictionary);
                return;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                WriteArray(builder, enumerable);
                return;
            }

            WriteObject(builder, value);
        }

        private static void WriteDictionary(StringBuilder builder, IDictionary dictionary)
        {
            builder.Append('{');
            var first = true;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                WriteString(builder, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                builder.Append(':');
                WriteValue(builder, entry.Value);
                first = false;
            }

            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, IEnumerable values)
        {
            builder.Append('[');
            var first = true;
            foreach (var item in values)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                WriteValue(builder, item);
                first = false;
            }

            builder.Append(']');
        }

        private static void WriteObject(StringBuilder builder, object value)
        {
            builder.Append('{');
            var first = true;
            var type = value.GetType();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(',');
                }

                WriteString(builder, property.Name);
                builder.Append(':');
                WriteValue(builder, property.GetValue(value, null));
                first = false;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!first)
                {
                    builder.Append(',');
                }

                WriteString(builder, field.Name);
                builder.Append(':');
                WriteValue(builder, field.GetValue(value));
                first = false;
            }

            builder.Append('}');
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private static bool IsNumeric(object value)
        {
            return value is sbyte
                || value is byte
                || value is short
                || value is ushort
                || value is int
                || value is uint
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal;
        }

        private sealed class JsonParser
        {
            private readonly string json;
            private int index;

            public JsonParser(string json)
            {
                this.json = json ?? throw new ArgumentNullException(nameof(json));
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (index >= json.Length)
                {
                    throw new InvalidOperationException("Unexpected end of JSON input.");
                }

                switch (json[index])
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return ParseString();
                    case 't':
                        return ParseLiteral("true", true);
                    case 'f':
                        return ParseLiteral("false", false);
                    case 'n':
                        return ParseLiteral("null", null);
                    default:
                        return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                Expect('{');
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    var value = ParseValue();
                    result[key] = value;
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();
                Expect('[');
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                var builder = new StringBuilder();
                while (index < json.Length)
                {
                    var character = json[index++];
                    if (character == '"')
                    {
                        return builder.ToString();
                    }

                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (index >= json.Length)
                    {
                        throw new InvalidOperationException("Unexpected end of JSON string escape.");
                    }

                    var escaped = json[index++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            if (index + 4 > json.Length)
                            {
                                throw new InvalidOperationException("Invalid unicode escape sequence.");
                            }

                            var hex = json.Substring(index, 4);
                            builder.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            index += 4;
                            break;
                        default:
                            throw new InvalidOperationException("Unsupported escape sequence: \\" + escaped);
                    }
                }

                throw new InvalidOperationException("Unterminated JSON string.");
            }

            private object ParseLiteral(string token, object value)
            {
                if (index + token.Length > json.Length || string.Compare(json, index, token, 0, token.Length, StringComparison.Ordinal) != 0)
                {
                    throw new InvalidOperationException("Invalid JSON literal at position " + index);
                }

                index += token.Length;
                return value;
            }

            private object ParseNumber()
            {
                var start = index;
                if (json[index] == '-')
                {
                    index++;
                }

                while (index < json.Length && char.IsDigit(json[index]))
                {
                    index++;
                }

                var isFloat = false;
                if (index < json.Length && json[index] == '.')
                {
                    isFloat = true;
                    index++;
                    while (index < json.Length && char.IsDigit(json[index]))
                    {
                        index++;
                    }
                }

                if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
                {
                    isFloat = true;
                    index++;
                    if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                    {
                        index++;
                    }

                    while (index < json.Length && char.IsDigit(json[index]))
                    {
                        index++;
                    }
                }

                var token = json.Substring(start, index - start);
                if (isFloat)
                {
                    return double.Parse(token, CultureInfo.InvariantCulture);
                }

                if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    if (integer >= int.MinValue && integer <= int.MaxValue)
                    {
                        return (int)integer;
                    }

                    return integer;
                }

                throw new InvalidOperationException("Invalid JSON number: " + token);
            }

            private void SkipWhitespace()
            {
                while (index < json.Length && char.IsWhiteSpace(json[index]))
                {
                    index++;
                }
            }

            private bool TryConsume(char expected)
            {
                if (index < json.Length && json[index] == expected)
                {
                    index++;
                    return true;
                }

                return false;
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (index >= json.Length || json[index] != expected)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Expected '{0}' at position {1}.",
                            expected,
                            index));
                }

                index++;
            }
        }
    }
}

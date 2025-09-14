using Google.Protobuf.WellKnownTypes;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AWise.AiExtensionsForGemini;

internal class ProtoJsonConversions
{
    // Used when the LLM requests a function call. Converts the LLM's values into values that
    // can be used to invoke a .NET function.
    // The default reflection-based invoker processes these values here:
    // https://github.com/dotnet/extensions/blob/d299e16f15234f9808b18fef50bf7770113fb4b2/src/Libraries/Microsoft.Extensions.AI.Abstractions/Functions/AIFunctionFactory.cs#L850-L855
    // Unless we exactly match the type of the function parameter, the library will serialize
    // our object into JSON and then deserialize it. To avoid an extra serialization round-trip,
    // we aim for the code path where JsonSerializer is used to deserialize a JsonNode.
    internal static JsonNode? ConvertValueToJsonNode(Value value)
    {
        switch (value.KindCase)
        {
            case Value.KindOneofCase.NullValue:
                return null;
            case Value.KindOneofCase.NumberValue:
                return JsonValue.Create(value.NumberValue);
            case Value.KindOneofCase.StringValue:
                return JsonValue.Create(value.StringValue);
            case Value.KindOneofCase.BoolValue:
                return JsonValue.Create(value.BoolValue);
            case Value.KindOneofCase.StructValue:
                return new JsonObject(value.StructValue.Fields.Select(kvp => KeyValuePair.Create(kvp.Key, ConvertValueToJsonNode(kvp.Value))));
            case Value.KindOneofCase.ListValue:
                return new JsonArray(value.ListValue.Values.Select(v => ConvertValueToJsonNode(v)).ToArray());
            case Value.KindOneofCase.None:
            default:
                throw new InvalidOperationException("Unexpected Value kind: " + value.KindCase);
        }
    }

    // Use to convert tool parameters and return values.
    internal static Value ConvertObjectToValue(object? element)
    {
        if (element is null)
        {
            return new Value()
            {
                NullValue = NullValue.NullValue,
            };
        }
        else if (element is JsonElement jsonElement)
        {
            // Return values serialized here:
            // https://github.com/dotnet/extensions/blob/d299e16f15234f9808b18fef50bf7770113fb4b2/src/Libraries/Microsoft.Extensions.AI.Abstractions/Functions/AIFunctionFactory.cs#L1033-L1046
            return ConvertJsonElementToValue(jsonElement);
        }
        else if (element is JsonNode jsonNode)
        {
            // Parameter values we converted to JSON nodes.
            return ConvertJsonNodeToValue(jsonNode);
        }
        else
        {
            // TODO: test coverage to see if we can hit this.
            throw new InvalidOperationException("Unexpected type: " + element.GetType().Name);
        }
    }

    private static Value ConvertJsonNodeToValue(JsonNode? node)
    {
        if (node is null)
        {
            return new Value()
            {
                NullValue = NullValue.NullValue,
            };
        }

        switch (node.GetValueKind())
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return new Value()
                {
                    NullValue = NullValue.NullValue,
                };
            case JsonValueKind.Object:
                var s = new Struct();
                foreach (var kvp in node.AsObject())
                {
                    s.Fields.Add(kvp.Key, ConvertJsonNodeToValue(kvp.Value));
                }
                return new Value()
                {
                    StructValue = s,
                };
            case JsonValueKind.Array:
                var list = new ListValue();
                foreach (var n in node.AsArray())
                {
                    list.Values.Add(ConvertJsonNodeToValue(n));
                }
                return new Value()
                {
                    ListValue = list,
                };
            case JsonValueKind.String:
                return new Value()
                {
                    StringValue = node.GetValue<string>(),
                };
            case JsonValueKind.Number:
                var value = node.AsValue();
                if (value.TryGetValue(out double d))
                {
                    return new Value()
                    {
                        NumberValue = d,
                    };
                }
                // We only construct numeric JsonNodes with doubles, so we should not get here.
                // TODO: test coverage to see if we can hit this.
                throw new InvalidOperationException("Could not get number.");
            case JsonValueKind.True:
                return new Value()
                {
                    BoolValue = true,
                };
            case JsonValueKind.False:
                return new Value()
                {
                    BoolValue = false,
                };
            default:
                throw new InvalidOperationException("Unexpected value kind: " + node.GetValueKind());
        }
    }

    // Used for converting JSON schema values
    internal static Value ConvertJsonElementToValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return new Value()
                {
                    NullValue = NullValue.NullValue,
                };
            case JsonValueKind.True:
                return new Value()
                {
                    BoolValue = true,
                };
            case JsonValueKind.False:
                return new Value()
                {
                    BoolValue = false,
                };
            case JsonValueKind.String:
                return new Value()
                {
                    StringValue = element.GetString()!,
                };
            case JsonValueKind.Number:
                if (element.TryGetDouble(out double d))
                {
                    return new Value()
                    {
                        NumberValue = d,
                    };
                }
                else
                {
                    throw new InvalidOperationException("Could not parse number: " + element.GetRawText());
                }
            case JsonValueKind.Array:
                var list = new ListValue();
                foreach (var item in element.EnumerateArray())
                {
                    list.Values.Add(ConvertJsonElementToValue(item));
                }
                return new Value()
                {
                    ListValue = list,
                };
            case JsonValueKind.Object:
                var s = new Struct();
                foreach (var item in element.EnumerateObject())
                {
                    s.Fields[item.Name] = ConvertJsonElementToValue(item.Value);
                }
                return new Value()
                {
                    StructValue = s,
                };
            default:
                throw new InvalidOperationException("Unexpected JsonValueKind: " + element.ValueKind);
        }
    }
}

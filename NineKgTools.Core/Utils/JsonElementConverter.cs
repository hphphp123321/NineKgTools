using System.Globalization;
using System.Text.Json;

namespace NineKgTools.Utils;

public static class JsonElementConverter
{
    public static Dictionary<string, string> ToStringDictionary(this JsonElement element)
    {
        var dictionary = new Dictionary<string, string>();

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                string? valueAsString;

                // 根据值的类型将其转换为字符串
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        valueAsString = prop.Value.GetString();
                        break;
                    case JsonValueKind.Number:
                        valueAsString = prop.Value.GetDecimal().ToString(CultureInfo.InvariantCulture);  // 使用 Decimal 避免精度损失
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        valueAsString = prop.Value.GetBoolean().ToString();
                        break;
                    case JsonValueKind.Object:
                    case JsonValueKind.Array:
                        // 对于复杂类型，获取它们的原始 JSON 文本
                        valueAsString = prop.Value.GetRawText();
                        break;
                    case JsonValueKind.Null:
                        valueAsString = "null";
                        break;
                    default:
                        valueAsString = prop.Value.GetRawText();
                        break;
                }

                if (valueAsString != null)
                {
                    dictionary.Add(prop.Name, valueAsString);
                }
            }
        }
        else
        {
            throw new InvalidOperationException("提供的 JSON 元素不是对象");
        }

        return dictionary;
    }
}
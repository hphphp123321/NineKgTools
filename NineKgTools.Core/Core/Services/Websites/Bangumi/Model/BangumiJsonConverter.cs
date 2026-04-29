using System.Text.Json;
using System.Text.Json.Serialization;

namespace NineKgTools.Core.Services.Websites.Bangumi.Model;

public class InfoBoxJsonConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected StartArray token");
        }

        var dictionary = new Dictionary<string, object>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return dictionary;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                string key = null;
                object value = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string propertyName = reader.GetString();
                        reader.Read();
                        if (propertyName == "key")
                        {
                            key = reader.GetString();
                        }
                        else if (propertyName == "value")
                        {
                            // Check the type of value
                            if (reader.TokenType == JsonTokenType.StartArray)
                            {
                                value = JsonSerializer.Deserialize<List<object>>(ref reader, options);
                            }
                            else
                            {
                                value = JsonSerializer.Deserialize<object>(ref reader, options);
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(key))
                {
                    dictionary.Add(key, value);
                }
            }
        }

        throw new JsonException("Expected EndArray token");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        throw new NotImplementedException("This converter does not support serialization.");
    }
}

public class PersonCareerConverter : JsonConverter<List<PersonCareer>>
{
    public override List<PersonCareer> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray token");

        var careers = new List<PersonCareer>();
        
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return careers;
            
            if (reader.TokenType == JsonTokenType.String)
            {
                var enumString = reader.GetString();
                if (Enum.TryParse(enumString, true, out PersonCareer career))
                {
                    careers.Add(career);
                }
                else
                {
                    throw new JsonException($"Unable to convert '{enumString}' to PersonCareer enum.");
                }
            }
        }

        throw new JsonException("Expected EndArray token");
    }

    public override void Write(Utf8JsonWriter writer, List<PersonCareer> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var career in value)
        {
            writer.WriteStringValue(career.ToString());
        }
        writer.WriteEndArray();
    }
}
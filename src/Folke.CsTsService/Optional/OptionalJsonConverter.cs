﻿namespace Folke.CsTsService.Optional
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);
        }

        public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var result = JsonSerializer.Deserialize<T>(ref reader, options);
            if (result == null)
                return Optional<T>.Undefined;
            return result;
        }

        public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
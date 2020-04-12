using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Folke.CsTsService.Optional
{
    public class OptionalJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type parameterType = typeToConvert.GetGenericArguments().Single();
            Type converterType = typeof(OptionalJsonConverter<>);
            return (JsonConverter)Activator.CreateInstance(converterType.MakeGenericType(parameterType));
        }
    }
}
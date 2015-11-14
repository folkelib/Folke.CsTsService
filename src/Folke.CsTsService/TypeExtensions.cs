using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Folke.CsTsService
{
    public static class TypeExtensions
    {
        public static bool HasBaseType(this Type type, string typeName)
        {
            if (type.GetTypeInfo().BaseType == null) return false;
            return type.GetTypeInfo().BaseType.Name == typeName || HasBaseType(type.GetTypeInfo().BaseType, typeName);
        }

        public static bool HasAttribute(this Type type, string attributeName)
        {
            return type.GetTypeInfo().CustomAttributes.Any(x => x.AttributeType.Name == attributeName);
        }

        public static bool HasAttribute(this ParameterInfo type, string attributeName)
        {
            return type.CustomAttributes.Any(x => x.AttributeType.Name == attributeName);
        }

        public static bool HasAttribute(this MethodInfo methodInfo, string attributeName)
        {
            return methodInfo.CustomAttributes.Any(x => x.AttributeType.Name == attributeName);
        }

        public static PropertyInfo GetProperty(this Type type, string propertyName)
        {
            return type.GetTypeInfo().DeclaredProperties.FirstOrDefault(x => x.Name == propertyName);
        }

        public static T GetAttributeProperty<T>(this Type type, string attributeName, string propertyName)
        {
            var attribute = type.GetTypeInfo().GetCustomAttributes().FirstOrDefault(x => x.GetType().Name == attributeName);
            if (attribute == null) return default(T);
            if (attribute.GetType().GetProperty(propertyName) == null) return default(T);
            return (T)attribute.GetType().GetProperty(propertyName).GetValue(attribute);
        }

        public static T GetAttributeProperty<T>(this MethodInfo type, string attributeName, string propertyName)
        {
            var attribute = type.GetCustomAttributes().FirstOrDefault(x => x.GetType().Name == attributeName);
            if (attribute == null) return default(T);
            if (attribute.GetType().GetProperty(propertyName) == null) return default(T);
            return (T)attribute.GetType().GetProperty(propertyName).GetValue(attribute);
        }

        public static IEnumerable<T> GetAttributeProperties<T>(this PropertyInfo type, string attributeName, string propertyName)
        {
            foreach (var attribute in type.GetCustomAttributes().Where(x => x.GetType().Name == attributeName))
            {
                yield return (T)attribute.GetType().GetProperty(propertyName).GetValue(attribute);
            }
        }

    }
}

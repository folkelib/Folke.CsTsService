using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Folke.CsTsService
{
    public static class ReflectionHelpers
    {
        /// <summary>
         /// Returns a value indicating whether a type (or one of its underlying types) is a dictionary (true) or not (false).
         /// </summary>
         /// <param name="type">The type.</param>
         /// <returns>True if a type (or one of its underlying types) is a dictionary, false otherwise.</returns>
        public static bool IsDictionary(Type type)
        {
            if (type.GetTypeInfo().IsInterface)
            {
                return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>);
            }
            return type.GetInterfaces().Any(IsDictionary);
        }
    }
}

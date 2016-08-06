using System;
using System.Linq;
using System.Reflection;

namespace Folke.CsTsService
{
    public static class TypeExtensions
    {
        public static bool InheritFrom(this Type type, Type baseType)
        {
            return type == baseType || (type.GetTypeInfo().BaseType != null && type.GetTypeInfo().BaseType.InheritFrom(baseType));
        }

        public static bool ImplementsInterface(this Type type, Type baseInterface)
        {
            if (type.GetTypeInfo().IsInterface && type.GetTypeInfo().IsGenericType && type.GetTypeInfo().GetGenericTypeDefinition() == baseInterface)
            {
                return true;
            }
            return type.GetTypeInfo().GetInterfaces().Any(x => x.ImplementsInterface(baseInterface));
        }
    }
}

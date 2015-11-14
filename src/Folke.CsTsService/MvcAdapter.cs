using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Folke.CsTsService
{
    public class MvcAdapter : IApiAdapter
    {
        public bool IsController(Type type)
        {
            return type.HasBaseType("Controller");
        }

        public bool IsObservableObject(Type type)
        {
            return true;
        }

        public string GetRoutePrefixName(Type type)
        {
            return type.GetAttributeProperty<string>("RouteAttribute", "Template");
        }

        public bool IsAction(MethodInfo methodInfo)
        {
            return methodInfo.HasAttribute("HttpGetAttribute") || methodInfo.HasAttribute("HttpPostAttribute")
                || methodInfo.HasAttribute("HttpDeleteAttribute") || methodInfo.HasAttribute("HttpPutAttribute");
        }

        public string GetRouteFormat(MethodInfo methodInfo)
        {
            if (methodInfo.HasAttribute("HttpGetAttribute"))
                return methodInfo.GetAttributeProperty<string>("HttpGetAttribute", "Template") ?? string.Empty;
            if (methodInfo.HasAttribute("HttpPostAttribute"))
                return methodInfo.GetAttributeProperty<string>("HttpPostAttribute", "Template") ?? string.Empty;
            if (methodInfo.HasAttribute("HttpPutAttribute"))
                return methodInfo.GetAttributeProperty<string>("HttpPutAttribute", "Template") ?? string.Empty;
            if (methodInfo.HasAttribute("HttpDeleteAttribute"))
                return methodInfo.GetAttributeProperty<string>("HttpDeleteAttribute", "Template") ?? string.Empty;
            return methodInfo.Name;
        }

        public Type GetReturnType(MethodInfo methodInfo)
        {
            var returnType = methodInfo.ReturnType;
            if (returnType.GetTypeInfo().IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                returnType = returnType.GenericTypeArguments[0];
            }

            if (returnType == typeof(void) || returnType.HasBaseType("IActionResult") || returnType == typeof(Task))
                returnType = null;
            return returnType;
        }

        public bool IsParameterFromUri(ParameterInfo parameterInfo)
        {
            return parameterInfo.HasAttribute("FromQuery");
        }

        public bool IsParameterFromBody(ParameterInfo parameterInfo)
        {
            return parameterInfo.HasAttribute("FromBody");
        }

        public bool IsPostAction(MethodInfo methodInfo)
        {
            return methodInfo.HasAttribute("HttpPost");
        }

        public bool IsDeleteAction(MethodInfo methodInfo)
        {
            return methodInfo.HasAttribute("HttpDelete");
        }

        public bool IsPutAction(MethodInfo methodInfo)
        {
            return methodInfo.HasAttribute("HttpPut");
        }

        public IEnumerable<Type> GetUnionTypes(PropertyInfo propertyInfo)
        {
            return Enumerable.Empty<Type>();
        }
    }
}

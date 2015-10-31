using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Folke.CsTsService
{
    public class WaAdapter : IApiAdapter
    {
        public bool IsController(Type type)
        {
            return type.HasBaseType("Controller");
        }
        
        public bool IsObservableObject(Type type)
        {
            return !type.HasAttribute("JsonAttribute") || type.GetAttributeProperty<bool>("JsonAttribute", "Observable");
        }

        public string GetRoutePrefixName(Type type)
        {
            return type.GetAttributeProperty<string>("RoutePrefixAttribute", "Name") ?? type.GetAttributeProperty<string>("RouteAttribute", "Template");
        }

        public bool IsAction(MethodInfo methodInfo)
        {
            return !methodInfo.HasAttribute("NonActionAttribute") && methodInfo.ReturnType.Name != "ActionResult"
                && (methodInfo.HasAttribute("RouteAttribute")
                || methodInfo.HasAttribute("HttpGetAttribute") || methodInfo.HasAttribute("HttpPostAttribute")
                || methodInfo.HasAttribute("HttpDeleteAttribute") || methodInfo.HasAttribute("HttpPutAttribute"));
        }

        public string GetRouteFormat(MethodInfo methodInfo)
        {
            var format = methodInfo.GetAttributeProperty<string>("RouteAttribute", "Format")
                ?? methodInfo.GetAttributeProperty<string>("RouteAttribute", "Template");
            if (format != null) return format;
            if (methodInfo.HasAttribute("HttpGetAttribute"))
                return methodInfo.GetAttributeProperty<string>("HttpGetAttribute", "Template") ?? string.Empty;
            if (methodInfo.HasAttribute("HttpPostAttribute"))
                return methodInfo.GetAttributeProperty<string>("HttpPostAttribute", "Template") ?? string.Empty;
            if (methodInfo.HasAttribute("HttpPutAttribute"))
                return methodInfo.GetAttributeProperty<string>("HttpPutAttribute", "Template") ?? string.Empty;
            if (methodInfo.HasAttribute("HttpDeleteAttribute"))
                return methodInfo.GetAttributeProperty<string>("HttpDeleteAttribute", "Template") ?? string.Empty;
            return null;
        }

        public Type GetReturnType(MethodInfo methodInfo)
        {
            var returnType = methodInfo.ReturnType;
            if (returnType.GetTypeInfo().IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                returnType = returnType.GetGenericArguments()[0];
            }

            if (returnType.GetTypeInfo().IsGenericType && returnType.GetGenericTypeDefinition().Name.StartsWith("IHttpActionResult"))
            {
                returnType = returnType.GetGenericArguments()[0];
            }

            if (returnType == typeof(void) || returnType.Name == "IHttpActionResult" || returnType.Name == "IActionResult" || returnType == typeof(Task))
                returnType = null;
            return returnType;
        }

        public bool IsParameterFromUri(ParameterInfo parameterInfo)
        {
            return parameterInfo.HasAttribute("FromUriAttribute") || parameterInfo.HasAttribute("FromQueryAttribute");
        }

        public bool IsParameterFromBody(ParameterInfo parameterInfo)
        {
            return parameterInfo.HasAttribute("FromBodyAttribute");
        }

        public bool IsPostAction(MethodInfo methodInfo)
        {
            return methodInfo.HasAttribute("HttpPostAttribute");
        }

        public bool IsDeleteAction(MethodInfo methodInfo)
        {
            return methodInfo.HasAttribute("HttpDeleteAttribute");
        }

        public bool IsPutAction(MethodInfo methodInfo)
        {
            return methodInfo.HasAttribute("HttpPutAttribute");
        }

        public IEnumerable<Type> GetUnionTypes(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetAttributeProperties<Type>("ReturnTypeAttribute", "ReturnType");
        }
    }
}

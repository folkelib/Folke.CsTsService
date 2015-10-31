using System;
using System.Collections.Generic;
using System.Reflection;

namespace Folke.CsTsService
{
    public interface IApiAdapter
    {
        bool IsController(Type type);
        bool IsObservableObject(Type type);
        string GetRoutePrefixName(Type type);
        bool IsAction(MethodInfo methodInfo);
        string GetRouteFormat(MethodInfo methodInfo);
        Type GetReturnType(MethodInfo methodInfo);
        bool IsParameterFromUri(ParameterInfo parameterInfo);
        bool IsParameterFromBody(ParameterInfo parameterInfo);
        bool IsPostAction(MethodInfo methodInfo);
        bool IsDeleteAction(MethodInfo methodInfo);
        bool IsPutAction(MethodInfo methodInfo);
        IEnumerable<Type> GetUnionTypes(PropertyInfo propertyInfo);
    }
}

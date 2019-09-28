using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Folke.CsTsService
{
    public class Documentation
    {
        private readonly IDictionary<string, XElement> memberDocumentationNodes = new Dictionary<string, XElement>();

        public Documentation()
        {
            
        }

        public Documentation(XDocument documentation)
        {
            foreach (var element in documentation.Descendants("member"))
            {
                memberDocumentationNodes[element.Attribute("name").Value] = element;
            }
        }

        public string? GetDocumentation(MethodInfo method)
        {
            return ParseDocumentation(GetMethodDocumentation(method));
        }

        public string? GetDocumentation(Type type)
        {
            return ParseDocumentation(GetTypeDocumentation(type));
        }

        public string? GetDocumentation(PropertyInfo propertyInfo)
        {
            return ParseDocumentation(GetPropertyDocumentation(propertyInfo));
        }

        public string? GetDocumentation(Type type, string enumValueName)
        {
            return ParseDocumentation(GetEnumValueDocumentation(type, enumValueName));
        }

        public XElement? GetMethodDocumentation(MethodInfo method)
        {
            Debug.Assert(method.DeclaringType != null, "method.DeclaringType != null");
            var name = "M:" + method.DeclaringType.FullName + "." + method.Name + "("
                + string.Join(",", method.GetParameters().Select(x => GetTypeName(x.ParameterType)).ToArray()) + ")";
            if (!memberDocumentationNodes.ContainsKey(name))
            {
                return null;
            }
            return memberDocumentationNodes[name];
        }

        private string GetTypeName(Type type)
        {
            if (type.GetTypeInfo().IsGenericType)
            {
                var arguments = string.Join(",", type.GetGenericArguments().Select(GetTypeName));
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                var name = genericTypeDefinition.Name.Split('`')[0];
                return $"{genericTypeDefinition.Namespace}.{name}{{{arguments}}}";
            }

            return type.FullName;
        }

        private XElement? GetTypeDocumentation(Type type)
        {
            var name = "T:" + type.FullName;
            if (!memberDocumentationNodes.ContainsKey(name)) return null;
            return memberDocumentationNodes[name];
        }

        public XElement? GetPropertyDocumentation(PropertyInfo property)
        {
            Debug.Assert(property.DeclaringType != null, "property.DeclaringType != null");
            var name = "P:" + property.DeclaringType.FullName.Replace("+", ".") + "." + property.Name;
            if (!memberDocumentationNodes.ContainsKey(name)) return null;
            return memberDocumentationNodes[name];
        }

        private XElement? GetEnumValueDocumentation(Type type, string enumValueName)
        {
            var name = "F:" + type.FullName.Replace("+", ".") + "." + enumValueName;
            if (!memberDocumentationNodes.ContainsKey(name)) return null;
            return memberDocumentationNodes[name];
        }

        public string? ParseDocumentation(XElement? documentationNode)
        {
            return documentationNode?.Value;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Folke.CsTsService.Nodes;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Folke.Mvc.Extensions;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Folke.CsTsService
{
    public class Converter
    {
        private readonly Documentation documentation;
        private static readonly Regex NameCleaner = new Regex(@"`\d+");

        public Converter(Documentation documentation = null)
        {
            this.documentation = documentation ?? new Documentation();
        }

        public AssemblyNode ReadApplicationPart(ApplicationPartManager applicationPartManager)
        {
            var feature = new ControllerFeature();
            applicationPartManager.PopulateFeature(feature);
            var converter = new Converter();
            var controllerTypes = feature.Controllers.Select(c => c.AsType());
            return ReadControllers(controllerTypes);
        }

        public AssemblyNode ReadControllers(IEnumerable<Type> controllers)
        {
            var assemblyNode = new AssemblyNode();
            foreach (var controllerType in controllers)
            {
                var controller = ReadController(controllerType, assemblyNode);
                if (controller.Actions.Any())
                {
                    assemblyNode.Controllers.Add(controller);
                }
            }
            return assemblyNode;
        }

        public ActionsGroupNode ReadController(Type type, AssemblyNode assemblyNode)
        {
            var controller = new ActionsGroupNode
            {
                Assembly = assemblyNode
            };

            string routePrefix = null;
            var routePrefixAttribute = type.GetTypeInfo().GetCustomAttribute<RouteAttribute>(false) ?? type.GetTypeInfo().GetCustomAttribute<RouteAttribute>();
            if (routePrefixAttribute != null)
            {
                routePrefix = routePrefixAttribute.Template;
            }

            controller.Name = type.Name.Replace("Controller", string.Empty);
            controller.Name = NameCleaner.Replace(controller.Name, string.Empty);
            controller.Documentation = documentation.GetDocumentation(type);

            foreach (var methodInfo in type.GetMethods().Where(x => x.IsPublic))
            {
                if (methodInfo.GetCustomAttribute<NonActionAttribute>() != null) continue;
                if (methodInfo.IsSpecialName) continue;

                var action = ReadAction(routePrefix, methodInfo, controller);
                if (action != null)
                {
                    controller.Actions.Add(action);
                }
            }
            return controller;
        }

        public ActionNode ReadAction(string routePrefix, MethodInfo methodInfo, ActionsGroupNode actionsGroup)
        {
            var actionNode = new ActionNode {Group = actionsGroup};

            string route = null;

            if (methodInfo.GetCustomAttribute<HttpGetAttribute>() != null)
            {
                actionNode.Type = ActionMethod.Get;
                route = methodInfo.GetCustomAttribute<HttpGetAttribute>().Template;
            }
            else if (methodInfo.GetCustomAttribute<HttpPostAttribute>() != null)
            {
                actionNode.Type = ActionMethod.Post;
                route = methodInfo.GetCustomAttribute<HttpPostAttribute>().Template;
            }
            else if (methodInfo.GetCustomAttribute<HttpPutAttribute>() != null)
            {
                actionNode.Type = ActionMethod.Put;
                route = methodInfo.GetCustomAttribute<HttpPutAttribute>().Template;
            }
            else if (methodInfo.GetCustomAttribute<HttpDeleteAttribute>() != null)
            {
                actionNode.Type = ActionMethod.Delete;
                route = methodInfo.GetCustomAttribute<HttpDeleteAttribute>().Template;
            }

            var routeAttribute = methodInfo.GetCustomAttribute<RouteAttribute>();
            if (routeAttribute != null)
            {
                route = routeAttribute.Template;
            }

            if (route == null)
            {
                return null;
            }

            if (actionNode.Type == ActionMethod.Unknown)
            {
                if (methodInfo.Name.StartsWith("Get", StringComparison.OrdinalIgnoreCase)) actionNode.Type = ActionMethod.Get;
                else if (methodInfo.Name.StartsWith("Post", StringComparison.OrdinalIgnoreCase)) actionNode.Type = ActionMethod.Post;
                else if (methodInfo.Name.StartsWith("Put", StringComparison.OrdinalIgnoreCase)) actionNode.Type = ActionMethod.Put;
                else if (methodInfo.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)) actionNode.Type = ActionMethod.Delete;
                else
                    return null;
            }

            // Remove type info from route
            route = Regex.Replace(route, @"{(\w+\??)(?::\w+)?}", "{$1}");
            if (route.StartsWith("~"))
            {
                route = Regex.Replace(route, @"^~/?", string.Empty);
            }
            else
            {
                route = $"{routePrefix}/{route}";
            }

            actionNode.Name = methodInfo.Name;
            actionNode.Route = route;

            var versionMatch = Regex.Match(route, @"api/v([\d\.])+");
            actionNode.Version = versionMatch.Success ? versionMatch.Groups[1].Value : null;

            var authorizeAttribute = methodInfo.GetCustomAttribute<AuthorizeAttribute>();
            if (authorizeAttribute != null)
            {
                actionNode.Authorization = authorizeAttribute.Policy ?? authorizeAttribute.Roles;
            }

            // Read documentation
            var methodNode = documentation.GetMethodDocumentation(methodInfo);

            var summary = methodNode?.Element("summary");
            if (summary != null)
            {
                actionNode.Documentation = summary.Value;
            }

            Dictionary<string, XElement> parameterNodes = null;

            if (methodNode != null)
            {
                parameterNodes = methodNode.Elements("param").ToDictionary(x => x.Attribute("name").Value);
            }

            var routeParameters = new Dictionary<string, bool>();
            var routeParametersMatches = Regex.Matches(route, @"{(\w+)(\?)?(?:\:\w+)?}");
            foreach (Match match in routeParametersMatches)
            {
                var parameter = match.Groups[1].Value;
                var optional = match.Groups[2].Value == "?";
                routeParameters.Add(parameter, optional);
            }

            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                var parameter = ReadParameter(parameterInfo, parameterNodes != null && parameterNodes.ContainsKey(parameterInfo.Name) ? parameterNodes[parameterInfo.Name] : null, actionsGroup.Assembly, routeParameters, actionNode);
                actionNode.Parameters.Add(parameter);
                if (parameter.Position == ParameterPosition.Body)
                {
                    parameter.Type.Class?.SetWritable();
                }
            }

            Type returnType;
            var returnTypeAttribute = methodInfo.GetCustomAttribute<ProducesResponseTypeAttribute>();
            if (returnTypeAttribute != null)
            {
                returnType = returnTypeAttribute.Type;
            }
            else
            {
                returnType = methodInfo.ReturnType;
                if (returnType.GetTypeInfo().IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    returnType = returnType.GenericTypeArguments[0];
                }

                if (returnType.GetTypeInfo().IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IHttpActionResult<>))
                {
                    returnType = returnType.GenericTypeArguments[0];
                }

                if (returnType.GetInterfaces().Contains(typeof(IActionResult)) || returnType == typeof(IActionResult) || returnType == typeof(Task))
                {
                    returnType = null;
                }
            }

            if (returnType != null && returnType != typeof(void))
            {
                var returnDocumentation = methodNode?.Element("returns");
                actionNode.Return = ReadReturn(returnType, actionsGroup.Assembly, returnDocumentation, actionNode);
            }

            return actionNode;
        }

        public ParameterNode ReadParameter(ParameterInfo parameterInfo, XElement documentationNode, AssemblyNode assembly, Dictionary<string, bool> routeParameters, ActionNode actionNode)
        {
            var parameterNode = new ParameterNode { Name = parameterInfo.Name };
            var parameterType = parameterInfo.ParameterType;
            parameterNode.Documentation = documentation.ParseDocumentation(documentationNode);
            parameterNode.IsRequired = RemoveNullable(ref parameterType);
            if (parameterInfo.DefaultValue != null)
                parameterNode.IsRequired = false;
            parameterNode.Type = ReadType(parameterType, assembly, new Type[0], actionNode);

            if (routeParameters.ContainsKey(parameterInfo.Name))
            {
                parameterNode.Position = ParameterPosition.Path;
                parameterNode.IsRequired = !routeParameters[parameterInfo.Name];
            }
            else if (parameterInfo.GetCustomAttribute<FromQueryAttribute>() != null)
            {
                parameterNode.Position = ParameterPosition.Query;
            }
            else if (parameterInfo.GetCustomAttribute<FromBodyAttribute>() != null || parameterNode.Type.Type == TypeIdentifier.Object)
            {
                parameterNode.Position = ParameterPosition.Body;
                parameterNode.IsRequired = true;
            }
            else
            {
                parameterNode.Position = ParameterPosition.Query;
            }


            GetConstraints(parameterInfo.GetCustomAttributes().ToArray(), parameterNode);

            return parameterNode;
        }
        
        public TypeNode ReadType(Type parameterType, AssemblyNode assembly, Type[] parents, ActionNode actionNode)
        {
            var typeNode = new TypeNode();
            if (Nullable.GetUnderlyingType(parameterType) == null && parameterType.GetTypeInfo().IsGenericType)
            {
                if (parameterType.ImplementsInterface(typeof(IDictionary<,>)))
                {
                    typeNode.IsDictionary = true;
                    parameterType = parameterType.GenericTypeArguments[1];
                }
                else if (parameterType.ImplementsInterface(typeof(IEnumerable<>)))
                {
                    typeNode.IsCollection = true;
                    parameterType = parameterType.GenericTypeArguments[0];
                }
            }

            if (parameterType.IsArray)
            {
                typeNode.IsCollection = true;
                parameterType = parameterType.GetElementType();
            }

            if (parameterType.GetTypeInfo().IsGenericType)
            {
                typeNode.GenericParameters = parameterType.GenericTypeArguments.Select(x => ReadType(x, assembly, parents, actionNode)).ToList();
                parameterType = parameterType.GetGenericTypeDefinition();
            }

            if (parameterType.GetTypeInfo().IsEnum)
            {
                typeNode.Type = TypeIdentifier.Enum;
                typeNode.Class = ReadClass(parameterType, assembly, actionNode, parents);
            }
            else if (parameterType == typeof(string))
            {
                typeNode.Type = TypeIdentifier.String;
            }
            else if (parameterType == typeof(long))
            {
                typeNode.Type = TypeIdentifier.Long;
            }
            else if (parameterType == typeof(int) || parameterType == typeof(short))
            {
                typeNode.Type = TypeIdentifier.Int;
            }
            else if (parameterType == typeof(float))
            {
                typeNode.Type = TypeIdentifier.Float;
            }
            else if (parameterType == typeof(double))
            {
                typeNode.Type = TypeIdentifier.Double;
            }
            else if (parameterType == typeof(DateTime))
            {
                typeNode.Type = TypeIdentifier.DateTime;
            }
            else if (parameterType == typeof(bool))
            {
                typeNode.Type = TypeIdentifier.Boolean;
            }
            else if (parameterType == typeof(decimal))
            {
                typeNode.Type = TypeIdentifier.Decimal;
            }
            else if (parameterType == typeof(Guid))
            {
                typeNode.Type = TypeIdentifier.Guid;
            }
            else if (parameterType == typeof(TimeSpan))
            {
                typeNode.Type = TypeIdentifier.TimeSpan;
            }
            else if (parameterType == typeof(object))
            {
                typeNode.Type = TypeIdentifier.Any;
            }
            else if (parameterType.IsGenericParameter)
            {
                typeNode.Type = TypeIdentifier.GenericParameter;
                typeNode.GenericName = parameterType.Name;
            }
            else
            {
                typeNode.Type = TypeIdentifier.Object;
                typeNode.Class = ReadClass(parameterType, assembly, actionNode, parents);
            }
            
            return typeNode;
        }

        private ClassNode ReadClass(Type parameterType, AssemblyNode assembly, ActionNode actionNode, Type[] parents)
        {
            var parameterTypeName = Regex.Replace(parameterType.Name, "`.*", "");
            
            ClassNode classNode;
            if (assembly.Types.ContainsKey(parameterTypeName))
            {
                classNode = assembly.Types[parameterTypeName];
            }
            else
            {
                classNode = new ClassNode
                {
                    Version = actionNode.Version,
                    Documentation = documentation.GetDocumentation(parameterType)
                };
                
                classNode.KoName = parameterTypeName;
                classNode.Name = Regex.Replace(parameterTypeName, @"View(Model)?$", string.Empty);
                if (parameterType.GetTypeInfo().IsEnum)
                {
                    var enumNames = parameterType.GetTypeInfo().GetEnumNames();
                    var enumValues = parameterType.GetTypeInfo().GetEnumValues();

                    classNode.Values = new List<EnumValueNode>();
                    for (var i = 0; i < enumValues.Length; i++)
                    {
                        var enumValue = new EnumValueNode
                        {
                            Name = enumNames[i],
                            Value = Convert.ToInt32(enumValues.GetValue(i)),
                            Documentation = documentation.GetDocumentation(parameterType, enumNames[i])
                        };
                        classNode.Values.Add(enumValue);
                    }
                }
                else
                {
                    if (parents.All(x => x != parameterType))
                    {
                        classNode.Properties = ReadProperties(parameterType, parents, assembly, actionNode);
                    }

                    if (parameterType.GetTypeInfo().IsGenericTypeDefinition)
                    {
                        classNode.GenericParameters = parameterType.GetGenericArguments().Select(x => x.Name).ToList();
                    }

                    var jsonAttribute = parameterType.GetTypeInfo().GetCustomAttribute<JsonAttribute>();
                    if (jsonAttribute != null)
                    {
                        classNode.IsObservable = jsonAttribute.Observable;
                    }
                }
                assembly.Types[classNode.KoName] = classNode;
            }
            return classNode;
        }


        private ReturnNode ReadReturn(Type responseType, AssemblyNode assembly, XElement documentationNode, ActionNode actionNode)
        {
            var returnNode = new ReturnNode
            {
                Documentation = documentation.ParseDocumentation(documentationNode),
                Type = ReadType(responseType, assembly, new Type[0], actionNode)
            };
            return returnNode;
        }

        public List<PropertyNode> ReadProperties(Type type, Type[] parents, AssemblyNode assembly, ActionNode actionNode)
        {
            var newParents = new Type[parents.Length + 1];
            parents.CopyTo(newParents, 0);
            newParents[parents.Length] = type;

            return type.GetProperties()
                .Where(propertyInfo => !IsIgnored(propertyInfo))
                .Select(propertyInfo => ReadProperty(propertyInfo, newParents, assembly, actionNode))
                .ToList();
        }

        private PropertyNode ReadProperty(PropertyInfo propertyInfo, Type[] newParents, AssemblyNode assembly, ActionNode actionNode)
        {
            var propertyType = propertyInfo.PropertyType;
            var propertyNode = new PropertyNode
            {
                Name = StringHelpers.ToCamelCase(propertyInfo.Name),
                IsRequired = RemoveNullable(ref propertyType),
                Documentation = documentation.GetDocumentation(propertyInfo)
            };

            var returnTypeAttributes = propertyInfo.GetCustomAttributes<ReturnTypeAttribute>().ToArray();
            if (returnTypeAttributes.Any())
            {
                if (returnTypeAttributes.Length == 1)
                {
                    propertyNode.Type = ReadType(returnTypeAttributes[0].ReturnType, assembly, newParents, actionNode);
                }
                else
                {
                    propertyNode.Type = new TypeNode
                    {
                        Type = TypeIdentifier.Union,
                        Union = returnTypeAttributes.Select(x => ReadType(x.ReturnType, assembly, newParents, actionNode)).ToArray()
                    };
                }
            }
            else
            {
                propertyNode.Type = ReadType(propertyType, assembly, newParents, actionNode);
            }

            var readOnly = propertyInfo.GetCustomAttribute<ReadOnlyAttribute>();
            if (readOnly != null && readOnly.IsReadOnly)
            {
                propertyNode.IsReadOnly = true;
            }
            var editable = propertyInfo.GetCustomAttribute<EditableAttribute>();
            if (editable != null && !editable.AllowEdit)
            {
                propertyNode.IsReadOnly = true;
            }

            propertyNode.Type.IsObservable = propertyNode.Name != "id" && !propertyNode.IsReadOnly;

            GetConstraints(propertyInfo.GetCustomAttributes().ToArray(), propertyNode);

            return propertyNode;
        }

        private static bool IsIgnored(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>() != null
                || propertyInfo.GetCustomAttribute<IgnoreDataMemberAttribute>() != null;
        }

        private static bool RemoveNullable(ref Type parameterType)
        {
            var required = true;
            if (Nullable.GetUnderlyingType(parameterType) != null)
            {
                required = false;
                parameterType = Nullable.GetUnderlyingType(parameterType);
            }

            if (!parameterType.GetTypeInfo().IsValueType)
            {
                required = false;
            }
            return required;
        }

        private static void GetConstraints(Attribute[] attributes, IConstraintsNode constraintsNode)
        {
            if (attributes != null)
            {
                if (attributes.Any(x => x is RequiredAttribute))
                {
                    constraintsNode.IsRequired = true;
                }

                var stringLengthAttribute = attributes.OfType<StringLengthAttribute>().FirstOrDefault();
                if (stringLengthAttribute != null)
                {
                    constraintsNode.MinimumLength = stringLengthAttribute.MinimumLength;
                    constraintsNode.MaximumLength = stringLengthAttribute.MaximumLength;
                }

                var minLengthAttribute = attributes.OfType<MaxLengthAttribute>().FirstOrDefault();
                if (minLengthAttribute != null)
                {
                    constraintsNode.MinimumLength = minLengthAttribute.Length;
                }

                var maxLengthAttribute = attributes.OfType<MaxLengthAttribute>().FirstOrDefault();
                if (maxLengthAttribute != null)
                {
                    constraintsNode.MaximumLength = maxLengthAttribute.Length;
                }

                var compareAttribute = attributes.OfType<CompareAttribute>().FirstOrDefault();
                if (compareAttribute != null)
                {
                    constraintsNode.CompareTo = StringHelpers.ToCamelCase(compareAttribute.OtherProperty);
                }

                var rangeAttribute = attributes.OfType<RangeAttribute>().FirstOrDefault();
                if (rangeAttribute != null)
                {
                    constraintsNode.Minimum = rangeAttribute.Minimum;
                    constraintsNode.Maximum = rangeAttribute.Maximum;
                }

                if (attributes.Any(x => x is EmailAddressAttribute))
                {
                    constraintsNode.Format = Format.Email;
                }
            }
        }
    }
}

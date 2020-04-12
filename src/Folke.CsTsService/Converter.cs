using System;
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
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Namotion.Reflection;
using Folke.CsTsService.Optional;

namespace Folke.CsTsService
{
    public class Converter
    {
        private readonly Documentation documentation;
        private static readonly Regex NameCleaner = new Regex(@"`\d+");

        public Converter(Documentation? documentation = null)
        {
            this.documentation = documentation ?? new Documentation();
        }

        public AssemblyNode ReadApplicationPart(ApplicationPartManager applicationPartManager)
        {
            var feature = new ControllerFeature();
            applicationPartManager.PopulateFeature(feature);
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
            var name = type.Name.Replace("Controller", string.Empty);
            name = NameCleaner.Replace(name, string.Empty);
            var controller = new ActionsGroupNode(assemblyNode, name);
            
            var routePrefixAttribute = type.GetTypeInfo().GetCustomAttribute<RouteAttribute>(false) ?? type.GetTypeInfo().GetCustomAttribute<RouteAttribute>();
            string routePrefix = routePrefixAttribute != null ? routePrefixAttribute.Template : "todo";
            
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

        public ActionNode? ReadAction(string routePrefix, MethodInfo methodInfo, ActionsGroupNode actionsGroup)
        {
            
            string? route = null;
            ActionMethod actionMethod = ActionMethod.Unknown;

            if (methodInfo.GetCustomAttribute<HttpGetAttribute>() != null)
            {
                actionMethod = ActionMethod.Get;
                route = methodInfo.GetCustomAttribute<HttpGetAttribute>().Template;
            }
            else if (methodInfo.GetCustomAttribute<HttpPostAttribute>() != null)
            {
                actionMethod = ActionMethod.Post;
                route = methodInfo.GetCustomAttribute<HttpPostAttribute>().Template;
            }
            else if (methodInfo.GetCustomAttribute<HttpPutAttribute>() != null)
            {
                actionMethod = ActionMethod.Put;
                route = methodInfo.GetCustomAttribute<HttpPutAttribute>().Template;
            }
            else if (methodInfo.GetCustomAttribute<HttpDeleteAttribute>() != null)
            {
                actionMethod = ActionMethod.Delete;
                route = methodInfo.GetCustomAttribute<HttpDeleteAttribute>().Template;
            }
            else if (methodInfo.GetCustomAttribute<HttpPatchAttribute>() != null)
            {
                actionMethod = ActionMethod.Patch;
                route = methodInfo.GetCustomAttribute<HttpPatchAttribute>().Template;
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

            if (actionMethod == ActionMethod.Unknown)
            {
                if (methodInfo.Name.StartsWith("Get", StringComparison.OrdinalIgnoreCase)) actionMethod = ActionMethod.Get;
                else if (methodInfo.Name.StartsWith("Post", StringComparison.OrdinalIgnoreCase)) actionMethod = ActionMethod.Post;
                else if (methodInfo.Name.StartsWith("Put", StringComparison.OrdinalIgnoreCase)) actionMethod = ActionMethod.Put;
                else if (methodInfo.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)) actionMethod = ActionMethod.Delete;
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
            route = route.Replace("[controller]", actionsGroup.Name);

            var actionNode = new ActionNode(actionsGroup, methodInfo.Name, route);
            actionNode.Type = actionMethod;
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

            Dictionary<string, XElement>? parameterNodes = null;

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

            ContextualType returnType = methodInfo.ReturnParameter.ToContextualParameter();
            if (returnType.Type.IsGenericType && returnType.Type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                returnType = returnType.GenericArguments[0];
            }

            if (returnType.Type.IsGenericType && returnType.Type.GetGenericTypeDefinition() == typeof(ActionResult<>))
            {
                returnType = returnType.GenericArguments[0];
            }

            if (!returnType.Type.GetInterfaces().Contains(typeof(IActionResult)) && returnType.Type != typeof(IActionResult) && returnType.Type != typeof(Task)
                && returnType.Type != typeof(void))
            {
                var returnDocumentation = methodNode?.Element("returns");
                actionNode.Return = ReadReturn(returnType, actionsGroup.Assembly, returnDocumentation, actionNode);
            }

            return actionNode;
        }

        public ParameterNode ReadParameter(ParameterInfo parameterInfo, XElement? documentationNode, AssemblyNode assembly, Dictionary<string, bool> routeParameters, ActionNode actionNode)
        {
            var parameterContext = parameterInfo.ToContextualParameter();
            var parameterType = parameterInfo.ParameterType;
            var type = ReadType(parameterInfo.ToContextualParameter(), assembly, new Type[0], actionNode);
            var parameterNode = new ParameterNode(parameterInfo.Name, type);
            parameterNode.Documentation = documentation.ParseDocumentation(documentationNode);
            parameterNode.IsRequired = !type.IsNullable;
            if (parameterInfo.DefaultValue != null)
                parameterNode.IsRequired = false;
            
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
        
        public TypeNode ReadType(ContextualType contextualType, AssemblyNode assembly, Type[] parents, ActionNode actionNode)
        {
            var typeNode = new TypeNode();
            if (contextualType.Nullability == Nullability.Nullable)
            {
                typeNode.IsNullable = true;
            }

            for (;;)
            {
                if (contextualType.GenericArguments.Length > 0)
                {
                    if (contextualType.Type.ImplementsInterface(typeof(IDictionary<,>)))
                    {
                        typeNode.Modifiers.Add(TypeModifier.Dictionary);
                        contextualType = contextualType.GenericArguments[1];
                    }
                    else if (contextualType.Type.ImplementsInterface(typeof(IEnumerable<>)))
                    {
                        typeNode.Modifiers.Add(TypeModifier.Array);
                        contextualType = contextualType.GenericArguments[0];
                    }
                    else if (contextualType.Type.GetGenericTypeDefinition() == typeof(Optional<>))
                    {
                        typeNode.IsOptional = true;
                        contextualType = contextualType.GenericArguments[0];
                    }
                    else
                    {
                        break;
                    }
                }
                else if (contextualType.Type.IsArray)
                {
                    typeNode.Modifiers.Add(TypeModifier.Array);
                    contextualType= contextualType.ElementType;
                }
                else
                {
                    break;
                }
            }

            var parameterType = contextualType.Type;

            if (parameterType.GetTypeInfo().IsGenericType)
            {
                typeNode.GenericParameters = contextualType.GenericArguments.Select(x => ReadType(x, assembly, parents, actionNode)).ToList();
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
            else if (parameterType == typeof(byte))
            {
                typeNode.Type = TypeIdentifier.Byte;
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
            if (assembly.Classes.ContainsKey(parameterTypeName))
            {
                classNode = assembly.Classes[parameterTypeName];
            }
            else
            {
                classNode = new ClassNode(Regex.Replace(parameterTypeName, @"View(Model)?$", string.Empty))
                {
                    Version = actionNode.Version,
                    Documentation = documentation.GetDocumentation(parameterType)
                };

                if (parameterType.GetTypeInfo().IsEnum)
                {
                    var enumNames = parameterType.GetTypeInfo().GetEnumNames();
                    var enumValues = parameterType.GetTypeInfo().GetEnumValues();
                    classNode.Values = new List<EnumValueNode>();

                    for (var i = 0; i < enumValues.Length; i++)
                    {
                        var enumValue = new EnumValueNode(enumNames[i])
                        {
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
                }

                assembly.Classes[classNode.Name] = classNode;
            }
            return classNode;
        }


        private ReturnNode ReadReturn(ContextualType responseType, AssemblyNode assembly, XElement? documentationNode, ActionNode actionNode)
        {
            var returnNode = new ReturnNode(ReadType(responseType, assembly, new Type[0], actionNode))
            {
                Documentation = documentation.ParseDocumentation(documentationNode),
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

            var unionTypeAttributes = propertyInfo.GetCustomAttributes<UnionTypeAttribute>().ToArray();
            TypeNode type;
            if (unionTypeAttributes.Any())
            {
                if (unionTypeAttributes.Length == 1)
                {
                    type = ReadType(unionTypeAttributes[0].Type.ToContextualType(), assembly, newParents, actionNode);
                }
                else
                {
                    type = new TypeNode
                    {
                        Type = TypeIdentifier.Union,
                        Union = unionTypeAttributes.Select(x => ReadType(x.Type.ToContextualType(), assembly, newParents, actionNode)).ToArray()
                    };
                }
            }
            else
            {
                type = ReadType(propertyInfo.ToContextualMember(), assembly, newParents, actionNode);
            }

            var propertyNode = new PropertyNode(StringHelpers.ToCamelCase(propertyInfo.Name), type)
            {
                Documentation = documentation.GetDocumentation(propertyInfo)
            };

            var propertyContext = propertyInfo.ToContextualProperty();
            if (propertyContext.Nullability == Nullability.Nullable)
            {
                propertyNode.Type.IsNullable = true;
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
            var nullable = false;
            if (Nullable.GetUnderlyingType(parameterType) != null)
            {
                nullable = true;
                parameterType = Nullable.GetUnderlyingType(parameterType);
            }

            if (!parameterType.GetTypeInfo().IsValueType)
            {
                nullable = true;
            }
            return nullable;
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

                var minLengthAttribute = attributes.OfType<MinLengthAttribute>().FirstOrDefault();
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

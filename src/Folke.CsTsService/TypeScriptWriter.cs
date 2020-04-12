using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Folke.CsTsService.Nodes;

namespace Folke.CsTsService
{
    public class TypeScriptWriter
    {
        private const string Tab = "    ";
        private readonly string serviceHelpersModule;
        private readonly string validationModule;
        private readonly TypeScriptOptions options;

        public Dictionary<string, string> OutputModules { get; } = new Dictionary<string, string>();

        public TypeScriptWriter(string serviceHelpersModule = "folke-service-helpers", string validationModule = "folke-ko-validation", TypeScriptOptions options = 0)
        {
            this.serviceHelpersModule = serviceHelpersModule;
            this.validationModule = validationModule;
            this.options = options;
        }

        public void WriteToFiles(string basePath)
        {
            Directory.CreateDirectory(basePath);
            foreach (var outputModule in OutputModules)
            {
                File.WriteAllText($"{basePath}/{outputModule.Key}.ts", outputModule.Value);
            }
        }

        public void WriteAssembly(AssemblyNode assemblyNode)
        {
            var controllers = assemblyNode.Controllers.OrderBy(x => x.Name).ToArray();
            foreach (var controllerNode in controllers)
            {
                WriteController(controllerNode);
            }

            WriteViews(assemblyNode);
            WriteEnums(assemblyNode);
            
            var output = new StringBuilder();
            foreach (var controllerNode in controllers)
            {
                output.AppendLine($"export * from \"./{StringHelpers.ToCamelCase(controllerNode.Name)}\";");
            }
            if (assemblyNode.Classes.Any(x => x.Value.Values != null))
            {
                output.AppendLine("export * from \"./enums\";");
            }
            output.AppendLine("export * from \"./views\";");
            
            OutputModules.Add("services", output.ToString());
        }

        private void WriteEnums(AssemblyNode assemblyNode)
        {
            StringBuilder result = new StringBuilder();
            var dependencies = new Dependencies();
            bool any = false;

            foreach (var classNode in assemblyNode.Classes.Where(x => x.Value.Values != null))
            {
                WriteTypeDefinition(classNode.Value, result, false, dependencies);
                any = true;
            }

            if (any) OutputModules.Add("enums", result.ToString());
        }

        private void AppendFormatDocumentation(StringBuilder builder, string? documentation, string? prefix = null)
        {
            if (!string.IsNullOrEmpty(documentation))
            {
                builder.Append("/** ");
                if (prefix != null)
                {
                    builder.Append(prefix);
                    builder.Append(" ");
                }
                builder.Append(documentation);
                builder.AppendLine(" */");
            }
        }

        private void WriteController(ActionsGroupNode controllerNode)
        {
            var dependencies = new Dependencies
            {
                PrefixModules = PrefixModules.All
            };
            
            var controllersOutput = new StringBuilder();

            AppendFormatDocumentation(controllersOutput, controllerNode.Documentation);

            controllersOutput.AppendLine($"export class {controllerNode.Name}Controller {{");
            controllersOutput.AppendLine("\tconstructor(private client: helpers.ApiClient) {}");
            WriteActions(controllerNode, controllersOutput, dependencies);
            controllersOutput.AppendLine("}");

            controllersOutput.AppendLine();
            
            var moduleName = StringHelpers.ToCamelCase(controllerNode.Name);

            var result = new StringBuilder();
            result.AppendLine("/* This is a generated file. Do not modify or all the changes will be lost. */");
            result.AppendLine($"import * as helpers from \"{serviceHelpersModule}\";");
            if (dependencies.Enums)
                result.AppendLine("import * as enums from \"./enums\";");
            if (dependencies.Views)
                result.AppendLine("import * as views from \"./views\";");
            result.AppendLine();
            result.Append(controllersOutput);

            OutputModules.Add(moduleName, result.ToString());
        }

        private void WriteActions(ActionsGroupNode controllerNode, StringBuilder controllersOutput, Dependencies dependencies)
        {
            var actions = controllerNode.Actions.OrderBy(x => x.Name).ThenBy(x => x.Parameters.Count).ToArray();
            foreach (var actionNode in actions)
            {
                var methodName = StringHelpers.ToCamelCase(actionNode.Name);
                var sameName = actions.Where(x => x.Name == actionNode.Name).ToList();
                if (sameName.Count > 1)
                {
                    methodName += sameName.IndexOf(actionNode) + 1;
                }

                WriteAction(methodName, actionNode, controllersOutput, dependencies);
            }
        }

        private void WriteViews(AssemblyNode assemblyNode)
        {
            StringBuilder body = new StringBuilder();
            var dependencies = new Dependencies
            {
                PrefixModules = PrefixModules.Enums
            };
            var any = false;
            foreach (var classNode in assemblyNode.Classes.Where(x => x.Value.Properties != null))
            {
                WriteTypeDefinition(classNode.Value, body, false, dependencies);
                any = true;
            }
            var result = new StringBuilder();
            if (dependencies.Enums)
            {
                result.AppendLine("import * as enums from \"./enums\";");
            }
            result.Append(body);
            if (any) OutputModules.Add("views", result.ToString());
        }

        private class Dependencies
        {
            public bool ValidationModule { get; set; }
            public bool KoViews { get; set; }
            public bool Views { get; set; }
            public PrefixModules PrefixModules { get; set; }
            public bool Enums { get; set; }
            public bool ServiceHelpers { get; set; }
        }

        //private void WriteClassName(ClassNode classNode, bool knockout, StringBuilder result)
        //{
        //    result.Append(classNode.Name);
            
        //    if (classNode.GenericParameters != null)
        //    {
        //        result.Append("<");
        //        result.Append(string.Join(", ", classNode.GenericParameters));
        //        result.Append(">");
        //    }
        //}

        private void WriteTypeDefinition(ClassNode viewNode, StringBuilder result, bool edition, Dependencies dependencies)
        {
            result.AppendLine();
            AppendFormatDocumentation(result, viewNode.Documentation);

            var name = viewNode.Name;
            if (edition)
            {
                name += "Edit";
            }

            if (viewNode.Properties != null)
            {
                result.Append($"export interface {name}");
                if (viewNode.GenericParameters != null)
                {
                    result.Append("<");
                    result.Append(string.Join(", ", viewNode.GenericParameters));
                    result.Append(">");
                }
                result.AppendLine(" {");

                bool first = true;
                foreach (var property in viewNode.Properties)
                {
                    if (!property.IsReadOnly || !edition)
                    {
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            result.AppendLine();
                        }

                        WriteProperty(property, result, edition, dependencies);
                    }
                }
            }
            else if (viewNode.Values != null)
            {
                result.AppendLine($"export enum {name} {{");
                bool first = true;
                foreach (var value in viewNode.Values)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        result.AppendLine(",");
                        result.AppendLine();
                    }
                    if (value.Documentation?.Length > 0)
                    {
                        result.AppendLine($"{Tab}/** {string.Join("\r\n", value.Documentation)} */");
                    }

                    result.Append($"{Tab}{value.Name} = {value.Value}");
                }
                result.AppendLine();
            }
            result.AppendLine("}");
        }

        private void WriteProperty(PropertyNode property, StringBuilder result, bool edition, Dependencies dependencies)
        {
            AppendFormatDocumentation(result, property.Documentation);

            result.Append($"{Tab}{property.Name}");
            if (property.Type.IsOptional) result.Append("?");
            result.Append(": ");
            WriteType(property.Type, result, edition, dependencies);
            result.AppendLine(";");
        }

        [Flags]
        private enum PrefixModules
        {
            None = 0,
            Views = 1,
            Enums = 4,
            All = 7
        }
        
        private void WriteType(TypeNode typeNode, StringBuilder result, bool edition, Dependencies dependencies, bool notACollection = false)
        {
            foreach (var modifier in typeNode.Modifiers)
            {
                if (modifier == TypeModifier.Dictionary)
                {
                    result.Append("{ [key: string]: ");
                }
            }

            WriteTypeWithoutModifiers(typeNode, result, edition, dependencies);

            foreach (var modifier in typeNode.Modifiers)
            {
                if (modifier == TypeModifier.Array && !notACollection)
                {
                    result.Append("[]");
                }

                if (modifier == TypeModifier.Dictionary)
                {
                    result.Append(" }");
                }
            }
        }

        private void WriteTypeWithoutModifiers(TypeNode typeNode, StringBuilder result, bool edition, Dependencies dependencies)
        {
            switch (typeNode.Type)
            {
                case TypeIdentifier.Boolean:
                    result.Append("boolean");
                    break;
                case TypeIdentifier.DateTime:
                    result.Append("string");
                    break;
                case TypeIdentifier.Object:
                    if (typeNode.Class == null) throw new Exception("class should not be null");
                    if (dependencies.PrefixModules.HasFlag(PrefixModules.Views))
                    {
                        dependencies.Views = true;
                        result.Append("views.");
                    }
                    result.Append(typeNode.Class.Name);
                    if (edition && typeNode.Class.HasReadOnly())
                    {
                        result.Append("Edit");
                    }
                    break;
                case TypeIdentifier.Decimal:
                case TypeIdentifier.Double:
                case TypeIdentifier.Float:
                case TypeIdentifier.Int:
                case TypeIdentifier.Long:
                case TypeIdentifier.Byte:
                    result.Append("number");
                    break;
                case TypeIdentifier.Guid:
                case TypeIdentifier.String:
                case TypeIdentifier.TimeSpan:
                    result.Append("string");
                    break;
                case TypeIdentifier.Enum:
                    if (typeNode.Class == null) throw new Exception("class should not be null");
                    if (dependencies.PrefixModules.HasFlag(PrefixModules.Enums))
                    {
                        result.Append("enums.");
                        dependencies.Enums = true;
                    }
                    result.Append(typeNode.Class.Name);
                    break;
                case TypeIdentifier.Any:
                    result.Append("any");
                    break;
                case TypeIdentifier.Union:
                {
                    bool first = true;
                        if (typeNode.Union == null) throw new Exception("class should not be null");
                        foreach (var type in typeNode.Union)
                    {
                        if (first)
                            first = false;
                        else
                            result.Append(" | ");
                        WriteType(type, result, edition, dependencies);
                    }
                    break;
                }
                case TypeIdentifier.GenericParameter:
                    result.Append(typeNode.GenericName);
                    break;
            }


            if (typeNode.GenericParameters != null)
            {
                result.Append("<");
                var first = true;
                foreach (var genericType in typeNode.GenericParameters)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        result.Append(", ");
                    }

                    WriteType(genericType, result, edition, dependencies, false);
                }
                result.Append(">");
            }

            if (typeNode.IsNullable) result.Append(" | null");
            if (typeNode.IsOptional) result.Append(" | undefined");
        }

        private void WriteAction(string methodName, ActionNode actionNode, StringBuilder controllersOutput, Dependencies dependencies)
        {
            controllersOutput.AppendLine();

            if (actionNode.Documentation != null && actionNode.Documentation.Any()
                || actionNode.Parameters.Any(x => x.Documentation != null && x.Documentation.Any())
                || (actionNode.Return?.Documentation != null && actionNode.Return.Documentation.Any()))
            {
                controllersOutput.AppendLine($"{Tab}/**");

                AppendFormatDocumentation(controllersOutput, actionNode.Documentation);

                foreach (var parameter in actionNode.Parameters)
                {
                    AppendFormatDocumentation(controllersOutput, parameter.Documentation, $"@param {parameter.Name}");
                }

                if (actionNode.Return != null)
                {
                    AppendFormatDocumentation(controllersOutput, actionNode.Return.Documentation, "@return");
                }
                controllersOutput.AppendLine($"{Tab} */");
            }

            controllersOutput.Append($"{Tab}{methodName}(");
            if (options.HasFlag(TypeScriptOptions.ParametersInObject))
                controllersOutput.Append("params: {");
            
            foreach (var parameter in actionNode.Parameters)
            {
                if (parameter != actionNode.Parameters.First())
                {
                    controllersOutput.Append(", ");
                }

                controllersOutput.Append(parameter.Name);
                if (!parameter.IsRequired)
                    controllersOutput.Append("?");
                controllersOutput.Append(": ");
                WriteType(parameter.Type, controllersOutput, true, dependencies);
            }

            if (options.HasFlag(TypeScriptOptions.ParametersInObject))
                controllersOutput.Append("}");

            //if ((actionNode.Type == ActionMethod.Post || actionNode.Type == ActionMethod.Put)
            //    && actionNode.Parameters.All(x => x.Position != ParameterPosition.Body && x.Position != ParameterPosition.FormData))
            //{
            //    if (actionNode.Parameters.Count > 0)
            //    {
            //        controllersOutput.Append(", ");
            //    }

            //    controllersOutput.Append("data: any");
            //}

            controllersOutput.AppendLine(") {");
            controllersOutput.Append($"{Tab}{Tab}return this.client.fetch");

            if (actionNode.Return != null)
            {
                controllersOutput.Append("Json");
                //if ((actionNode.Return.Type.Type == TypeIdentifier.Object && actionNode.Return.Type.Class.HasObservable&& knockout) || actionNode.Return.Type.Type == TypeIdentifier.DateTime)
                //{
                //    controllersOutput.Append("T");
                //}

                controllersOutput.Append("<");

                if (actionNode.Return.Type.Type == TypeIdentifier.DateTime)
                {
                    controllersOutput.Append("string");
                }
                else
                {
                    WriteType(actionNode.Return.Type, controllersOutput, false, dependencies);
                }

                controllersOutput.Append(">");
            }

            if (actionNode.Parameters.Any(x => x.Position == ParameterPosition.Path))
            {
                var route = actionNode.Route.Replace("{", options.HasFlag(TypeScriptOptions.ParametersInObject) ? "${params." : "${").Replace("?","");

                controllersOutput.Append($"(`{route}`");
            }
            else
            {
                controllersOutput.Append($"(\"{actionNode.Route}\"");
            }


            if (actionNode.Parameters.Any(x => x.Position == ParameterPosition.Query))
            {
                controllersOutput.Append(" + helpers.getQueryString({ ");
                bool first = true;
                foreach (var parameter in actionNode.Parameters.Where(x => x.Position == ParameterPosition.Query))
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        controllersOutput.Append(", ");
                    }

                    if (options.HasFlag(TypeScriptOptions.ParametersInObject))
                    {
                        controllersOutput.Append($"{parameter.Name}: params.{parameter.Name}");
                    }
                    else
                    {
                        controllersOutput.Append($"{parameter.Name}: {parameter.Name}");
                    }
                }
                controllersOutput.Append(" })");
            }

            controllersOutput.Append(", \"");
            switch (actionNode.Type)
            {
                case ActionMethod.Delete:
                    controllersOutput.Append("DELETE");
                    break;
                case ActionMethod.Patch:
                    controllersOutput.Append("PATCH");
                    break;
                case ActionMethod.Get:
                    controllersOutput.Append("GET");
                    break;
                case ActionMethod.Post:
                    controllersOutput.Append("POST");
                    break;
                case ActionMethod.Put:
                    controllersOutput.Append("PUT");
                    break;
                default:
                    throw new Exception($"Unkown {actionNode.Type} action type");
            }
            controllersOutput.Append("\"");
            
            var body = actionNode.Parameters.FirstOrDefault(x => x.Position == ParameterPosition.Body);
            if (body != null)
            {
                controllersOutput.Append(", JSON.stringify(");
                if (options.HasFlag(TypeScriptOptions.ParametersInObject))
                    controllersOutput.Append("params.");
                controllersOutput.Append(body.Name);
                controllersOutput.Append(")");
            }
            else
            {
                controllersOutput.Append(", undefined");
            }

            controllersOutput.Append(")");

            controllersOutput.AppendLine(";");
            controllersOutput.AppendLine($"{Tab}}}");
        }
    }
}

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

        public Dictionary<string, string> OutputModules { get; } = new Dictionary<string, string>();

        public TypeScriptWriter(string serviceHelpersModule = "folke-ko-service-helpers", string validationModule = "folke-ko-validation")
        {
            this.serviceHelpersModule = serviceHelpersModule;
            this.validationModule = validationModule;
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
            if (assemblyNode.Types.Values.Any(x => x.IsObservable))
                WriteKoViews(assemblyNode);

            var output = new StringBuilder();
            foreach (var controllerNode in controllers)
            {
                output.AppendLine($"import * as {StringHelpers.ToCamelCase(controllerNode.Name)}Group from './{StringHelpers.ToCamelCase(controllerNode.Name)}';");
            }
            output.AppendLine("import * as views from './views';");
            output.AppendLine("import * as koViews from './ko-views';");
            output.AppendLine($"import {{ loading }} from '{serviceHelpersModule}';");
            output.AppendLine("export * from './views';");
            output.AppendLine("export * from './ko-views';");
            output.AppendLine($"export {{ loading }} from '{serviceHelpersModule}';");
            output.AppendLine();

            foreach (var controllerNode in controllers)
            {
                var moduleName = StringHelpers.ToCamelCase(controllerNode.Name);
                output.AppendLine($"export const {moduleName} = new {moduleName}Group.{controllerNode.Name}Controller();");
            }

            output.AppendLine("export const services = {");
            
            foreach (var controllerNode in controllers)
            {
                var moduleName = StringHelpers.ToCamelCase(controllerNode.Name);
                output.AppendLine($"{Tab}{moduleName}: {moduleName},");
            }

            output.AppendLine($"{Tab}factories: {{");
            foreach (var type in assemblyNode.Types.Values.Where(x => x.IsObservable))
            {
                output.AppendLine($"{Tab}{Tab}create{StringHelpers.ToPascalCase(type.Name)}: (data: views.{type.CleanName}) => new koViews.{type.Name}(data),");
            }
            output.AppendLine($"{Tab}}},");
            output.AppendLine($"{Tab}loading: loading");
            output.AppendLine("}");
            output.AppendLine();
         
            OutputModules.Add("services", output.ToString());
        }
        
        private void AppendFormatDocumentation(StringBuilder builder, string documentation, string prefix = null)
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
            var result = new StringBuilder();
            result.AppendLine("/* This is a generated file. Do not modify or all the changes will be lost. */");
            result.AppendLine($"import * as helpers from '{serviceHelpersModule}';");
            result.AppendLine("import * as views from './views';");
            result.AppendLine("import * as koViews from './ko-views';");

            var controllersOutput = new StringBuilder();

            AppendFormatDocumentation(controllersOutput, controllerNode.Documentation);

            controllersOutput.AppendLine($"export class {controllerNode.Name}Controller {{");
            WriteActions(controllerNode, controllersOutput);
            controllersOutput.AppendLine("}");

            controllersOutput.AppendLine();
            
            var moduleName = StringHelpers.ToCamelCase(controllerNode.Name);
           
            result.AppendLine();
            result.Append(controllersOutput);

            OutputModules.Add(moduleName, result.ToString());
        }

        private void WriteActions(ActionsGroupNode controllerNode, StringBuilder controllersOutput)
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

                WriteAction(methodName, actionNode, controllersOutput, true);
                if (actionNode.Return != null && actionNode.Return.Type.IsObservable)
                    WriteAction(methodName + "T", actionNode, controllersOutput, false);
            }
        }

        private void WriteViews(AssemblyNode assemblyNode)
        {
            StringBuilder result = new StringBuilder();

            foreach (var classNode in assemblyNode.Types)
            {
                WriteTypeDefinition(classNode.Value, result, false);
            }

            OutputModules.Add("views", result.ToString());
        }

        private void WriteKoViews(AssemblyNode assemblyNode)
        {
            var result = new StringBuilder();
            result.AppendLine("import * as ko from 'knockout';");
            result.AppendLine("import * as views from './views';");
            result.AppendLine($"import * as validation from '{validationModule}';");
            result.AppendLine($"import {{ loading }} from '{serviceHelpersModule}';");
            result.AppendLine();
            result.AppendLine("function toDate(date:string) {");
            result.AppendLine($"{Tab}return date != undefined ? new Date(date) : undefined;");
            result.AppendLine("}");
            result.AppendLine("function fromDate(date:Date) {");
            result.AppendLine($"{Tab}return date != undefined ? date.toISOString() : undefined;");
            result.AppendLine("}");
            result.AppendLine("function arrayChanged<T>(array: ko.ObservableArray<T>, original: T[]) {");
            result.AppendLine($"{Tab}return array() && (!original || array().length !== original.length);");
            result.AppendLine("}");
            result.AppendLine("function dateArrayChanged(array: ko.ObservableArray<Date>, original: string[]) {");
            result.AppendLine($"{Tab}return array() && (!original || array().length !== original.length);");
            result.AppendLine("}");

            foreach (var classNode in assemblyNode.Types.Where(x => x.Value.IsObservable))
            {
                WriteKoTypeDefinition(classNode.Value, result);
            }
            OutputModules.Add("ko-views", result.ToString());
        }

        private void WriteKoTypeDefinition(TypeNode viewNode, StringBuilder result)
        {
            result.AppendLine();
            AppendFormatDocumentation(result, viewNode.Documentation);

            if (viewNode.Type != TypeIdentifier.Object)
                return;

            result.AppendLine($"export class {viewNode.Name} {{");
            var cleanName = viewNode.CleanName;
            result.AppendLine($"{Tab}originalData: views.{cleanName};");
            result.AppendLine($"{Tab}changed: ko.Computed<boolean>;");

            bool first = true;
            foreach (var property in viewNode.Properties)
            {
                
                if (first)
                {
                    first = false;
                }
                else
                {
                    result.AppendLine();
                }

                if (property.IsObservable)
                {
                    WriteKoProperty(property, result);
                }
                else
                {
                    WriteProperty(property, result, false);
                }
            }
            result.AppendLine();

            // constructor
            result.AppendLine($"{Tab}constructor(data: views.{cleanName}) {{");
            result.AppendLine($"{Tab}{Tab}this.load(data);");
            result.AppendLine($"{Tab}{Tab}this.originalData = data;");

            // changed
            result.AppendLine($"{Tab}{Tab}this.changed = ko.computed(() => ");

            first = true;
            foreach (var propertyNode in viewNode.Properties.Where(x => x.IsObservable))
            {
                if (first)
                {
                    first = false;
                    result.Append($"{Tab}{Tab}{Tab}");
                }
                else
                {
                    result.Append($"{Tab}{Tab}{Tab}|| ");
                }

                if (propertyNode.Type.Type == TypeIdentifier.DateTime && !propertyNode.IsCollection)
                    result.Append("fromDate(");
                
                if (propertyNode.IsCollection)
                {
                    if (propertyNode.Type.IsObservable)
                    {
                        result.Append($"this.{propertyNode.Name}()");

                        result.Append(" != undefined && (");
                        result.Append($"this.{propertyNode.Name}().length !== this.originalData.{propertyNode.Name}.length");
                        result.Append($" || this.{propertyNode.Name}().some(x => x.changed())");
                        result.AppendLine(")");
                    }
                    else
                    {
                        if (propertyNode.Type.Type== TypeIdentifier.DateTime)
                            result.AppendLine($"dateArrayChanged(this.{propertyNode.Name}, this.originalData.{propertyNode.Name})");
                        else
                            result.AppendLine($"arrayChanged(this.{propertyNode.Name}, this.originalData.{propertyNode.Name})");
                    }
                }
                else
                {
                    result.Append($"this.{propertyNode.Name}()");

                    if (propertyNode.Type.IsObservable)
                    {
                        result.AppendLine($" != undefined && this.{propertyNode.Name}().changed()");
                    }
                    else
                    {
                        if (propertyNode.Type.Type == TypeIdentifier.DateTime)
                            result.Append(")");
                        result.AppendLine($" !== this.originalData.{propertyNode.Name}");
                    }
                }
            }
            result.AppendLine($"{Tab}{Tab});");
            result.AppendLine($"{Tab}}}");

            // toJs() method
            result.AppendLine($"{Tab}public toJs() {{");
            result.AppendLine($"{Tab}{Tab}return {{");
            foreach (var property in viewNode.Properties)
            {
                result.Append($"{Tab}{Tab}{Tab}{property.Name}: ");
                if (property.Type.Type == TypeIdentifier.DateTime && !property.IsCollection)
                    result.Append("fromDate(");
                result.Append($"this.{property.Name}");
                if (property.IsObservable)
                {
                    result.Append("()");
                }

                if (property.Type.IsObservable)
                {
                    result.Append($" ? this.{property.Name}");
                    if (property.IsObservable)
                    {
                        result.Append("()");
                    }
                    if (property.IsCollection)
                    {
                        result.Append(".map(x => x.toJs())");
                    }
                    else
                    {
                        result.Append(".toJs()");
                    }
                    result.Append(": null");
                }
                else
                {
                    if (property.Type.Type == TypeIdentifier.DateTime)
                    {
                        if (property.IsCollection)
                        {
                            result.Append($" && this.{property.Name}");
                            if (property.IsObservable) result.Append("()");
                            result.Append(".map(x => x.toISOString())");
                        }
                        else
                            result.Append(")");
                    }
                }
                result.AppendLine(",");
            }
            result.AppendLine($"{Tab}{Tab}}}");
            result.AppendLine($"{Tab}}}");

            // load() method
            result.AppendLine($"{Tab}public load(data: views.{cleanName}) {{");
            foreach (var propertyNode in viewNode.Properties)
            {
                result.Append($"{Tab}{Tab}this.{propertyNode.Name}");
                if (propertyNode.IsObservable)
                {
                    if (propertyNode.Type.IsObservable)
                    {
                        if (propertyNode.IsCollection)
                        {
                            result.Append($"(data.{propertyNode.Name} ? data.{propertyNode.Name}.map(x => new {propertyNode.Type.Name}(x)) : null)");
                        }
                        else
                        {
                            result.Append(
                                $"(data.{propertyNode.Name} ? new {propertyNode.Type.Name}(data.{propertyNode.Name}) : null)");
                        }
                    }
                    else
                    {
                        if (propertyNode.Type.Type == TypeIdentifier.DateTime)
                        {
                            if (propertyNode.IsCollection)
                                result.Append($"(data.{propertyNode.Name} && data.{propertyNode.Name}.map(x => new Date(x)))");
                            else
                                result.Append($"(toDate(data.{propertyNode.Name}))");
                        }
                        else
                            result.Append($"(data.{propertyNode.Name})");
                    }
                }
                else
                {
                    if (propertyNode.Type.Type == TypeIdentifier.DateTime)
                        result.Append($" = toDate(data.{propertyNode.Name})");
                    else
                        result.Append($" = data.{propertyNode.Name}");
                }
                result.AppendLine(";");
            }
            result.AppendLine($"{Tab}}}");

            // isValid() property
            if (viewNode.Properties.Any(x => x.NeedValidation()))
            {
                result.AppendLine();
                result.Append($"{Tab}public isValid = ko.computed(() => !loading()");

                foreach (var source in viewNode.Properties.Where(x => x.NeedValidation()))
                {
                    result.Append($" && !this.{source.Name}.validating() && !this.{source.Name}.errorMessage()");
                }
                result.AppendLine(");");
            }
            result.AppendLine("}");
        }

        private void WriteKoProperty(PropertyNode property, StringBuilder result)
        {
            AppendFormatDocumentation(result, property.Documentation);

            result.Append($"{Tab}{property.Name}");

            if (property.IsCollection)
            {
                result.Append(" = ko.observableArray<");
                WriteType(property.Type, result, false, PrefixModules.Views, allowObservable: true);
            }
            else
            {
                if (property.NeedValidation())
                {
                    result.Append(" = validation.validableObservable<");
                }
                else
                {
                    result.Append(" = ko.observable<");
                }
                WriteTypedNode(property, result, false, prefixModule: PrefixModules.Views, allowObservable: true);
            }
            result.Append(">()");

            if (property.NeedValidation())
            {
                if (property.IsRequired)
                {
                    result.Append(".addValidator(validation.isRequired)");
                }

                if (property.CompareTo != null)
                {
                    result.Append($".addValidator(validation.areSame(this.{property.CompareTo}))");
                }

                if (property.MinimumLength.HasValue)
                {
                    result.Append($".addValidator(validation.hasMinLength({property.MinimumLength.Value}))");
                }

                if (property.MaximumLength.HasValue)
                {
                    result.Append($".addValidator(validation.hasMinLength({property.MaximumLength.Value}))");
                }

                if (property.Format == Format.Email)
                {
                    result.Append(".addValidator(validation.isEmail)");
                }

                if (property.Maximum != null & property.Maximum != null)
                {
                    result.Append($".addValidator(validation.isInRange({property.Minimum}, {property.Maximum}))");
                }
                else if (property.Maximum != null)
                {
                    result.Append($".addValidator(validation.isAtMost({property.Maximum}))");
                }
                else if (property.Minimum != null)
                {
                    result.Append($".addValidator(validation.isAtLeast({property.Minimum}))");
                }
            }

            result.AppendLine(";");
        }

        private void WriteTypeDefinition(TypeNode viewNode, StringBuilder result, bool edition)
        {
            result.AppendLine();
            AppendFormatDocumentation(result, viewNode.Documentation);

            var cleanName = viewNode.CleanName;
            if (edition)
            {
                cleanName += "Edit";
            }

            if (viewNode.Type == TypeIdentifier.Object)
            {
                result.AppendLine($"export interface {cleanName} {{");

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

                        WriteProperty(property, result, edition);
                    }
                }
            }
            else
            {
                result.AppendLine($"export const enum {cleanName} {{");
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

        private void WriteProperty(PropertyNode property, StringBuilder result, bool edition)
        {
            AppendFormatDocumentation(result, property.Documentation);

            result.Append($"{Tab}{property.Name}");
            if (!property.IsRequired)
            {
                result.Append("?");
            }
            result.Append(": ");
            WriteTypedNode(property, result, edition, prefixModule: PrefixModules.None, allowObservable: false);
            result.AppendLine(";");
        }

        [Flags]
        private enum PrefixModules
        {
            None = 0,
            Views = 1,
            KoViews = 2,
            All = 3
        }

        private void WriteTypedNode(ITypedNode typedNode, StringBuilder result, bool edition, PrefixModules prefixModule,
            bool allowObservable)
        {
            if (typedNode.IsDictionary)
            {
                result.Append("{ [key: string]: ");
            }

            WriteType(typedNode.Type, result, edition, prefixModule, allowObservable);

            if (typedNode.IsCollection)
            {
                result.Append("[]");
            }

            if (typedNode.IsDictionary)
            {
                result.Append(" }");
            }
        }

        private void WriteType(TypeNode classNode, StringBuilder result, bool edition, PrefixModules prefixModule, bool allowObservable)
        {
            switch (classNode.Type)
            {
                case TypeIdentifier.Boolean:
                    result.Append("boolean");
                    break;
                case TypeIdentifier.DateTime:
                    if (allowObservable)
                        result.Append("Date");
                    else
                        result.Append("string");
                    break;
                case TypeIdentifier.Object:
                    if (allowObservable && classNode.IsObservable)
                    {
                        if (prefixModule.HasFlag(PrefixModules.KoViews))
                            result.Append("koViews.");
                        result.Append(classNode.Name);
                    }
                    else
                    {
                        if (prefixModule.HasFlag(PrefixModules.Views))
                            result.Append("views.");
                        result.Append(classNode.CleanName);
                    }
                    if (edition && classNode.HasReadOnly())
                    {
                        result.Append("Edit");
                    }
                    break;
                case TypeIdentifier.Decimal:
                case TypeIdentifier.Double:
                case TypeIdentifier.Float:
                case TypeIdentifier.Int:
                case TypeIdentifier.Long:
                    result.Append("number");
                    break;
                case TypeIdentifier.Guid:
                case TypeIdentifier.String:
                case TypeIdentifier.TimeSpan:
                    result.Append("string");
                    break;
                case TypeIdentifier.Enum:
                    if (prefixModule.HasFlag(PrefixModules.Views))
                        result.Append("views.");
                    result.Append(classNode.CleanName);
                    break;
                case TypeIdentifier.Any:
                    result.Append("any");
                    break;
                case TypeIdentifier.Union:
                    {
                        bool first = true;
                        foreach (var type in classNode.Union)
                        {
                            if (first)
                                first = false;
                            else
                                result.Append(" | ");
                            WriteType(type, result, edition, prefixModule, allowObservable);
                        }
                        break;
                    }
            }
        }

        private void WriteAction(string methodName, ActionNode actionNode, StringBuilder controllersOutput, bool knockout)
        {
            controllersOutput.AppendLine();

            if (actionNode.Documentation != null && actionNode.Documentation.Any()
                || actionNode.Parameters.Any(x => x.Documentation != null && x.Documentation.Any())
                || (actionNode.Return != null && actionNode.Return.Documentation != null && actionNode.Return.Documentation.Any()))
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

            controllersOutput.Append($"{Tab}{methodName}(params:{{");
            
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
                WriteTypedNode(parameter, controllersOutput, true, prefixModule: PrefixModules.All, allowObservable: true);
            }

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
            controllersOutput.Append($"{Tab}{Tab}return helpers.fetch");

            if (actionNode.Return == null)
            {
                controllersOutput.Append("Void");
            }
            else
            {
                controllersOutput.Append(actionNode.Return.IsCollection ? "List" : "Single");
                if ((actionNode.Return.Type.IsObservable&& knockout) || actionNode.Return.Type.Type == TypeIdentifier.DateTime)
                {
                    controllersOutput.Append("T");
                }

                controllersOutput.Append("<");

                if (actionNode.Return.Type.Type == TypeIdentifier.DateTime)
                {
                    controllersOutput.Append("string, Date");
                }
                else
                {
                    if (actionNode.Return.IsCollection)
                    {
                        WriteType(actionNode.Return.Type, controllersOutput, false, PrefixModules.All, allowObservable: false);
                    }
                    else
                    {
                        WriteTypedNode(actionNode.Return, controllersOutput, false, PrefixModules.All, allowObservable: false);
                    }

                    if (actionNode.Return.Type.IsObservable&& knockout)
                    {
                        controllersOutput.Append(", koViews.");
                        controllersOutput.Append(actionNode.Return.Type.Name);
                    }
                }

                controllersOutput.Append(">");
            }

            if (actionNode.Parameters.Any(x => x.Position == ParameterPosition.Path))
            {
                var route = actionNode.Route.Replace("{", "${params.").Replace("?","");

                controllersOutput.Append($"(`{route}`");
            }
            else
            {
                controllersOutput.Append($"('{actionNode.Route}'");
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
                    controllersOutput.Append($"{parameter.Name}: params.{parameter.Name}");
                    if (parameter.Type.Type == TypeIdentifier.DateTime)
                    {
                        controllersOutput.Append($" && params.{parameter.Name}.toISOString()");
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
                case ActionMethod.Get:
                    controllersOutput.Append("GET");
                    break;
                case ActionMethod.Post:
                    controllersOutput.Append("POST");
                    break;
                case ActionMethod.Put:
                    controllersOutput.Append("PUT");
                    break;
            }
            controllersOutput.Append("\"");

            if (actionNode.Return != null)
            {
                if (actionNode.Return.Type.Type == TypeIdentifier.DateTime)
                {
                    controllersOutput.Append(", view => new Date(view)");
                }
                else if (actionNode.Return.Type.IsObservable&& knockout)
                {
                    controllersOutput.Append(", view => new koViews.");
                    controllersOutput.Append(actionNode.Return.Type.Name);
                    controllersOutput.Append("(view)");
                }
            }

            var body = actionNode.Parameters.FirstOrDefault(x => x.Position == ParameterPosition.Body);
            if (body != null)
            {
                controllersOutput.Append(", JSON.stringify(params.");
                controllersOutput.Append(body.Name);
                if (body.Type.IsObservable)
                {
                    controllersOutput.Append(".toJs()");
                }
                controllersOutput.Append(")");
            }
            else
            {
                controllersOutput.Append(", null");
            }

            controllersOutput.AppendLine(");");
            controllersOutput.AppendLine($"{Tab}}}");
        }
    }
}

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
            Directory.CreateDirectory($"{basePath}/ko");
            foreach (var outputModule in OutputModules)
            {
                File.WriteAllText($"{basePath}/{outputModule.Key}.ts", outputModule.Value);
            }
        }

        public void WriteAssembly(AssemblyNode assemblyNode, bool knockout)
        {
            var controllers = assemblyNode.Controllers.OrderBy(x => x.Name).ToArray();
            foreach (var controllerNode in controllers)
            {
                WriteController(controllerNode, knockout);
            }

            if (knockout)
                WriteKoViews(assemblyNode);
            else
                WriteViews(assemblyNode);
            
            var output = new StringBuilder();
            foreach (var controllerNode in controllers)
            {
                output.AppendLine($"import * as {StringHelpers.ToCamelCase(controllerNode.Name)}Group from \"./{StringHelpers.ToCamelCase(controllerNode.Name)}\";");
            }
            if (knockout)
            {
                output.AppendLine("import * as views from \"../views\";");
                output.AppendLine("import * as koViews from \"./views\";");
            }
            else
            {
                output.AppendLine("import * as views from \"./views\";");
            }

            output.AppendLine($"import {{ loading }} from \"{serviceHelpersModule}\";");
            output.AppendLine("export * from \"./views\";");
            output.AppendLine($"export {{ loading }} from \"{serviceHelpersModule}\";");
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

            //if (knockout)
            //{
            //    output.AppendLine($"{Tab}factories: {{");
            //    foreach (var classNode in assemblyNode.Types.Values.Where(x => x.IsObservable))
            //    {
            //        output.AppendLine($"{Tab}{Tab}create{StringHelpers.ToPascalCase(classNode.KoName)}: (data: views.{classNode.Name}) => new koViews.{classNode.KoName}(data),");
            //    }
            // output.AppendLine($"{Tab}}},");
            //}

            output.AppendLine($"{Tab}loading: loading");
            output.AppendLine("};");
            output.AppendLine();
         
            OutputModules.Add(knockout ? "ko/services" : "services", output.ToString());
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

        private void WriteController(ActionsGroupNode controllerNode, bool knockout)
        {
            var dependencies = new Dependencies
            {
                PrefixModules = PrefixModules.All
            };
            
            var controllersOutput = new StringBuilder();

            AppendFormatDocumentation(controllersOutput, controllerNode.Documentation);

            controllersOutput.AppendLine($"export class {controllerNode.Name}Controller {{");
            WriteActions(controllerNode, controllersOutput, knockout, dependencies);
            controllersOutput.AppendLine("}");

            controllersOutput.AppendLine();
            
            var moduleName = StringHelpers.ToCamelCase(controllerNode.Name);

            var result = new StringBuilder();
            result.AppendLine("/* This is a generated file. Do not modify or all the changes will be lost. */");
            result.AppendLine($"import * as helpers from \"{serviceHelpersModule}\";");
            if (knockout)
            {
                if (dependencies.Views)
                    result.AppendLine("import * as views from \"../views\";");

                if (dependencies.KoViews)
                    result.AppendLine("import * as koViews from \"./views\";");
            }
            else
            {
                if (dependencies.Views)
                    result.AppendLine("import * as views from \"./views\";");
            }
            result.AppendLine();
            result.Append(controllersOutput);

            OutputModules.Add(knockout ? $"ko/{moduleName}" : moduleName, result.ToString());
        }

        private void WriteActions(ActionsGroupNode controllerNode, StringBuilder controllersOutput, bool knockout, Dependencies dependencies)
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

                WriteAction(methodName, actionNode, controllersOutput, knockout, dependencies);
            }
        }

        private void WriteViews(AssemblyNode assemblyNode)
        {
            StringBuilder result = new StringBuilder();
            var dependencies = new Dependencies();

            foreach (var classNode in assemblyNode.Types)
            {
                WriteTypeDefinition(classNode.Value, result, false, dependencies);
            }

            OutputModules.Add("views", result.ToString());
        }

        private class Dependencies
        {
            public bool ToDate { get; set; }
            public bool FromDate { get; set; }
            public bool ArrayChanged { get; set; }
            public bool DateArrayChanged { get; set; }
            public bool ValidationModule { get; set; }
            public bool KoViews { get; set; }
            public bool Views { get; set; }
            public PrefixModules PrefixModules { get; set; }

        }

        private void WriteKoViews(AssemblyNode assemblyNode)
        {
            var dependencies = new Dependencies
            {
                PrefixModules = PrefixModules.Views
            };
            
            var body = new StringBuilder();
            foreach (var classNode in assemblyNode.Types.Where(x => x.Value.IsObservable))
            {
                WriteKoClass(classNode.Value, body, dependencies);
            }

            var result = new StringBuilder();
            result.AppendLine("import * as ko from \"knockout\";");
            result.AppendLine("import * as views from \"../views\";");
            if (dependencies.ValidationModule)
            {
                result.AppendLine($"import * as validation from \"{validationModule}\";");
            }

            result.AppendLine($"import {{ loading }} from \"{serviceHelpersModule}\";");
            result.AppendLine();

            if (dependencies.ToDate)
            {
                result.AppendLine("function toDate(date:string) {");
                result.AppendLine($"{Tab}return date != undefined ? new Date(date) : undefined;");
                result.AppendLine("}");
            }

            if (dependencies.FromDate)
            {
                result.AppendLine("function fromDate(date:Date) {");
                result.AppendLine($"{Tab}return date != undefined ? date.toISOString() : undefined;");
                result.AppendLine("}");
            }

            if (dependencies.ArrayChanged)
            {
                result.AppendLine("function arrayChanged<T>(array: KnockoutObservableArray<T>, original: T[]) {");
                result.AppendLine($"{Tab}return array() && (!original || array().length !== original.length);");
                result.AppendLine("}");
            }

            if (dependencies.DateArrayChanged)
            {
                result.AppendLine("function dateArrayChanged(array: KnockoutObservableArray<Date>, original: string[]) {");
                result.AppendLine($"{Tab}return array() && (!original || array().length !== original.length);");
                result.AppendLine("}");
            }
            result.Append(body);

            OutputModules.Add("ko/views", result.ToString());
        }

        private void WriteClassName(ClassNode classNode, bool knockout, StringBuilder result)
        {
            result.Append(knockout ? classNode.KoName : classNode.Name);
            
            if (classNode.GenericParameters != null)
            {
                result.Append("<");
                result.Append(string.Join(", ", classNode.GenericParameters));
                result.Append(">");
            }
        }

        private void WriteKoClass(ClassNode classNode, StringBuilder result, Dependencies dependencies)
        {
            result.AppendLine();
            AppendFormatDocumentation(result, classNode.Documentation);

            result.Append("export class ");
            WriteClassName(classNode, true, result);
            result.AppendLine(" {");
            result.Append($"{Tab}originalData: views.");
            WriteClassName(classNode, false, result);
            result.AppendLine(";");
            result.AppendLine($"{Tab}changed: KnockoutComputed<boolean>;");

            bool first = true;
            foreach (var property in classNode.Properties)
            {
                
                if (first)
                {
                    first = false;
                }
                else
                {
                    result.AppendLine();
                }

                if (property.Type.IsObservable)
                {
                    WriteKoProperty(property, result, dependencies);
                }
                else
                {
                    WriteProperty(property, result, false, dependencies);
                }
            }
            result.AppendLine();

            // constructor
            result.Append($"{Tab}constructor(data: views.");
            WriteClassName(classNode, false, result);
            result.AppendLine(") {");
            result.AppendLine($"{Tab}{Tab}this.load(data);");
            result.AppendLine($"{Tab}{Tab}this.originalData = data;");

            // changed
            result.AppendLine($"{Tab}{Tab}this.changed = ko.pureComputed(() =>");

            first = true;
            foreach (var propertyNode in classNode.Properties.Where(x => x.Type.IsObservable))
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

                if (propertyNode.Type.Type == TypeIdentifier.DateTime && !propertyNode.Type.IsCollection)
                {
                    result.Append("fromDate(");
                    dependencies.FromDate = true;
                }
                
                if (propertyNode.Type.IsCollection)
                {
                    if (propertyNode.Type.Type == TypeIdentifier.Object && propertyNode.Type.Class.IsObservable)
                    {
                        result.Append($"this.{propertyNode.Name}()");

                        result.Append(" != undefined && (");
                        result.Append($"this.{propertyNode.Name}().length !== this.originalData.{propertyNode.Name}.length");
                        result.Append($" || this.{propertyNode.Name}().some(x => x.changed())");
                        result.AppendLine(")");
                    }
                    else
                    {
                        if (propertyNode.Type.Type == TypeIdentifier.DateTime)
                        {
                            result.AppendLine(
                                $"dateArrayChanged(this.{propertyNode.Name}, this.originalData.{propertyNode.Name})");
                            dependencies.DateArrayChanged = true;
                        }
                        else
                        {
                            result.AppendLine($"arrayChanged(this.{propertyNode.Name}, this.originalData.{propertyNode.Name})");
                            dependencies.ArrayChanged = true;
                        }
                    }
                }
                else
                {
                    result.Append($"this.{propertyNode.Name}()");

                    if (propertyNode.Type.Type == TypeIdentifier.Object && propertyNode.Type.Class.IsObservable)
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
            foreach (var property in classNode.Properties)
            {
                result.Append($"{Tab}{Tab}{Tab}{property.Name}: ");
                if (property.Type.Type == TypeIdentifier.DateTime && !property.Type.IsCollection)
                {
                    result.Append("fromDate(");
                    dependencies.FromDate = true;
                }
                result.Append($"this.{property.Name}");
                if (property.Type.IsObservable)
                {
                    result.Append("()");
                }

                if (property.Type.Type == TypeIdentifier.Object && property.Type.Class.IsObservable)
                {
                    result.Append($" ? this.{property.Name}");
                    if (property.Type.IsObservable)
                    {
                        result.Append("()");
                    }
                    if (property.Type.IsCollection)
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
                        if (property.Type.IsCollection)
                        {
                            result.Append($" && this.{property.Name}");
                            if (property.Type.IsObservable) result.Append("()");
                            result.Append(".map(x => x.toISOString())");
                        }
                        else
                            result.Append(")");
                    }
                }
                result.AppendLine(",");
            }
            result.AppendLine($"{Tab}{Tab}}};");
            result.AppendLine($"{Tab}}}");

            // load() method
            result.Append($"{Tab}public load(data: views.");
            WriteClassName(classNode, false, result);
            result.AppendLine(") {");
            foreach (var propertyNode in classNode.Properties)
            {
                result.Append($"{Tab}{Tab}this.{propertyNode.Name}");
                if (propertyNode.Type.IsObservable)
                {
                    if (propertyNode.Type.Type == TypeIdentifier.Object && propertyNode.Type.Class.IsObservable)
                    {
                        var propertyClassNode = propertyNode.Type.Class;
                        if (propertyNode.Type.IsCollection)
                        {
                            result.Append($"(data.{propertyNode.Name} ? data.{propertyNode.Name}.map(x => new {propertyClassNode.KoName}(x)) : null)");
                        }
                        else
                        {
                            result.Append(
                                $"(data.{propertyNode.Name} ? new {propertyClassNode.KoName}(data.{propertyNode.Name}) : null)");
                        }
                    }
                    else
                    {
                        if (propertyNode.Type.Type == TypeIdentifier.DateTime)
                        {
                            if (propertyNode.Type.IsCollection)
                            {
                                result.Append(
                                    $"(data.{propertyNode.Name} && data.{propertyNode.Name}.map(x => new Date(x)))");
                            }
                            else
                            {
                                result.Append($"(toDate(data.{propertyNode.Name}))");
                                dependencies.ToDate = true;
                            }
                        }
                        else
                            result.Append($"(data.{propertyNode.Name})");
                    }
                }
                else
                {
                    if (propertyNode.Type.Type == TypeIdentifier.DateTime)
                    {
                        result.Append($" = toDate(data.{propertyNode.Name})");
                        dependencies.ToDate = true;
                    }
                    else
                    {
                        result.Append($" = data.{propertyNode.Name}");
                    }
                }
                result.AppendLine(";");
            }
            result.AppendLine($"{Tab}}}");

            // isValid() property
            if (classNode.Properties.Any(x => x.NeedValidation()))
            {
                result.AppendLine();
                result.Append($"{Tab}public isValid = ko.computed(() => !loading()");

                foreach (var source in classNode.Properties.Where(x => x.NeedValidation()))
                {
                    result.Append($" && !this.{source.Name}.validating() && !this.{source.Name}.errorMessage()");
                }
                result.AppendLine(");");
            }
            result.AppendLine("}");
        }

        private void WriteKoProperty(PropertyNode property, StringBuilder result, Dependencies dependencies)
        {
            AppendFormatDocumentation(result, property.Documentation);

            result.Append($"{Tab}{property.Name}");

            if (property.Type.IsCollection)
            {
                result.Append(" = ko.observableArray<");
                WriteType(property.Type, result, false, dependencies, allowObservable: true, notACollection: true);
            }
            else
            {
                if (property.NeedValidation())
                {
                    result.Append(" = validation.validableObservable<");
                    dependencies.ValidationModule = true;
                }
                else
                {
                    result.Append(" = ko.observable<");
                }

                WriteType(property.Type, result, false, dependencies, allowObservable: true);
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
            else
            {
                result.AppendLine($"export const enum {name} {{");
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
            if (!property.IsRequired)
            {
                result.Append("?");
            }
            result.Append(": ");
            WriteType(property.Type, result, edition, dependencies, allowObservable: false);
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
        
        private void WriteType(TypeNode typeNode, StringBuilder result, bool edition, Dependencies dependencies, bool allowObservable, bool notACollection = false)
        {
            if (typeNode.IsDictionary)
            {
                result.Append("{ [key: string]: ");
            }

            switch (typeNode.Type)
            {
                case TypeIdentifier.Boolean:
                    result.Append("boolean");
                    break;
                case TypeIdentifier.DateTime:
                    result.Append(allowObservable ? "Date" : "string");
                    break;
                case TypeIdentifier.Object:
                    if (allowObservable && typeNode.Class.IsObservable)
                    {
                        if (dependencies.PrefixModules.HasFlag(PrefixModules.KoViews))
                        {
                            dependencies.KoViews = true;
                            result.Append("koViews.");
                        }
                        result.Append(typeNode.Class.KoName);
                    }
                    else
                    {
                        if (dependencies.PrefixModules.HasFlag(PrefixModules.Views))
                        {
                            dependencies.Views = true;
                            result.Append("views.");
                        }
                        result.Append(typeNode.Class.Name);
                    }
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
                    result.Append("number");
                    break;
                case TypeIdentifier.Guid:
                case TypeIdentifier.String:
                case TypeIdentifier.TimeSpan:
                    result.Append("string");
                    break;
                case TypeIdentifier.Enum:
                    if (dependencies.PrefixModules.HasFlag(PrefixModules.Views))
                    {
                        result.Append("views.");
                        dependencies.Views = true;
                    }
                    result.Append(typeNode.Class.Name);
                    break;
                case TypeIdentifier.Any:
                    result.Append("any");
                    break;
                case TypeIdentifier.Union:
                    {
                        bool first = true;
                        foreach (var type in typeNode.Union)
                        {
                            if (first)
                                first = false;
                            else
                                result.Append(" | ");
                            WriteType(type, result, edition, dependencies, allowObservable);
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

            if (typeNode.IsCollection && !notACollection)
            {
                result.Append("[]");
            }

            if (typeNode.IsDictionary)
            {
                result.Append(" }");
            }
        }

        private void WriteAction(string methodName, ActionNode actionNode, StringBuilder controllersOutput, bool knockout, Dependencies dependencies)
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

            controllersOutput.Append($"{Tab}{methodName}(params: {{");
            
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
                WriteType(parameter.Type, controllersOutput, true, dependencies, allowObservable: knockout);
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
                controllersOutput.Append(actionNode.Return.Type.IsCollection ? "List" : "Single");
                if ((actionNode.Return.Type.Type == TypeIdentifier.Object && actionNode.Return.Type.Class.IsObservable&& knockout) || actionNode.Return.Type.Type == TypeIdentifier.DateTime)
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
                    WriteType(actionNode.Return.Type, controllersOutput, false, dependencies, allowObservable: false, notACollection: true);
                    
                    if (actionNode.Return.Type.Type == TypeIdentifier.Object && actionNode.Return.Type.Class.IsObservable && knockout)
                    {
                        controllersOutput.Append(", ");
                        WriteType(actionNode.Return.Type, controllersOutput, false, dependencies, allowObservable: true, notACollection: true);
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
                else if (actionNode.Return.Type.Type == TypeIdentifier.Object && actionNode.Return.Type.Class.IsObservable && knockout)
                {
                    dependencies.KoViews = true;
                    controllersOutput.Append(", view => new koViews.");
                    controllersOutput.Append(actionNode.Return.Type.Class.KoName);
                    controllersOutput.Append("(view)");
                }
            }

            var body = actionNode.Parameters.FirstOrDefault(x => x.Position == ParameterPosition.Body);
            if (body != null)
            {
                controllersOutput.Append(", JSON.stringify(params.");
                controllersOutput.Append(body.Name);
                if (body.Type.Type == TypeIdentifier.Object && body.Type.Class.IsObservable && knockout)
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

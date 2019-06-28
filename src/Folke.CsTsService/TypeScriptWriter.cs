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
            if (options.HasFlag(TypeScriptOptions.Knockout))
            {
                Directory.CreateDirectory($"{basePath}/ko");
            }
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

            if (options.HasFlag(TypeScriptOptions.Knockout))
            {
                WriteKoViews(assemblyNode);
            }
            else
            {
                WriteViews(assemblyNode);
                WriteEnums(assemblyNode);
            }
            
            var output = new StringBuilder();
            foreach (var controllerNode in controllers)
            {
                output.AppendLine($"import * as {StringHelpers.ToCamelCase(controllerNode.Name)}Group from \"./{StringHelpers.ToCamelCase(controllerNode.Name)}\";");
            }
            output.AppendLine($"import {{ ApiClient }} from \"{serviceHelpersModule}\";");
            if (options.HasFlag(TypeScriptOptions.Knockout))
            {
                output.AppendLine("import * as views from \"../views\";");
                output.AppendLine("import * as koViews from \"./views\";");
                output.AppendLine("export * from \"../enums\";");
                output.Append("export { ");
                output.Append(string.Join(", ",
                    assemblyNode.Classes.Values.Where(x => !x.HasObservable && x.Properties != null).Select(x => x.Name)));
                output.AppendLine(" } from \"../views\";");
            }
            else
            {
                output.AppendLine("import * as views from \"./views\";");
                output.AppendLine("export * from \"./enums\";");
           }

            output.AppendLine("export * from \"./views\";");
            output.AppendLine();

            output.AppendLine(options.HasFlag(TypeScriptOptions.Knockout) ? "export class KoServices {" : "export class Services {");
            output.AppendLine("\tconstructor(private client: ApiClient) {}");

            foreach (var controllerNode in controllers)
            {
                var moduleName = StringHelpers.ToCamelCase(controllerNode.Name);
                output.AppendLine($"\t{moduleName} = new {moduleName}Group.{controllerNode.Name}Controller(this.client);");
            }

            output.AppendLine("}");
            output.AppendLine();
         
            OutputModules.Add(options.HasFlag(TypeScriptOptions.Knockout) ? "ko/services" : "services", output.ToString());
        }

        private void WriteEnums(AssemblyNode assemblyNode)
        {
            StringBuilder result = new StringBuilder();
            var dependencies = new Dependencies();

            foreach (var classNode in assemblyNode.Classes.Where(x => x.Value.Values != null))
            {
                WriteTypeDefinition(classNode.Value, result, false, dependencies);
            }

            OutputModules.Add("enums", result.ToString());
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
            if (options.HasFlag(TypeScriptOptions.Knockout))
            {
                if (dependencies.Views)
                    result.AppendLine("import * as views from \"../views\";");
                if (dependencies.Enums)
                    result.AppendLine("import * as enums from \"../enums\";");

                if (dependencies.KoViews)
                    result.AppendLine("import * as koViews from \"./views\";");
            }
            else
            {
                if (dependencies.Enums)
                    result.AppendLine("import * as enums from \"./enums\";");
                if (dependencies.Views)
                    result.AppendLine("import * as views from \"./views\";");
            }
            result.AppendLine();
            result.Append(controllersOutput);

            OutputModules.Add(options.HasFlag(TypeScriptOptions.Knockout) ? $"ko/{moduleName}" : moduleName, result.ToString());
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

            foreach (var classNode in assemblyNode.Classes.Where(x => x.Value.Properties != null))
            {
                WriteTypeDefinition(classNode.Value, body, false, dependencies);
            }
            var result = new StringBuilder();
            if (dependencies.Enums)
            {
                result.AppendLine("import * as enums from \"./enums\";");
            }
            result.Append(body);
            OutputModules.Add("views", result.ToString());
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

        private void WriteKoViews(AssemblyNode assemblyNode)
        {
            var dependencies = new Dependencies
            {
                PrefixModules = PrefixModules.Views | PrefixModules.Enums
            };
            
            var body = new StringBuilder();
            foreach (var classNode in assemblyNode.Classes.Where(x => x.Value.HasObservable))
            {
                WriteKoClass(classNode.Value, body, dependencies);
            }

            var result = new StringBuilder();
            result.AppendLine("import * as ko from \"knockout\";");
            if (dependencies.Views)
                result.AppendLine("import * as views from \"../views\";");
            if (dependencies.Enums)
                result.AppendLine("import * as enums from \"../enums\";");
            if (dependencies.ValidationModule)
            {
                result.AppendLine($"import * as validation from \"{validationModule}\";");
            }

            result.AppendLine();

            if (dependencies.ServiceHelpers)
            {
                result.AppendLine($"import * as helpers from \"folke-ko-service-helpers\";");
            }

            //if (dependencies.FromDate)
            //{
            //    result.AppendLine("function fromDate(date:Date) {");
            //    result.AppendLine($"{Tab}return date != undefined ? date.toISOString() : undefined;");
            //    result.AppendLine("}");
            //}

            //if (dependencies.ArrayChanged)
            //{
            //    result.AppendLine("function arrayChanged<T>(array: KnockoutObservableArray<T>, original: T[]) {");
            //    result.AppendLine($"{Tab}return array() && (!original || array().length !== original.length);");
            //    result.AppendLine("}");
            //}

            //if (dependencies.DateArrayChanged)
            //{
            //    result.AppendLine("function dateArrayChanged(array: KnockoutObservableArray<Date>, original: string[]) {");
            //    result.AppendLine($"{Tab}return array() && (!original || array().length !== original.length);");
            //    result.AppendLine("}");
            //}
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
            dependencies.Views = true;
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
                    result.Append("helpers.fromDate(");
                    dependencies.ServiceHelpers= true;
                }
                
                if (propertyNode.Type.IsCollection)
                {
                    if (propertyNode.Type.Type == TypeIdentifier.Object && propertyNode.Type.Class.HasObservable)
                    {
                        result.AppendLine($"helpers.hasArrayOfObjectsChanged(this.{propertyNode.Name}, data.{propertyNode.Name})");
                    }
                    else
                    {
                        if (propertyNode.Type.Type == TypeIdentifier.DateTime)
                        {
                            result.AppendLine(
                                $"helpers.dateArrayChanged(this.{propertyNode.Name}, this.originalData.{propertyNode.Name})");
                            dependencies.ServiceHelpers = true;
                        }
                        else
                        {
                            result.AppendLine($"helpers.arrayChanged(this.{propertyNode.Name}, this.originalData.{propertyNode.Name})");
                            dependencies.ServiceHelpers = true;
                        }
                    }
                }
                else
                {
                    result.Append($"this.{propertyNode.Name}()");

                    if (propertyNode.Type.Type == TypeIdentifier.Object && propertyNode.Type.Class.HasObservable)
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
                    result.Append("helpers.fromDate(");
                    dependencies.ServiceHelpers = true;
                }
                result.Append($"this.{property.Name}");
                if (property.Type.IsObservable)
                {
                    result.Append("()");
                }

                if (property.Type.Type == TypeIdentifier.Object && property.Type.Class.HasObservable)
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
                    if (propertyNode.Type.Type == TypeIdentifier.Object && propertyNode.Type.Class.HasObservable)
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
                                result.Append($"(helpers.toDate(data.{propertyNode.Name}))");
                                dependencies.ServiceHelpers = true;
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
                        result.Append($" = helpers.toDate(data.{propertyNode.Name})");
                        dependencies.ServiceHelpers = true;
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
                result.Append($"{Tab}public isValid = ko.computed(() => ");

                var propertyNodes = classNode.Properties.Where(x => x.NeedValidation()).ToArray();
                if (!propertyNodes.Any()) result.Append("true");
                else
                    result.Append(string.Join(" && ",
                        propertyNodes.Select(source =>
                            $" !this.{source.Name}.validating() && !this.{source.Name}.errorMessage()")));
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
                    result.Append($".addValidator(validation.hasMaxLength({property.MaximumLength.Value}))");
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
            result.Append(": ");
            WriteType(property.Type, result, edition, dependencies, allowObservable: false);
            if (!property.IsRequired)
            {
                result.Append(" | null");
            }
            result.AppendLine(";");
        }

        [Flags]
        private enum PrefixModules
        {
            None = 0,
            Views = 1,
            KoViews = 2,
            Enums = 4,
            All = 7
        }
        
        private void WriteType(TypeNode typeNode, StringBuilder result, bool edition, Dependencies dependencies, bool allowObservable, bool notACollection = false)
        {
            foreach (var modifier in typeNode.Modifiers)
            {
                if (modifier == TypeModifier.Dictionary)
                {
                    result.Append("{ [key: string]: ");
                }
            }

            WriteTypeWithoutModifiers(typeNode, result, edition, dependencies, allowObservable);

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

        private void WriteTypeWithoutModifiers(TypeNode typeNode, StringBuilder result, bool edition, Dependencies dependencies,
            bool allowObservable)
        {
            switch (typeNode.Type)
            {
                case TypeIdentifier.Boolean:
                    result.Append("boolean");
                    break;
                case TypeIdentifier.DateTime:
                    result.Append(allowObservable ? "Date" : "string");
                    break;
                case TypeIdentifier.Object:
                    if (allowObservable && typeNode.Class.HasObservable)
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
                case TypeIdentifier.Byte:
                    result.Append("number");
                    break;
                case TypeIdentifier.Guid:
                case TypeIdentifier.String:
                case TypeIdentifier.TimeSpan:
                    result.Append("string");
                    break;
                case TypeIdentifier.Enum:
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
                WriteType(parameter.Type, controllersOutput, true, dependencies, allowObservable: options.HasFlag(TypeScriptOptions.Knockout));
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
                    WriteType(actionNode.Return.Type, controllersOutput, false, dependencies, allowObservable: false);
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

                    if (parameter.Type.Type == TypeIdentifier.DateTime && options.HasFlag(TypeScriptOptions.Knockout))
                    {
                        if (options.HasFlag(TypeScriptOptions.ParametersInObject))
                            controllersOutput.Append($" && params.{parameter.Name}.toISOString()");
                        else
                            controllersOutput.Append($" && {parameter.Name}.toISOString()");
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
            
            var body = actionNode.Parameters.FirstOrDefault(x => x.Position == ParameterPosition.Body);
            if (body != null)
            {
                controllersOutput.Append(", JSON.stringify(");
                if (options.HasFlag(TypeScriptOptions.ParametersInObject))
                    controllersOutput.Append("params.");
                controllersOutput.Append(body.Name);
                if (body.Type.Type == TypeIdentifier.Object && body.Type.Class.HasObservable && options.HasFlag(TypeScriptOptions.Knockout))
                {
                    controllersOutput.Append(".toJs()");
                }
                controllersOutput.Append(")");
            }
            else
            {
                controllersOutput.Append(", undefined");
            }

            controllersOutput.Append(")");

            if (actionNode.Return != null && actionNode.Return.Type.Type == TypeIdentifier.Object && actionNode.Return.Type.Class.HasObservable && options.HasFlag(TypeScriptOptions.Knockout))
            {
                controllersOutput.Append(".then(");
                if (actionNode.Return.Type.IsCollection)
                {
                    controllersOutput.Append("x => x.map(");
                }

                if (actionNode.Return.Type.Type == TypeIdentifier.DateTime)
                {
                    controllersOutput.Append("view => new Date(view)");
                }
                else if (actionNode.Return.Type.Type == TypeIdentifier.Object && actionNode.Return.Type.Class.HasObservable && options.HasFlag(TypeScriptOptions.Knockout))
                {
                    dependencies.KoViews = true;
                    controllersOutput.Append("view => new koViews.");
                    controllersOutput.Append(actionNode.Return.Type.Class.KoName);
                    controllersOutput.Append("(view)");
                }

                if (actionNode.Return.Type.IsCollection) controllersOutput.Append(")");
                controllersOutput.Append(")");
            }

            controllersOutput.AppendLine(";");
            controllersOutput.AppendLine($"{Tab}}}");
        }
    }
}

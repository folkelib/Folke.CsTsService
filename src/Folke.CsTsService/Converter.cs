using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Folke.CsTsService
{
    public class Converter
    {
        private readonly HashSet<Type> enums;
        private readonly Dictionary<Type, bool> views;
        private readonly StringBuilder controllerOut;
        private readonly StringBuilder viewOut;
        private readonly IApiAdapter apiAdapter;

        public Converter(IApiAdapter apiAdapter)
        {
            this.apiAdapter = apiAdapter;
            enums = new HashSet<Type>();
            views = new Dictionary<Type, bool>();
            controllerOut = new StringBuilder();
            viewOut = new StringBuilder();
        }
        
        public void Write(IEnumerable<Assembly> assemblies, string outputPath, string helperModule, string validatorModule)
        {
            var controllers = LoadControllers(assemblies);

            foreach (var type in controllers)
            {
                WriteController(type);
            }

            // Pour chacun des Dtos
            while (views.Any(x => !x.Value))
            {
                WriteLastView();
            }

            foreach (var type in enums)
            {
                WriteEnum(type);
            }

            var fileContent = new StringBuilder();
            fileContent.AppendLine("// Generated code, do not edit");
            fileContent.AppendLine("import ko = require('knockout');");
            fileContent.AppendLine($"import helper = require('{helperModule}');");
            fileContent.AppendLine($"import validator = require('{validatorModule}');");
            fileContent.AppendLine();
            fileContent.AppendLine("export var loading = helper.loading;");
            fileContent.AppendLine();
            fileContent.Append(viewOut);
            fileContent.AppendLine();
            fileContent.Append(controllerOut);

            File.WriteAllText(outputPath, fileContent.ToString());
        }

        private void WriteEnum(Type type)
        {
            viewOut.AppendLine("export enum " + type.Name + " {");
            bool first = true;
            foreach (var value in Enum.GetNames(type))
            {
                if (!first)
                    viewOut.AppendLine(",");
                first = false;
                viewOut.Append("\t" + value);
            }
            viewOut.AppendLine();
            viewOut.AppendLine("}");
            viewOut.AppendLine();
        }

        private void WriteLastView()
        {
            var type = views.First(x => !x.Value).Key;
            views[type] = true;
            
            // On construit le constructeur au fur et à mesure
            var constructor = new StringBuilder("constructor(data?:" + type.Name + "Data) {" + Environment.NewLine);
            constructor.AppendLine("\t\tthis.originalData = data = data || {};");
            constructor.AppendLine("\t\tthis.load(data);");

            var load = new StringBuilder("public load(data:" + type.Name + "Data) {" + Environment.NewLine);


            // On construit également la méthode qui convertit en javascript
            var toData = new StringBuilder("public toJs() {" + Environment.NewLine + "\t\treturn {" + Environment.NewLine);

            // Une méthode pour vérifier si la valeur a changé
            var hasChanged = new StringBuilder("\tthis.changed = ko.computed(() => " + Environment.NewLine);
            hasChanged.Append("\t\t\t");

            var view = new StringBuilder();
            // Hop, on crée la classe dans output
            view.AppendLine("export class " + type.Name + " {");
            view.AppendLine("\toriginalData: " + type.Name + "Data;");
            view.AppendLine("\tchanged: KnockoutComputed<boolean>;");
            /*view.AppendLine("\tvalid: KnockoutComputed<boolean>");
                view.AppendLine("\tcanSave: KnockoutComputed<boolean>");*/
            view.AppendLine();
            //var valids = new List<string>();
            bool firstProperty = true;

            var viewData = new StringBuilder();
            viewData.AppendLine("export interface " + type.Name + "Data {");

            var validableObservables = new List<string>();
            var validableReferenceObservables = new List<string>();
            var declaredProperties = type.GetProperties().ToList();
            foreach (var member in declaredProperties)
            {
                bool last = member.Equals(declaredProperties.Last());

                // Add the constructor
                var camel = Camelize(member.Name);
                view.Append("\t");
                view.Append(camel);
                view.Append(": ");
                load.Append("\t\tthis." + camel);
                toData.Append("\t\t\t" + camel + ": ");

                viewData.Append("\t");
                viewData.Append(camel);
                viewData.Append("?: ");

                // The type
                var propertyType = member.PropertyType;
                //bool nullable;
                if (Nullable.GetUnderlyingType(propertyType) != null)
                {
                    //nullable = true;
                    propertyType = Nullable.GetUnderlyingType(propertyType);
                }
                //else
                //{
                //    nullable = !propertyType.IsValueType;
                //}

                if (member.Name == "Id" && propertyType == typeof (int))
                {
                    // Do not make an observable of an id
                    view.AppendLine("number;");
                    load.AppendLine(" = data.id;");
                    toData.AppendLine("this.id" + (last ? "" : ","));
                    viewData.AppendLine("number;");
                }
                else
                {
                    if (!firstProperty && member.Name != "_destroy")
                        hasChanged.Append("\t\t\t|| ");

                    Type elementType = propertyType.GetTypeInfo().IsGenericType ? propertyType.GenericTypeArguments[0] : propertyType;

                    var typeName = GetTypeName(elementType, member);

                    if (member.Name == "_destroy")
                    {
                        view.Append(typeName);
                        view.AppendLine(";");
                        load.AppendLine(" = data." + camel + ";");
                        toData.AppendLine("this." + camel + (last ? "" : ","));
                        viewData.AppendLine(typeName + ";");
                    }
                    else if (propertyType.GetTypeInfo().IsGenericType)
                    {
                        var collectionType = typeof (IList<>).MakeGenericType(elementType);
                        var readonlyCollectionType = typeof (IReadOnlyList<>).MakeGenericType(elementType);
                        var collectionTypeInfo = collectionType.GetTypeInfo();
                        var propertTypeInfo = propertyType.GetTypeInfo();
                        // A collection of values
                        if (propertTypeInfo.IsAssignableFrom(collectionTypeInfo) ||
                            propertTypeInfo.IsAssignableFrom(readonlyCollectionType.GetTypeInfo()))
                        {
                            RegisterType(elementType);
                            view.Append("KnockoutObservableArray<" + typeName + ">");
                            view.AppendLine(" = ko.observableArray<" + typeName + ">();");

                            if (typeName == "Date" || typeName == "string" || elementType.GetTypeInfo().IsEnum)
                                viewData.AppendLine(typeName + "[];");
                            else
                                viewData.AppendLine(typeName + "Data[];");

                            load.Append("(data['" + camel + "'] ? (<any[]>data." + camel + ").map(value => ");
                            if (NeedNew(elementType))
                            {
                                load.Append("new " + typeName + "(value)");
                                if (typeName == "Date")
                                {
                                    toData.AppendLine("this." + camel + "()" + (last ? "" : ","));
                                    hasChanged.Append("helper.hasArrayChanged(this." + camel + ", this.originalData." + camel +
                                                      ")");
                                }
                                else
                                {
                                    toData.AppendLine("this." + camel + "() != null ? this." + camel +
                                                      "().map(v => v.toJs()) : null" + (last ? "" : ","));
                                    hasChanged.Append("helper.hasArrayOfObjectsChanged(this." + camel + ", this.originalData." +
                                                      camel + ")");
                                }
                            }
                            else
                            {
                                load.Append("value");
                                toData.AppendLine("this." + camel + "()" + (last ? "" : ","));
                                hasChanged.Append("helper.hasArrayChanged(this." + camel + ", this.originalData." + camel + ")");
                            }
                            load.AppendLine(") : null);");
                        }
                    }
                    else
                    {
                        var requiredAttribute = member.GetCustomAttribute<RequiredAttribute>();
                        var emailAddressAttribute = member.GetCustomAttribute<EmailAddressAttribute>();
                        var stringLengthAttribute = member.GetCustomAttribute<StringLengthAttribute>();
                        var compareAttribute = member.GetCustomAttribute<CompareAttribute>();
                        var minLengthAttribute = member.GetCustomAttribute<MinLengthAttribute>();
                        var maxLengthAttribute = member.GetCustomAttribute<MaxLengthAttribute>();
                        var rangeAttribute = member.GetCustomAttribute<RangeAttribute>();
                        var needValidation = requiredAttribute != null || emailAddressAttribute != null ||
                                             stringLengthAttribute != null || compareAttribute != null ||
                                             minLengthAttribute != null
                                             || rangeAttribute != null || maxLengthAttribute != null;

                        if (needValidation)
                        {
                            validableObservables.Add(camel);
                            view.Append("validator.ValidableObservable<" + typeName + ">");
                        }
                        else
                            view.Append("KnockoutObservable<" + typeName + ">");

                        RegisterType(propertyType);
                        if (NeedNew(propertyType))
                        {
                            if (NeedValidation(propertyType))
                                validableReferenceObservables.Add(camel);
                            view.AppendLine(" = ko.observable<" + typeName + ">();");
                            if (typeName == "Date")
                            {
                                load.AppendLine("(data." + camel + " ? new Date(data." + camel + ") : null);");
                                toData.AppendLine("this." + camel + "()" + (last ? "" : ","));
                                viewData.AppendLine("string;");
                            }
                            else
                            {
                                load.AppendLine("(data." + camel + " ? new " + typeName + "(data." + camel + ") : null);");
                                toData.AppendLine("this." + camel + "() ? this." + camel + "().toJs() : null" +
                                                  (last ? "" : ","));
                                viewData.AppendLine(typeName + "Data;");
                            }
                        }
                        else
                        {
                            if (needValidation)
                            {
                                view.Append(" = validator.validableObservable<" + typeName + ">()");
                                if (emailAddressAttribute != null)
                                    view.Append(".addValidator(validator.isEmail)");
                                if (requiredAttribute != null)
                                    view.Append(".addValidator(validator.isRequired)");
                                if (stringLengthAttribute != null)
                                    view.Append(".addValidator(validator.hasMinLength(" +
                                                stringLengthAttribute.MinimumLength + "))");
                                if (minLengthAttribute != null)
                                    view.Append($".addValidator(validator.hasMinLength({minLengthAttribute.Length}))");
                                if (maxLengthAttribute != null)
                                    view.Append($".addValidator(validator.hasMaxLength({maxLengthAttribute.Length}))");

                                if (compareAttribute != null)
                                    view.Append(".addValidator(validator.areSame(this." +
                                                Camelize(compareAttribute.OtherProperty) + "))");
                                if (rangeAttribute != null)
                                    view.Append(".addValidator(validator.isInRange(" +
                                                rangeAttribute.Minimum + ", " + rangeAttribute.Maximum + "))");

                                view.AppendLine(";");
                            }
                            else
                                view.AppendLine(" = ko.observable<" + typeName + ">();");
                            load.AppendLine("(data." + camel + ");");
                            toData.AppendLine("this." + camel + "()" + (last ? "" : ","));
                            viewData.AppendLine(typeName + ";");
                        }
                        if (propertyType == typeof (DateTime))
                        {
                            hasChanged.AppendLine("helper.hasDateChanged(this." + camel + "(), this.originalData." + camel + ")");
                            //if (!nullable)
                            //    valids.Add("this." + camel + "() != null");
                        }
                        else if (NeedNew(propertyType))
                        {
                            hasChanged.AppendLine("helper.hasObjectChanged(this." + camel + "(), this.originalData." + camel + ")");
                            //valids.Add("(this." + camel + "() == null || this." + camel + "().valid())");
                        }
                        else
                        {
                            hasChanged.AppendLine("this." + camel + "() !== this.originalData." + camel);
                            //if (!nullable)
                            //    valids.Add("this." + camel + "() !== null");
                        }
                    }
                    firstProperty = false;
                }
            }

            constructor.Append("\t" + hasChanged);
            if (firstProperty)
                constructor.AppendLine("false");
            constructor.AppendLine("\t\t);");
            /*constructor.Append("\t\tthis.valid = ko.computed(() => ");
                if (valids.Count == 0)
                    constructor.Append("true");
                else
                    constructor.Append(string.Join(" && ", valids));
                constructor.AppendLine(");");
                constructor.AppendLine("\t\tthis.canSave = ko.computed(() => this.changed() && this.valid());");*/
            constructor.AppendLine("\t}");

            load.AppendLine("\t}");
            toData.Append("\t\t}" + Environment.NewLine + "\t}");
            view.AppendLine();
            view.Append("\t" + constructor);
            view.AppendLine();
            view.AppendLine("\t" + toData);
            view.AppendLine();
            view.AppendLine("\t" + load);
            view.AppendLine("\tpublic reset() {");
            view.AppendLine("\t\tthis.load(this.originalData);");
            view.AppendLine("\t}");

            if (validableObservables.Count > 0 || validableReferenceObservables.Count > 0)
            {
                view.AppendLine();
                view.AppendLine("\tpublic isValid = ko.computed(() => {");
                view.Append("\t\treturn !loading() ");
                foreach (var validableObservable in validableObservables)
                {
                    view.Append(" && ");
                    view.Append("!this." + validableObservable + ".validating() && ");
                    view.Append("this." + validableObservable + ".errorMessage() == null");
                }

                foreach (var validableReferenceObservable in validableReferenceObservables)
                {
                    view.Append(" && ");
                    view.Append("(!this." + validableReferenceObservable + "() || this." + validableReferenceObservable + "().isValid())");
                }
                view.AppendLine(";");
                view.AppendLine("\t});");
            }

            view.AppendLine("}" + Environment.NewLine);

            if (apiAdapter.IsObservableObject(type))
                viewOut.Append(view);

            viewData.AppendLine("}");
            viewData.AppendLine();
            viewOut.Append(viewData);
        }

        private bool NeedValidation(Type type)
        {
            foreach (var property in type.GetProperties())
            {
                var attributes = property.CustomAttributes;
                foreach (var customAttributeData in attributes)
                {
                    if (customAttributeData.AttributeType.GetTypeInfo().IsSubclassOf(typeof (ValidationAttribute)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void WriteController(Type type)
        {
            var routePrefix = apiAdapter.GetRoutePrefixName(type);
            if (routePrefix == null) return;
            routePrefix = routePrefix.Replace("[controller]", type.Name.Replace("Controller", ""));
            controllerOut.AppendLine("export class " + type.Name + " {");
            bool firstController = true;
            foreach (var method in type.GetMethods().Where(m => m.IsPublic))
            {
                if (method.IsSpecialName) continue;

                if (!apiAdapter.IsAction(method))
                    continue;

                var returnType = apiAdapter.GetReturnType(method);

                bool isCollection = CheckCollectionType(ref returnType);
                
                if (!firstController)
                    controllerOut.AppendLine();
                firstController = false;

                string returnTypeName = null;
                if (returnType != null)
                {
                    returnTypeName = GetTypeName(returnType, null);
                    RegisterType(returnType);
                }

                bool overridable = returnType != null && returnType != typeof (DateTime) && NeedNew(returnType);
                var viewDataReturnType = overridable
                    ? returnTypeName + "Data"
                    : (returnType == typeof (DateTime) ? "string" : returnTypeName);
                //var fullViewDataReturnType = viewDataReturnType;
                //if (isCollection)
                //    fullViewDataReturnType += "[]";

                var name = Camelize(method.Name);
                var over = new StringBuilder();
                if (overridable)
                {
                    controllerOut.Append("\tpublic " + name + "T<T extends " + returnTypeName + ">(factory: (data:" +
                                viewDataReturnType);
                    controllerOut.Append(" ) => T, parameters:{");
                    over.Append("\tpublic " + name + " = (parameters:{");
                }
                else
                    controllerOut.Append("\tpublic " + name + "(parameters:{");

                bool first = true;
                string queryParameters = null;
                ParameterInfo bodyParameter = null;
                int start = controllerOut.Length;

                foreach (var parameter in method.GetParameters())
                {
                    if (!first)
                    {
                        controllerOut.Append("; ");
                    }

                    first = false;
                    var parameterType = parameter.ParameterType;

                    bool isCollectionParameter = CheckCollectionType(ref parameterType);

                    RegisterType(parameterType);
                    string parameterTypeName = GetTypeName(parameterType, null);

                    if (isCollectionParameter)
                        parameterTypeName += "[]";
                    controllerOut.Append(parameter.Name);
                    if (parameter.IsOptional)
                        controllerOut.Append("?");
                    controllerOut.Append(':');
                    controllerOut.Append(parameterTypeName);

                    if (apiAdapter.IsParameterFromUri(parameter))
                    {
                        if (queryParameters != null)
                            queryParameters += ", ";
                        queryParameters += parameter.Name + ": parameters." + parameter.Name;
                    }
                    if (apiAdapter.IsParameterFromBody(parameter))
                        bodyParameter = parameter;
                }
                /*controllerOut.Append("}, success?:(");
                
                if (returnTypeName != null)
                {
                    controllerOut.Append(Camelize(returnType.Name));
                    if (isCollection)
                        controllerOut.Append('s');
                    controllerOut.Append(':');
                    if (overridable)
                        over.Append(controllerOut.ToString().Substring(start));

                    if (overridable)
                    {
                        controllerOut.Append("T");
                        over.Append(returnTypeName);
                        start = controllerOut.ToString().Length;
                    }
                    else
                        controllerOut.Append(returnTypeName);

                    if (isCollection)
                        controllerOut.Append("[]");
                }
                */
                if (overridable)
                {
                    over.Append(controllerOut.ToString().Substring(start));
                    over.AppendLine("}) => {");
                    over.AppendLine("\t\treturn this." + name + "T(data => new " + returnTypeName +
                                    "(data), parameters);");
                    over.AppendLine("\t}");
                }
                controllerOut.AppendLine("}) {");
                string httpMethod;
                if (apiAdapter.IsPostAction(method))
                    httpMethod = "POST";
                else if (apiAdapter.IsPutAction(method))
                    httpMethod = "PUT";
                else if (apiAdapter.IsDeleteAction(method))
                    httpMethod = "DELETE";
                else
                    httpMethod = "GET";

                controllerOut.Append("\t\treturn helper.fetch");

                if (returnType != null)
                {
                    controllerOut.Append(isCollection ? "List" : "Single");
                    if (overridable || returnType == typeof (DateTime))
                    {
                        controllerOut.Append("T");
                    }
                    else
                    {
                        controllerOut.Append($"<{returnTypeName}>");
                    }
                }
                else
                {
                    controllerOut.Append("Void");
                }

                controllerOut.Append("('");
                var route = apiAdapter.GetRouteFormat(method);
                if (route.IndexOf("~/", StringComparison.Ordinal) == 0)
                    route = route.Substring(1);
                else
                    route = "/" + routePrefix + "/" + route;
                var routeString = Regex.Replace(route, @"{(\w+)(:.*?)?}",
                    match => "' + parameters." + match.Groups[1].Value + " + '");
                controllerOut.Append(routeString);
                controllerOut.Append("'");
                if (queryParameters != null)
                {
                    controllerOut.Append(" + helper.getQueryString({");
                    controllerOut.Append(queryParameters);
                    controllerOut.Append("})");
                }
                controllerOut.Append(", '" + httpMethod + "', ");
                if (returnType != null)
                {
                    if (overridable)
                    {
                        controllerOut.Append("factory, ");
                    }
                    else if (returnType == typeof (DateTime))
                    {
                        controllerOut.Append("(d:string) => new Date(d), ");
                    }
                }

                if (bodyParameter != null)
                {
                    var bodyType = bodyParameter.ParameterType;
                    if (CheckCollectionType(ref bodyType))
                    {
                        if (NeedNew(bodyType) && bodyType != typeof (DateTime))
                            controllerOut.Append("JSON.stringify(parameters." + bodyParameter.Name + ".map(v => v.toJs()))");
                        else
                            controllerOut.Append("JSON.stringify(parameters." + bodyParameter.Name + ")");
                    }
                    else
                    {
                        if (NeedNew(bodyType) && bodyType != typeof (DateTime))
                            controllerOut.Append("JSON.stringify(parameters." + bodyParameter.Name + ".toJs())");
                        else
                            controllerOut.Append("JSON.stringify(parameters." + bodyParameter.Name + ")");
                    }
                }
                else
                {
                    controllerOut.Append("null");
                }

                controllerOut.AppendLine(");");
                controllerOut.AppendLine("\t}");
                if (overridable)
                {
                    controllerOut.AppendLine();
                    controllerOut.Append(over);
                }
            }
            controllerOut.AppendLine("}" + Environment.NewLine);
            controllerOut.AppendLine("export var " + Camelize(type.Name.Replace("Controller", "")) + " = new " + type.Name + "();");
            controllerOut.AppendLine();
        }

        private List<Type> LoadControllers(IEnumerable<Assembly> assemblies)
        {
            var controllers = new List<Type>();
            foreach (var siteAssembly in assemblies)
            {
                foreach (var type in siteAssembly.ExportedTypes.Where(t => t.Name != "TypedController" && t.GetTypeInfo().IsClass && t.Name.EndsWith("Controller")))
                {
                    if (!apiAdapter.IsController(type))
                        continue;
                    controllers.Add(type);
                }
            }
            return controllers;
        }

        private void RegisterType(Type propertyType)
        {
            if (propertyType.Namespace == "System")
                return;

            if (propertyType.GetTypeInfo().IsEnum)
            {
                if (!enums.Contains(propertyType))
                    enums.Add(propertyType);
                return;
            }

            if (!views.ContainsKey(propertyType))
                views.Add(propertyType, false);
        }
        
        private static bool CheckCollectionType(ref Type returnType)
        {
            if (returnType == null) return false;
            bool isCollection = false;
            if (returnType.GetTypeInfo().IsGenericType)
            {
                var collectionType = returnType.GetGenericTypeDefinition();
                if (collectionType.GetTypeInfo().ImplementedInterfaces.Any(x => x.Name == "IEnumerable"))
                {
                    isCollection = true;
                    returnType = returnType.GenericTypeArguments[0];
                }
            }
            else if (returnType.IsArray)
            {
                isCollection = true;
                returnType = returnType.GetElementType();
            }
            return isCollection;
        }

        private static string Camelize(string name)
        {
            return name[0].ToString().ToLowerInvariant() + name.Substring(1);
        }

        private static bool NeedNew(Type type)
        {
            return !(type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double) || type == typeof(string) || type == typeof(bool) || type == typeof(decimal) || type == typeof(TimeSpan) || type == typeof(object)) && !type.GetTypeInfo().IsEnum;
        }

        private string GetTypeName(Type type, PropertyInfo propertyInfo)
        {
            if (type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(long) || type == typeof(decimal))
                return "number";
            if (type == typeof(string) || type == typeof(TimeSpan))
                return "string";
            if (type == typeof(DateTime))
                return "Date";
            if (type == typeof(bool))
                return "boolean";
            if (type == typeof (object))
            {
                if (propertyInfo != null)
                {
                    var returnTypes = apiAdapter.GetUnionTypes(propertyInfo).ToList();
                    if (returnTypes.Any())
                    {
                        foreach (var returnType in returnTypes)
                        {
                            RegisterType(returnType);
                        }
                        return string.Join("|", returnTypes.Select(x => GetTypeName(x, null)));
                    }
                }

                return "any";
            }

            if (!apiAdapter.IsObservableObject(type))
                return type.Name + "Data";
            return type.Name;
        }
    }
}

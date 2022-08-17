﻿using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Azure.Functions.Worker.Sdk.Generators
{
    /// <summary>
    /// Generates a class that implements IFunctionMetadataProvider and the method GetFunctionsMetadataAsync() which returns a list of IFunctionMetadata. 
    /// This source generator indexes a Function App and explicitly creates a list of DefaultFunctionMetadata (which implements IFunctionMetadata) from the functions defined
    /// in the user's compilation. This allows the worker to index functions at build time, rather than waiting for the process to start.
    /// </summary>
    [Generator]
    public class FunctionMetadataProviderGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            // retreive the populated receiver 
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            Compilation compilation = context.Compilation;

            SourceText sourceText;
            using (var stringWriter = new StringWriter())
            using (var indentedTextWriter = new IndentedTextWriter(stringWriter))
            {
                // set up usings
                indentedTextWriter.WriteLine("// <auto-generated/>");
                indentedTextWriter.WriteLine("using System;");
                indentedTextWriter.WriteLine("using System.Collections.Generic;");
                indentedTextWriter.WriteLine("using System.Collections.Immutable;");
                indentedTextWriter.WriteLine("using System.Text.Json;");
                indentedTextWriter.WriteLine("using System.Threading.Tasks;");
                indentedTextWriter.WriteLine("using Microsoft.Azure.Functions.Core;");
                indentedTextWriter.WriteLine("using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;");
                indentedTextWriter.WriteLine("using Microsoft.Extensions.DependencyInjection;");
                indentedTextWriter.WriteLine("using Microsoft.Extensions.Hosting;");

                // create namespace
                indentedTextWriter.WriteLine("namespace Microsoft.Azure.Functions.Worker");
                indentedTextWriter.WriteLine("{");
                indentedTextWriter.Indent++;

                // create class that implements IFunctionMetadataProvider
                indentedTextWriter.WriteLine("public class GeneratedFunctionMetadataProvider : IFunctionMetadataProvider");
                indentedTextWriter.WriteLine("{");
                indentedTextWriter.Indent++;

                WriteGetFunctionsMetadataAsyncMethod(indentedTextWriter, receiver, compilation);
                AddEnumTypes(indentedTextWriter);

                indentedTextWriter.Indent--;
                indentedTextWriter.WriteLine("}");

                // add method that users can call in startup to register the source-generated file
                AddRegistrationExtension(indentedTextWriter);

                indentedTextWriter.Indent--;
                indentedTextWriter.WriteLine("}");

                indentedTextWriter.Flush();
                sourceText = SourceText.From(stringWriter.ToString(), encoding: Encoding.UTF8);
            }
            // Add the source code to the compilation
            context.AddSource($"GeneratedFunctionMetadataProvider.g.cs", sourceText);
        }

        /// <summary>
        /// Register a factory that can create our custom syntax receiver
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        /// <summary>
        /// This method populates a dictionary with argumentName (keys) and argumentValue (value) pairs given attribMethodSymbol, which
        /// represents the attribute constructor as as an IMethodSymbol, and attributeData which stores constructor argument values.
        /// </summary>
        /// <param name="attribMethodSymbol">The attribute's constructor as an IMethodSymbol.</param>
        /// <param name="attributeData">Contains constructor arguments for the constructor represented in attribMethodSymbol.</param>
        /// <param name="dict">A dicitonary to be populated with constructor arguments.</param>
        /// <exception cref="InvalidOperationException"></exception>
        internal static void LoadConstructorArguments(IMethodSymbol attribMethodSymbol, AttributeData attributeData, IDictionary<string, object> dict)
        {
            if (attribMethodSymbol.Parameters.Length < attributeData.ConstructorArguments.Length)
            {
                throw new InvalidOperationException($"The constructor at '{nameof(attribMethodSymbol)}' has less total arguments than '{nameof(attributeData)}'.");
            }

            // It's fair to assume than constructor arguments appear before named arguments, and
            // that the constructor names would match the property names
            for (int i = 0; i < attributeData.ConstructorArguments.Length; i++)
            {
                var argumentName = attribMethodSymbol.Parameters[i].Name;
                var arg = attributeData.ConstructorArguments[i];

                switch (arg.Kind)
                {
                    case TypedConstantKind.Error:
                        break;
                    case TypedConstantKind.Primitive:
                        dict[argumentName] = arg.Value;
                        break;
                    case TypedConstantKind.Enum:
                        dict[argumentName] = $"((AuthorizationLevel){arg.Value}).ToString();";
                        break;
                    case TypedConstantKind.Type:
                        break;
                    case TypedConstantKind.Array:
                        var arrayValues = arg.Values.Select(a => a.Value!.ToString()).ToArray();
                        dict[argumentName] = arrayValues;
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Formats an object into a string value for the source-generated file. This can mean adding quotation marks around the string
        /// representation of the object, or leaving it as is if the object is a string or Enum type.
        /// </summary>
        /// <param name="propValue">The property that needs to be formmated into a string.</param>
        /// <returns></returns>
        internal static string FormatObject(object propValue)
        {
            if (propValue != null)
            {
                // catch values that are already strings or Enum parsing
                // we don't need to surround these cases with quotation marks
                if (propValue.ToString().Contains("\"") || propValue.ToString().Contains("Enum"))
                {
                    return propValue.ToString();
                }

                return "\"" + propValue.ToString() + "\"";
            }
            else
            {
                return "null";
            }
        }

        /// <summary>
        /// Format an array into a string.
        /// </summary>
        /// <param name="enumerableValues">An array object to be formatted.</param>
        /// <returns></returns>
        internal static string FormatArray(IEnumerable enumerableValues)
        {
            string arrAsString;

            arrAsString = "new List<string> { ";

            foreach (var o in enumerableValues)
            {
                arrAsString += FormatObject(o);
                arrAsString += ",";
            }

            arrAsString = arrAsString.TrimEnd(',', ' ');
            arrAsString += " }";

            return arrAsString;
        }

        /// <summary>
        /// Colllect all of the properties associated with an attribute.
        /// </summary>
        /// <param name="attribMethodSymbol">The symbol that represents the attribute constructor method.</param>
        /// <param name="attributeData">Contains the values associated with the attribute constructor method properties.</param>
        /// <returns></returns>
        internal static IDictionary<string, object> GetAttributeProperties(IMethodSymbol attribMethodSymbol, AttributeData attributeData)
        {
            Dictionary<string, object> argumentData = new();
            if (attributeData.ConstructorArguments.Any())
            {
                LoadConstructorArguments(attribMethodSymbol, attributeData, argumentData);
            }

            foreach (var namedArgument in attributeData.NamedArguments)
            {
                if (namedArgument.Value.Value != null)
                {
                    argumentData[namedArgument.Key] = namedArgument.Value.Value;
                }
            }

            return argumentData;
        }

        /// <summary>
        /// Created on demand before each generation pass
        /// </summary>
        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<MethodDeclarationSyntax> CandidateMethods { get; } = new List<MethodDeclarationSyntax>();

            /// <summary>
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
            /// </summary>
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is MethodDeclarationSyntax methodSyntax)
                {
                    if (methodSyntax.AttributeLists.Count > 0) // collect all methods with attributes - we will verify they are functions when we have access to symbols to get the full name
                    {
                        CandidateMethods.Add(methodSyntax); 
                    }
                }
            }
        }

        private static void WriteGetFunctionsMetadataAsyncMethod(IndentedTextWriter indentedTextWriter, SyntaxReceiver receiver, Compilation compilation)
        {
            indentedTextWriter.WriteLine("public Task<ImmutableArray<IFunctionMetadata>> GetFunctionMetadataAsync(string directory)");
            indentedTextWriter.WriteLine("{");
            indentedTextWriter.Indent++;

            // create list of IFunctionMetadata and populate it
            indentedTextWriter.WriteLine("var metadataList = new List<IFunctionMetadata>();");
            AddFunctionMetadataInfo(indentedTextWriter, receiver, compilation);
            indentedTextWriter.WriteLine("return Task.FromResult(metadataList.ToImmutableArray());");

            indentedTextWriter.Indent--;
            indentedTextWriter.WriteLine("}");
        }

        /// <summary>
        /// Checks that a candidate method has a Function attribute then proceeds to create a DefaultFunctionMetadata object.
        /// </summary>
        private static void AddFunctionMetadataInfo(IndentedTextWriter indentedTextWriter, SyntaxReceiver receiver, Compilation compilation)
        {
            var assemblyName = compilation.Assembly.Name;
            var scriptFile = Path.Combine(assemblyName + ".dll");

            // Loop through the candidate methods (methods with any attribute associated with them)
            foreach (MethodDeclarationSyntax method in receiver.CandidateMethods)
            {
                var model = compilation.GetSemanticModel(method.SyntaxTree);
                
                if(!IsMethodAFunction(model, method))
                {
                    continue;
                }

                var functionClass = (ClassDeclarationSyntax)method.Parent!;
                var functionName = functionClass.Identifier.ValueText;
                var entryPoint = assemblyName + "." + functionName + "." + method.Identifier.ValueText;
                var bindingsListName = functionName + "RawBindings";

                // collect Bindings
                indentedTextWriter.WriteLine($"var {bindingsListName} = new List<string>();");
                AddBindingInfo(indentedTextWriter, method, model, functionName);
                indentedTextWriter.WriteLine($"var {functionName} = new DefaultFunctionMetadata(Guid.NewGuid().ToString(), \"dotnet-isolated\", \"{functionName}\", \"{entryPoint}\", {functionName}RawBindings, \"{scriptFile}\");");
                indentedTextWriter.WriteLine($"metadataList.Add({functionName});");
            }
        }

        private static void AddBindingInfo(IndentedTextWriter indentedTextWriter, MethodDeclarationSyntax method, SemanticModel model, string functionName)
        {
            AddMethodOutputBinding(indentedTextWriter, method, model, functionName);
            AddParameterInputAndTriggerBindings(indentedTextWriter, method, model, functionName, out bool hasHttpTrigger);
            AddReturnTypeBindings(indentedTextWriter, method, model, functionName, hasHttpTrigger);
        }

        /// <summary>
        /// Only an Output Binding can be an attribute on a Function method. This method will check if there is one and return it.
        /// </summary>
        /// <exception cref="FormatException">Throws if attributes are formatted or used incorrectly.</exception>
        private static void AddMethodOutputBinding(IndentedTextWriter indentedTextWriter, MethodDeclarationSyntax method, SemanticModel model, string funcName)
        {
            var methodSymbol = model.GetDeclaredSymbol(method);
            var attributes = methodSymbol!.GetAttributes(); // methodSymbol is not null here because it's checked in IsMethodAFunction which is called before bindings are collected/created

            AttributeData? outputBinding = null;
            var foundOutputBinding = false;

            foreach (var attribute in attributes)
            {
                if (IsBindingAttribute(attribute))
                {
                    if (foundOutputBinding)
                    {
                        throw new FormatException($"Found multiple output attributes on method '{nameof(method)}'. Only one output binding attribute is is supported on a method.");
                    }

                    outputBinding = attribute;
                    foundOutputBinding = true;
                }
            }

            if (outputBinding != null)
            {
                WriteBindingToFile(indentedTextWriter, outputBinding, funcName, Constants.ReturnBindingName, null);
            }
        }

        /// <summary>
        /// This method adds the input/trigger bindings found in the parameters of the Function
        /// </summary>
        /// <exception cref="InvalidOperationException">Throws when a symbol cannot be found.</exception>
        /// <exception cref="FormatException">Throws if the attributes were used and formatted incorrectly.</exception>
        private static void AddParameterInputAndTriggerBindings(IndentedTextWriter indentedTextWriter, MethodDeclarationSyntax method, SemanticModel model, string funcName, out bool hasHttpTrigger)
        {
            hasHttpTrigger = false;

            foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
            {
                // If there's no attribute, we can assume that this parameter is not a binding
                if (parameter.AttributeLists.Count == 0)
                {
                    continue;
                }

                IParameterSymbol? parameterSymbol = model.GetDeclaredSymbol(parameter);

                if (parameterSymbol is null)
                {
                    throw new InvalidOperationException($"The symbol for the parameter '{nameof(parameter)}' could not be found");
                }

                // Check to see if any of the attributes associated with this parameter is a BindingAttribute
                foreach (var attribute in parameterSymbol.GetAttributes())
                {
                    if (IsBindingAttribute(attribute))
                    {
                        if(IsHttpTrigger(attribute))
                        {
                            hasHttpTrigger = true;
                        }

                        string? dataType = null;
                        string parameterSymbolType = parameterSymbol.Type.GetFullName();

                        // Check if parameter datatype is string or binary
                        // Is string parameter type
                        if (IsStringType(parameterSymbolType))
                        {
                            dataType = "\"String\"";
                        }
                        // Is binary parameter type
                        else if (IsBinaryType(parameterSymbolType))
                        {
                            dataType = "\"Binary\"";
                        }

                        string bindingName = parameter.Identifier.ValueText;

                        WriteBindingToFile(indentedTextWriter, attribute, funcName, bindingName, dataType);
                    }
                }
            }
        }

        /// <summary>
        /// Adds bindings found in the ReturnType class of the Function
        /// </summary>
        /// <exception cref="InvalidOperationException">Throws when a symbol cannot be found.</exception>
        /// <exception cref="FormatException">Throws if the attributes were used and formatted incorrectly.</exception>
        private static void AddReturnTypeBindings(IndentedTextWriter indentedTextWriter, MethodDeclarationSyntax method, SemanticModel model, string funcName, bool hasHttpTrigger)
        {
            TypeSyntax returnTypeSyntax = method.ReturnType;
            ITypeSymbol? returnTypeSymbol = model.GetSymbolInfo(returnTypeSyntax).Symbol as ITypeSymbol;
            var bindingsCount = 0;

            if (returnTypeSymbol is null)
            {
                throw new InvalidOperationException($"The symbol for the return type '{nameof(returnTypeSymbol)}' for the method '{nameof(method)}' could not be found");
            }

            if (!String.Equals(returnTypeSymbol.GetFullName(), Constants.VoidType, StringComparison.Ordinal))
            {
                if (String.Equals(returnTypeSymbol.GetFullName(), Constants.HttpResponseType, StringComparison.Ordinal))
                {
                    AddHttpReturnBinding(indentedTextWriter, funcName, Constants.ReturnBindingName);
                }
                else
                {
                    // Check all the members(properties) of this return type class to see if any of them have a binding attribute associated
                    var members = returnTypeSymbol.GetMembers();
                    var foundHttpOutput = false;

                    foreach (var m in members)
                    {
                        if (m.GetAttributes().Length > 0)
                        {
                            var foundOutputAttr = false;

                            foreach (var attr in m.GetAttributes())
                            {
                                if (IsBindingAttribute(attr))
                                {
                                    if (foundOutputAttr)
                                    {
                                        throw new FormatException($"Found multiple output attributes on property '{nameof(m)}' defined in the function return type '{nameof(returnTypeSymbol)}'. " +
                                            $"Only one output binding attribute is is supported on a property.");
                                    }

                                    // Check if this attribute is an HttpResponseData type attribute
                                    if (String.Equals(returnTypeSymbol.GetFullName(), Constants.HttpResponseType, StringComparison.Ordinal))
                                    {
                                        if (foundHttpOutput)
                                        {
                                            throw new FormatException($"Found multiple public properties with type '{Constants.HttpResponseType}' defined in output type '{nameof(returnTypeSymbol)}'. " +
                                                $"Only one HTTP response binding type is supported in your return type definition.");
                                        }

                                        foundHttpOutput = true;
                                        AddHttpReturnBinding(indentedTextWriter, funcName, m.Name);
                                    }
                                    else
                                    {
                                        IPropertySymbol? propertySymbol = m as IPropertySymbol;

                                        if (propertySymbol is null)
                                        {
                                            throw new InvalidOperationException($"The property '{nameof(propertySymbol)}' is invalid.");
                                        }

                                        string? dataType = null;
                                        var propertySymbolType = propertySymbol.Type.GetFullName();

                                        if (IsStringType(propertySymbolType))
                                        {
                                            dataType = "\"String\"";
                                        }
                                        // Is binary parameter type
                                        else if (IsBinaryType(propertySymbolType))
                                        {
                                            dataType = "\"Binary\"";
                                        }

                                        WriteBindingToFile(indentedTextWriter, attr, funcName, m.Name, dataType);
                                    }

                                    foundOutputAttr = true;
                                }
                            }

                        }
                    }

                    // No output bindings found in the return type.
                    if (bindingsCount == 0)
                    {
                        if (hasHttpTrigger)
                        {
                            AddHttpReturnBinding(indentedTextWriter, funcName, Constants.ReturnBindingName);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds HttpReturn binding.
        /// </summary>
        private static void AddHttpReturnBinding(IndentedTextWriter indentedTextWriter, string funcName, string bindingName)
        {
            indentedTextWriter.WriteLine($"var {funcName}{bindingName.Replace("$", "")}Binding = new {{");
            indentedTextWriter.Indent++;
            indentedTextWriter.WriteLine($"name = \"{bindingName}\",");
            indentedTextWriter.WriteLine("type = \"http\",");
            indentedTextWriter.WriteLine("direction = \"Out\",");
            indentedTextWriter.Indent--;
            indentedTextWriter.WriteLine("};");

            SerializeAnonymousBindingAsJsonString(indentedTextWriter, funcName, bindingName.Replace("$", ""));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="indentedTextWriter"></param>
        /// <param name="bindingAttrData"></param>
        /// <param name="funcName"></param>
        /// <param name="bindingName"></param>
        /// <param name="dataType"></param>
        /// <exception cref="InvalidOperationException">Throws when a symbol cannot be found.</exception>
        private static void WriteBindingToFile(IndentedTextWriter indentedTextWriter, AttributeData bindingAttrData, string funcName, string bindingName, string? dataType)
        {
            IMethodSymbol? attribMethodSymbol = bindingAttrData.AttributeConstructor;

            // Check if the attribute constructor has any parameters
            if (attribMethodSymbol is null || attribMethodSymbol?.Parameters is null)
            {
                throw new InvalidOperationException($"The constructor of attribute with syntax '{nameof(attribMethodSymbol)}' is invalid");
            }

            // Get binding info as a dictionary with keys as the property name and value as the property value
            IDictionary<string, object> attributeProperties = GetAttributeProperties(attribMethodSymbol, bindingAttrData);

            // Grab some required binding info properties
            string attributeName = bindingAttrData.AttributeClass!.Name;

            // properly format binding types by removing "Attribute" and "Input" descriptors
            string bindingType = attributeName.Replace("Attribute", "");
            bindingType = bindingType.Replace("Input", "");

            // Set binding direction
            string bindingDirection = "In";
            if (IsOutputBindingAttribute(bindingAttrData))
            {
                bindingDirection = "Out";
            }

            // Create raw binding anonymous type, example:
            /*  var binding1 = new {
                name = "req",
                type = "HttpTrigger",
                direction = "In",
                authLevel = Enum.GetName(typeof(AuthorizationLevel),0),
                methods = new List<string> { "get","post" },
            };*/
            CreateBindingInfo(indentedTextWriter, funcName, bindingName, bindingType, bindingDirection, attributeProperties, dataType);
        }

        /// <summary>
        /// Writes binding info to the generated file. This method takes care of all bindings except for auto-added http return types.
        /// </summary>
        private static void CreateBindingInfo(IndentedTextWriter indentedTextWriter, string functionName, string bindingName, string bindingType, string bindingDirection, IDictionary<string, object> attributeProperties, string? dataType)
        {
            // Create raw binding anonymous type, example:
            /*  var binding1 = new {
                name = "req",
                type = "HttpTrigger",
                direction = "In",
                authLevel = Enum.GetName(typeof(AuthorizationLevel),0),
                methods = new List<string> { "get","post" },
            };*/
            indentedTextWriter.WriteLine($"var {functionName}{bindingName}Binding = new {{");
            indentedTextWriter.Indent++;
            indentedTextWriter.WriteLine($"name = \"{bindingName}\",");
            indentedTextWriter.WriteLine($"type = \"{bindingType}\",");
            indentedTextWriter.WriteLine($"direction = \"{bindingDirection}\",");

            // Add additional bindingInfo to the anonymous type because some functions have more properties than others
            // TODO: See how to handle isBatched property here? This seems like the best place for it.
            foreach (var prop in attributeProperties)
            {
                var propertyName = prop.Key;

                if (prop.Value.GetType().IsArray)
                {
                    string arr = FormatArray((IEnumerable)prop.Value);
                    indentedTextWriter.WriteLine($"{propertyName} = {arr},");
                }
                else
                {
                    var propertyValue = FormatObject(prop.Value);
                    indentedTextWriter.WriteLine($"{propertyName} = {propertyValue},");
                }
            }

            // add dataType property if it is passed through (value is not null)
            if (dataType != null)
            {
                indentedTextWriter.WriteLine($"dataType = {dataType},");
            }    

            indentedTextWriter.Indent--;
            indentedTextWriter.WriteLine("};");

            // Take the anonymous type representing the binding and serialize it as a JSON string
            SerializeAnonymousBindingAsJsonString(indentedTextWriter, functionName, bindingName);
        }

        private static void SerializeAnonymousBindingAsJsonString(IndentedTextWriter indentedTextWriter, string functionName, string bindingName)
        {
            indentedTextWriter.WriteLine($"var {functionName}{bindingName}BindingJSONstring = JsonSerializer.Serialize({functionName}{bindingName}Binding);");
            indentedTextWriter.WriteLine($"{functionName}RawBindings.Add({functionName}{bindingName}BindingJSONstring);");
        }

        /// <summary>
        /// Auto-add an enum type used to parse enum values found in user code.
        /// </summary>
        /// <param name="indentedTextWriter"></param>
        private static void AddEnumTypes(IndentedTextWriter indentedTextWriter)
        {
            indentedTextWriter.WriteLine("public enum AuthorizationLevel");
            indentedTextWriter.WriteLine("{");
            indentedTextWriter.Indent++;
            indentedTextWriter.WriteLine("Anonymous,");
            indentedTextWriter.WriteLine("User,");
            indentedTextWriter.WriteLine("Function,");
            indentedTextWriter.WriteLine("System,");
            indentedTextWriter.WriteLine("Admin");
            indentedTextWriter.Indent--;
            indentedTextWriter.WriteLine("}");
        }

        /// <summary>
        /// Adds a generated registration extension that users can call to register the source-generated function metadata provider.
        /// </summary>
        private static void AddRegistrationExtension(IndentedTextWriter indentedTextWriter)
        {
            indentedTextWriter.WriteLine("public static class WorkerHostBuilderFunctionMetadataProviderExtension");
            indentedTextWriter.WriteLine("{");
            indentedTextWriter.Indent++;
            indentedTextWriter.WriteLine("public static IHostBuilder ConfigureGeneratedFunctionMetadataProvider(this IHostBuilder builder)");
            indentedTextWriter.WriteLine("{");
            indentedTextWriter.Indent++;
            indentedTextWriter.WriteLine("builder.ConfigureServices(s => ");
            indentedTextWriter.WriteLine("{");
            indentedTextWriter.Indent++;
            indentedTextWriter.WriteLine("s.AddSingleton<IFunctionMetadataProvider, GeneratedFunctionMetadataProvider>();");
            indentedTextWriter.Indent--;
            indentedTextWriter.WriteLine("});");
            indentedTextWriter.WriteLine("return builder;");
            indentedTextWriter.Indent--;
            indentedTextWriter.WriteLine("}");
            indentedTextWriter.Indent--;
            indentedTextWriter.WriteLine("}");
        }

        /// <summary>
        /// Checks if a method is a function by using its full name.
        /// </summary>
        private static bool IsMethodAFunction(SemanticModel model, MethodDeclarationSyntax method)
        {
            var methodSymbol = model.GetDeclaredSymbol(method);

            if (methodSymbol is null)
            {
                throw new InvalidOperationException($"The symbol for the parameter '{nameof(method)}' could not be found");
            }

            foreach (var attr in methodSymbol.GetAttributes())
            {
                if (attr.AttributeClass != null &&
                   String.Equals(attr.AttributeClass.GetFullName(), Constants.FunctionNameType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether an attribute is a Function Binding or not. This method assumes that any Function Binding related attributes passed in are
        /// of the highest level (specific bindings like QueueTrigger, Queue Input Binding, etc).
        /// </summary>
        /// <param name="attribute">The exact attribute decorating a method/parameter/property declaration.</param>
        private static bool IsBindingAttribute(AttributeData attribute)
        {
            if (attribute.AttributeClass != null && // this class is the exact type of binding/trigger (QueueTrigger, Queue input binding, etc)
                attribute.AttributeClass.BaseType != null && // this base type tells you if something is input or output binding
                attribute.AttributeClass.BaseType.BaseType != null) // this base type is the binding attribute type
            {
                return String.Equals(attribute.AttributeClass.BaseType.BaseType.GetFullName(), Constants.BindingAttributeType);
            }

            return false;
        }

        /// <summary>
        /// Determines whether an attribute is a Function OutputBinding or not. This method assumes that any Function Binding related attributes passed in are
        /// of the highest level (specific bindings like QueueTrigger, Queue Input Binding, etc).
        /// </summary>
        /// <param name="attribute">The exact attribute decorating a method/parameter/property declaration.</param>
        private static bool IsOutputBindingAttribute(AttributeData attribute)
        {
            if (attribute.AttributeClass != null &&
                attribute.AttributeClass.BaseType != null)
            {
                return String.Equals(attribute.AttributeClass.BaseType.GetFullName(), Constants.OutputBindingAttributeType);
            }

            return false;
        }

        /// <summary>
        /// Determines whether an attribute is an HttpTrigger or not. This method assumes that any Function Binding related attributes passed in are
        /// of the highest level (specific bindings like QueueTrigger, Queue Input Binding, etc).
        /// </summary>
        /// <param name="attribute">The exact attribute decorating a method/parameter/property declaration.</param>
        private static bool IsHttpTrigger(AttributeData attribute)
        {
            if (attribute.AttributeClass != null)
            {
                return String.Equals(attribute.AttributeClass.GetFullName(), Constants.HttpTriggerBindingType);
            }

            return false;
        }

        private static bool IsStringType(string fullName)
        {
            return String.Equals(fullName, Constants.StringType, StringComparison.Ordinal);
        }

        private static bool IsBinaryType(string fullName)
        {
            return String.Equals(fullName, Constants.ByteArrayType, StringComparison.Ordinal)
                || String.Equals(fullName, Constants.ReadOnlyMemoryOfBytes, StringComparison.Ordinal);
        }
    }
}

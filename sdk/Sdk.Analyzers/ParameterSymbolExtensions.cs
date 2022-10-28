// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Security;
using System.Reflection;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Azure.Functions.Worker.Sdk.Analyzers
{
    internal static class ParameterSymbolExtensions
    {
        public static AttributeData GetWebJobsAttribute(this IParameterSymbol parameter)
        {
            var parameterAttributes = parameter.GetAttributes();

            foreach (var parameterAttribute in parameterAttributes)
            {
                var attributeAttributes = parameterAttribute.AttributeClass.GetAttributes();

                foreach (var attribute in attributeAttributes)
                {
                    if (string.Equals(attribute.AttributeClass.ToDisplayString(), Constants.Types.WebJobsBindingAttribute, StringComparison.Ordinal))
                    {
                        return parameterAttribute;
                    }
                }
            }

            return null;
        }

        public static AttributeData GetInvalidAttribute(this IParameterSymbol parameter)
        {
            // This code will be removed. It's only for illustration on how to examine value of a parameter
            // which will be needed to analyzer value of Types field

            var parameterAttributes = parameter.GetAttributes();

            foreach (var parameterAttribute in parameterAttributes)
            {
                var allInterfaces = parameterAttribute.AttributeClass.AllInterfaces;
                // method GetType() or GetMembers() or parameterAttribute.AttributeClass.BaseType;

                foreach (var interfaceName in allInterfaces)
                {
                    if (interfaceName.Name == "ITypesProvider")
                    {
                        var types = typeof(ITypesProvider).InvokeMember("GetTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, null, null);
                    }
                }

                var b = parameterAttribute.ConstructorArguments;
                foreach (var attribute in b)
                {
                    if (attribute.Type.ToString() == "Microsoft.Azure.Functions.Worker.HttpTriggerAttribute")
                    {
                        if (attribute.Value.ToString() == "5")
                        {
                            return parameterAttribute;
                        }
                    }
                }
            }

            return null;
        }

    }
}

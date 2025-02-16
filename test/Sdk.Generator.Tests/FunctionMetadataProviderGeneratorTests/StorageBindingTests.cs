﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Sdk.Generators;
using Xunit;

namespace Microsoft.Azure.Functions.SdkGeneratorTests
{
    public partial class FunctionMetadataProviderGeneratorTests
    {
        public class StorageBindingTests
        {
            private Assembly[] referencedExtensionAssemblies;

            public StorageBindingTests()
            {
                // load all extensions used in tests (match extensions tested on E2E app? Or include ALL extensions?)
                var abstractionsExtension = Assembly.LoadFrom("Microsoft.Azure.Functions.Worker.Extensions.Abstractions.dll");
                var httpExtension = Assembly.LoadFrom("Microsoft.Azure.Functions.Worker.Extensions.Http.dll");
                var storageExtension = Assembly.LoadFrom("Microsoft.Azure.Functions.Worker.Extensions.Storage.dll");            
                var queueExtension = Assembly.LoadFrom("Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues.dll");
                var hostingExtension = Assembly.LoadFrom("Microsoft.Extensions.Hosting.dll");
                var diExtension = Assembly.LoadFrom("Microsoft.Extensions.DependencyInjection.dll");
                var hostingAbExtension = Assembly.LoadFrom("Microsoft.Extensions.Hosting.Abstractions.dll");
                var diAbExtension = Assembly.LoadFrom("Microsoft.Extensions.DependencyInjection.Abstractions.dll");
                var blobExtension = Assembly.LoadFrom("Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs.dll");

                referencedExtensionAssemblies = new[]
                {
                    abstractionsExtension,
                    blobExtension,
                    httpExtension,
                    storageExtension,
                    queueExtension,
                    hostingExtension,
                    hostingAbExtension,
                    diExtension,
                    diAbExtension
                };
            }

            [Fact]
            public async void TestQueueTriggerAndOutput()
            {
                string inputCode = @"
                using System.Collections.Generic;
                using System.Linq;
                using System.Net;
                using System.Text.Json.Serialization;
                using Microsoft.Azure.Functions.Worker;
                using Microsoft.Azure.Functions.Worker.Http;

                namespace FunctionApp
                {
                    public class QueueTriggerAndOutput
                    {
                        [Function('QueueTriggerFunction')]
                        [QueueOutput('test-output-dotnet-isolated')]
                        public string QueueTriggerAndOutputFunction([QueueTrigger('test-input-dotnet-isolated')] string message, FunctionContext context)
                        {
                            return message;
                        }
                    }
                }".Replace("'", "\"");

                string expectedGeneratedFileName = $"GeneratedFunctionMetadataProvider.g.cs";
                string expectedOutput = @"// <auto-generated/>
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Core;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace Microsoft.Azure.Functions.Worker
{
    public class GeneratedFunctionMetadataProvider : IFunctionMetadataProvider
    {
        public Task<ImmutableArray<IFunctionMetadata>> GetFunctionMetadataAsync(string directory)
        {
            var metadataList = new List<IFunctionMetadata>();
            var Function0RawBindings = new List<string>();
            var Function0binding0 = new {
                Name = '$return',
                Type = 'Queue',
                Direction = 'Out',
                QueueName = 'test-output-dotnet-isolated',
            };
            var Function0binding0JSON = JsonSerializer.Serialize(Function0binding0);
            Function0RawBindings.Add(Function0binding0JSON);
            var Function0binding1 = new {
                Name = 'message',
                Type = 'QueueTrigger',
                Direction = 'In',
                QueueName = 'test-input-dotnet-isolated',
                DataType = 'String',
            };
            var Function0binding1JSON = JsonSerializer.Serialize(Function0binding1);
            Function0RawBindings.Add(Function0binding1JSON);
            var Function0 = new DefaultFunctionMetadata
            {
                Language = 'dotnet-isolated',
                Name = 'QueueTriggerFunction',
                EntryPoint = 'TestProject.QueueTriggerAndOutput.QueueTriggerAndOutputFunction',
                RawBindings = Function0RawBindings,
                ScriptFile = 'TestProject.dll'
            };
            metadataList.Add(Function0);
            return Task.FromResult(metadataList.ToImmutableArray());
        }
    }
    public static class WorkerHostBuilderFunctionMetadataProviderExtension
    {
        ///<summary>
        /// Adds the GeneratedFunctionMetadataProvider to the service collection.
        /// During initialization, the worker will return generated function metadata instead of relying on the Azure Functions host for function indexing.
        ///</summary>
        public static IHostBuilder ConfigureGeneratedFunctionMetadataProvider(this IHostBuilder builder)
        {
            builder.ConfigureServices(s => 
            {
                s.AddSingleton<IFunctionMetadataProvider, GeneratedFunctionMetadataProvider>();
            });
            return builder;
        }
    }
}
".Replace("'", "\"");

                await TestHelpers.RunTestAsync<FunctionMetadataProviderGenerator>(
                    referencedExtensionAssemblies,
                    inputCode,
                    expectedGeneratedFileName,
                    expectedOutput);
            }

            [Fact]
            public async void TestBlobAndQueueInputsAndOutputs()
            {
                string inputCode = @"
                using System;
                using System.Collections.Generic;
                using System.Linq;
                using System.Net;
                using System.Text.Json.Serialization;
                using Microsoft.Azure.Functions.Worker;
                using Microsoft.Azure.Functions.Worker.Http;

                namespace FunctionApp
                {
                    public class QueueTriggerAndOutput
                    {
                        [Function('QueueToBlobFunction')]
                        [BlobOutput('container1/hello.txt', Connection = 'MyOtherConnection')]
                        public string QueueToBlob(
                        [QueueTrigger('queueName', Connection = 'MyConnection')] string queuePayload)
                        {
                            throw new NotImplementedException();
                        }

                        [Function('BlobToQueueFunction')]
                        [QueueOutput('queue2')]
                        public object BlobToQueue(
                            [BlobTrigger('container2/%file%')] string blob)

                        {
                            throw new NotImplementedException();
                        }
                    }
                }".Replace("'", "\"");

                string expectedGeneratedFileName = $"GeneratedFunctionMetadataProvider.g.cs";
                string expectedOutput = @"// <auto-generated/>
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Core;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace Microsoft.Azure.Functions.Worker
{
    public class GeneratedFunctionMetadataProvider : IFunctionMetadataProvider
    {
        public Task<ImmutableArray<IFunctionMetadata>> GetFunctionMetadataAsync(string directory)
        {
            var metadataList = new List<IFunctionMetadata>();
            var Function0RawBindings = new List<string>();
            var Function0binding0 = new {
                Name = '$return',
                Type = 'Blob',
                Direction = 'Out',
                BlobPath = 'container1/hello.txt',
                Connection = 'MyOtherConnection',
            };
            var Function0binding0JSON = JsonSerializer.Serialize(Function0binding0);
            Function0RawBindings.Add(Function0binding0JSON);
            var Function0binding1 = new {
                Name = 'queuePayload',
                Type = 'QueueTrigger',
                Direction = 'In',
                QueueName = 'queueName',
                Connection = 'MyConnection',
                DataType = 'String',
            };
            var Function0binding1JSON = JsonSerializer.Serialize(Function0binding1);
            Function0RawBindings.Add(Function0binding1JSON);
            var Function0 = new DefaultFunctionMetadata
            {
                Language = 'dotnet-isolated',
                Name = 'QueueToBlobFunction',
                EntryPoint = 'TestProject.QueueTriggerAndOutput.QueueToBlob',
                RawBindings = Function0RawBindings,
                ScriptFile = 'TestProject.dll'
            };
            metadataList.Add(Function0);
            var Function1RawBindings = new List<string>();
            var Function1binding0 = new {
                Name = '$return',
                Type = 'Queue',
                Direction = 'Out',
                QueueName = 'queue2',
            };
            var Function1binding0JSON = JsonSerializer.Serialize(Function1binding0);
            Function1RawBindings.Add(Function1binding0JSON);
            var Function1binding1 = new {
                Name = 'blob',
                Type = 'BlobTrigger',
                Direction = 'In',
                Path = 'container2/%file%',
                DataType = 'String',
            };
            var Function1binding1JSON = JsonSerializer.Serialize(Function1binding1);
            Function1RawBindings.Add(Function1binding1JSON);
            var Function1 = new DefaultFunctionMetadata
            {
                Language = 'dotnet-isolated',
                Name = 'BlobToQueueFunction',
                EntryPoint = 'TestProject.QueueTriggerAndOutput.BlobToQueue',
                RawBindings = Function1RawBindings,
                ScriptFile = 'TestProject.dll'
            };
            metadataList.Add(Function1);
            return Task.FromResult(metadataList.ToImmutableArray());
        }
    }
    public static class WorkerHostBuilderFunctionMetadataProviderExtension
    {
        ///<summary>
        /// Adds the GeneratedFunctionMetadataProvider to the service collection.
        /// During initialization, the worker will return generated function metadata instead of relying on the Azure Functions host for function indexing.
        ///</summary>
        public static IHostBuilder ConfigureGeneratedFunctionMetadataProvider(this IHostBuilder builder)
        {
            builder.ConfigureServices(s => 
            {
                s.AddSingleton<IFunctionMetadataProvider, GeneratedFunctionMetadataProvider>();
            });
            return builder;
        }
    }
}
".Replace("'", "\"");

                await TestHelpers.RunTestAsync<FunctionMetadataProviderGenerator>(
                    referencedExtensionAssemblies,
                    inputCode,
                    expectedGeneratedFileName,
                    expectedOutput);
            }
        }
    }
}

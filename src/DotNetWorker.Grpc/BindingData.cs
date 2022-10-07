// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Functions.Worker.Core;

namespace Microsoft.Azure.Functions.Worker.Grpc.Messages
{
    internal sealed partial class BindingData : IBindingData
    {
        string IBindingData.Version => Version;

        string IBindingData.Source => Source;

        string IBindingData.ContentType => ContentType;

        BinaryData IBindingData.Content => BinaryData.FromBytes(Content.ToByteArray());
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.Functions.Worker.Core
{
    /// <summary>
    /// A representation of a Microsoft.Azure.WebJobs.ParameterBindingData
    /// </summary>
    public interface IBindingData
    {
        /// <summary>
        /// Gets the version of ParameterBindingData schema
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the extension source of the event i.e CosmosDB, BlobStorage
        /// </summary>
        string Source { get; }

        /// <summary>
        /// Gets the content type of the content data
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// Gets the event content as <see cref="BinaryData"/>. Using BinaryData, one can deserialize
        /// the payload into rich data, or access the raw JSON data using <see cref="BinaryData.ToString()"/>.
        /// </summary>
        BinaryData Content { get; }
    }
}

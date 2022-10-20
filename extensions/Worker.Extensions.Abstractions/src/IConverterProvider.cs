// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.Worker.Extensions.Abstractions
{
    /// <summary>
    /// Provides information about what converters to be used.
    /// </summary>
    public interface IConverterProvider
    {
        /// <summary>
        /// Gets an <see cref="IEnumerable{T}"/> of <see cref="Type"/> instances representing the converters to be used.
        /// </summary>
        public IList<Type> ConverterTypes { get; }
    }
}

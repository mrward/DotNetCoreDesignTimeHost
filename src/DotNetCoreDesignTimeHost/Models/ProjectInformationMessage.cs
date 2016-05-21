// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectModel.Server.Models
{
    public class ProjectInformationMessage
    {
        public string Name { get; set; }

        public IReadOnlyList<FrameworkData> Frameworks { get; set; }

        public IReadOnlyList<string> Configurations { get; set; }

        public IDictionary<string, string> Commands { get; set; }

        public IReadOnlyList<string> ProjectSearchPaths { get; set; }

        public string GlobalJsonPath { get; set; }
    }
}
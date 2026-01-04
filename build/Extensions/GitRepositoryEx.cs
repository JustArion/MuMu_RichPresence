using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nuke.Common.Git;

namespace Extensions;

[SuppressMessage("ReSharper", "InvokeAsExtensionMember")]
static class GitRepositoryEx
{
    extension(GitRepository)
    {
        // Reversed since the first tag should be the latest
        public static ICollection<string> GetTags() => Git("tag", logOutput: false).Select(x => x.Text).Reverse().ToArray();
        
        public static string GetTag(Func<string, bool> predicate) => GetTags().FirstOrDefault(predicate);
    }
}
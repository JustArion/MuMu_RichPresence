using System.Diagnostics.CodeAnalysis;

namespace Extensions;

[SuppressMessage("ReSharper", "InvokeAsExtensionMember")]
[SuppressMessage("ReSharper", "InvokeAsExtensionMemberFromSameClass")]
static class GitRepositoryEx
{
    extension(GitRepository)
    {
        // Reversed since the first tag should be the latest
        public static ICollection<string> GetTags() => [.. Git("tag", logOutput: false).Select(x => x.Text).Reverse()];
        
        public static string? GetTag(Func<string, bool> predicate) => GetTags().FirstOrDefault(predicate);
    }
}
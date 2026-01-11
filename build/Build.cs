using Extensions;
using JetBrains.Annotations;

[
    GitHubActions("Run Tests", GitHubActionsImage.WindowsLatest, InvokedTargets = [nameof(Test)],        
        On = [GitHubActionsTrigger.WorkflowDispatch],
        CacheIncludePatterns = ["~/.nuget/packages"],
        CacheKeyFiles = ["**/global.json", "**/*.csproj", "**/Directory.Packages.props", "**/packages.lock.json"],
        Lfs = true),
    GitHubActions("CI Build", GitHubActionsImage.WindowsLatest, InvokedTargets = [nameof(Velopack)], PublishArtifacts = true,
        Submodules = GitHubActionsSubmodules.Recursive,
        CacheIncludePatterns = ["~/.nuget/packages"],
        CacheKeyFiles = ["**/global.json", "**/*.csproj", "**/Directory.Packages.props", "**/packages.lock.json"], 
        OnWorkflowDispatchOptionalInputs = ["Version"]),
    GitHubActions("Release on Tag", 
        GitHubActionsImage.WindowsLatest,
        InvokedTargets = [nameof(TaggedRelease)],
        EnableGitHubToken = true,
        PublishArtifacts = true,
        WritePermissions = [GitHubActionsPermissions.Contents],
        Submodules = GitHubActionsSubmodules.Recursive,
        CacheIncludePatterns = ["~/.nuget/packages"],
        CacheKeyFiles = ["**/global.json", "**/*.csproj", "**/Directory.Packages.props", "**/packages.lock.json"],
        Lfs = true,
        
        OnPushTags = ["v*"]),
    GitHubActions("Manual Release", 
        GitHubActionsImage.WindowsLatest, 
        InvokedTargets = [nameof(TaggedRelease)],
        EnableGitHubToken = true,
        PublishArtifacts = true,
        WritePermissions = [GitHubActionsPermissions.Contents],
        Submodules = GitHubActionsSubmodules.Recursive,
        CacheIncludePatterns = ["~/.nuget/packages"],
        CacheKeyFiles = ["**/global.json", "**/*.csproj", "**/Directory.Packages.props", "**/packages.lock.json"],
        Lfs = true,
        
        OnWorkflowDispatchRequiredInputs = ["Version"])
]
class Build : NukeBuild, ICreateGitHubRelease, IHazArtifacts
{
    public static int Main ()
    {
        return Execute<Build>(x => x.Velopack);
    }
    
    Target TaggedRelease => _ => _
        .DependsOn(Velopack)
        .DependsOn(Test)
        .Unlisted()
        .OnlyWhenDynamic(()=> !string.IsNullOrWhiteSpace(Actions.Token))
        .OnlyWhenStatic(() => IsServerBuild)
        .Executes(async () =>
        {
            // https://github.com/nuke-build/nuke/blob/develop/source/Nuke.Components/ICreateGitHubRelease.cs#L35
            GitHubTasks.GitHubClient.Credentials = new(Actions.Token);

            var tag = Version ?? GetLatestTag();
            var releases = GitHubTasks.GitHubClient.Repository.Release;
            var release = await GetOrCreateRelease(tag);

            var uploadTasks = AssetFiles.Select(async x =>
            {
                await using var assetFile = File.OpenRead(x);
                var asset = new ReleaseAssetUpload
                {
                    FileName = x.Name,
                    ContentType = "application/octet-stream",
                    RawData = assetFile
                };

                await releases.UploadAsset(release, asset);
            }).ToArray();
            
            Task.WaitAll(uploadTasks);
            
            Log.Information("All Assets uploaded!");

            var unreleasedNotes = ReadChangelog(ChangelogPath).Unreleased;
            var noNewChanges = unreleasedNotes?.EndIndex <= unreleasedNotes?.StartIndex;
            if (unreleasedNotes == null || noNewChanges)
                return;
            
            // Moves the changes in Unreleased to the latest tag
            FinalizeChangelog(ChangelogPath, tag, Repository);
            
            var defaultBranch = Git("remote show origin")
                .FirstOrDefault(x => x.Text.Trim().StartsWith("HEAD branch:"))
                .Text.Split(':')[1]
                .Trim();
            
            Git($"config --global user.name {Quote("github-actions[bot]")}");
            Git($"config --global user.email {Quote("github-actions[bot]@users.noreply.github.com")}");
            
            Git($"add {ChangelogPath}");
            Git($"commit -m {Quote($"chore: {Path.GetFileName(ChangelogPath)} for {tag}")}");
            Git($"push origin HEAD:{defaultBranch}");
        });

    private async Task<Release> GetOrCreateRelease(string name)
    {
        var release = GitHubTasks.GitHubClient.Repository.Release;
        try
        {
            return await release.Create(
                Repository.GetGitHubOwner(),
                Repository.GetGitHubName(),
                new NewRelease(name)
                {
                    Name = name,
                    Prerelease = false,
                    Draft = false,
                    Body = $"""
                           **Dependencies**
                           - [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.0-windows-x64-installer)
                           
                           **Changelog**
                           {ExtractChangelogSectionNotes(ChangelogPath).JoinNewLine()}
                           """
                });
        }
        catch (Exception e1)
        {
            try
            {
                return await release.Get(
                    Repository.GetGitHubOwner(),
                    Repository.GetGitHubName(),
                    name);
            }
            catch
            {
                throw e1;
            }
        }
    }

    Target Velopack => _ => _
        .DependsOn(InstallOrUpdateVelopack)
        .DependsOn(Standalone)
        .Produces(VelopackDirectory)
        .Executes(() =>
        {
            StartProcess("vpk", $"pack " +
                                       $"--packId MuMu-RichPresence " +
                                       $"-v {Version ?? GetLatestTag()} " +
                                       $"--outputDir velopack " +
                                       $"--mainExe {Quote("MuMu RichPresence Standalone.exe")} " +
                                       $"--packDir bin " +
                                       $"--framework {VelopackDotnetFrameworkVersion} " +
                                       $"--packTitle {Quote("MuMu - Rich Presence")} " +
                                       $"--shortcuts {Quote("StartMenuRoot")} " +
                                       $"-y",
                    workingDirectory: RootDirectory)
                .AssertZeroExitCode();
            
            Log.Information("Successfully built to {Directory}", VelopackDirectory);
        });

    Target Test => _ => _
        .DependsOn(Restore)
        .Executes(() => 
            DotNetTest(options => options
                .SetProjectFile(Solution.src.MuMu_RichPresence_Tests)));

    Target Standalone => _ => _
        .DependsOn(Restore)
        .Produces(StandaloneDirectory)
        .Executes(() => 
            DotNetPublish(options => options
            .SetProject(Solution.src.MuMu_RichPresence)
            .SetRuntime(DotNetRuntimeIdentifier.win_x64)
            .SetProperty("Version", Version ?? GetLatestTag())
            .SetOutput(StandaloneDirectory)));

    Target InstallOrUpdateVelopack => _ => _
        .Executes(() => 
            DotNetToolUpdate(options => options
            .EnableGlobal()
            .SetPackageName("vpk")));
    
    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("*/bin", "*/obj").DeleteDirectories();
            StandaloneDirectory.CreateOrCleanDirectory();
            VelopackDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            // Riot Tray Context Theme
            Git("submodule update --init --remote --recursive");

            return DotNetRestore(options => options
                .SetProcessWorkingDirectory(RootDirectory));
        });

    // Parameters
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    [Optional, Parameter, CanBeNull] string Version;
    [Optional, Parameter] string VelopackDotnetFrameworkVersion = "net10-x64-desktop";
    
    // Injected
    
    [GitRepository]
    GitRepository Repository;
    
    [Solution(GenerateProjects = true)] 
    readonly Solution Solution;
    
    //

    private GitHubActions Actions => GitHubActions.Instance;
    public string Name => Version ?? GetLatestTag();
    public IEnumerable<AbsolutePath> AssetFiles => StandaloneFiles.Concat(VelopackFiles);
    
    // Paths
    readonly AbsolutePath ChangelogPath = RootDirectory / "CHANGELOG.md";
    readonly AbsolutePath SourceDirectory = RootDirectory / "src";
    readonly AbsolutePath StandaloneDirectory = RootDirectory / "bin";
    readonly AbsolutePath VelopackDirectory = RootDirectory / "velopack";
    
    // MuMU Rich Presence - Standalone.exe
    private IEnumerable<AbsolutePath> StandaloneFiles => StandaloneDirectory.GlobFiles("*.exe");
    
    private IEnumerable<AbsolutePath> VelopackFiles => VelopackDirectory.GlobFiles(
        "*.nupkg", // MuMu-RichPresence-<version>-full.nupkg
            "*.zip", // MuMu-RichPresence-win-Portable.zip
            "*.exe", // MuMu-RichPresence-win-Setup.exe
            "releases.win.json");
    
    // Extra Methods
    private static readonly Func<string, bool> _versionPredicate = s => s.StartsWith('v'); 
    private string GetLatestTag() => (Repository.Tags?.FirstOrDefault(_versionPredicate) ?? GitRepository.GetTag(_versionPredicate)).TrimStart('v');
    private static string Quote(string str) => $"\"{str}\"";
}
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");
var baseDirectory = MakeAbsolute(Directory("."));
var buildDirectory = baseDirectory + "/.build";
var outputDirectory = buildDirectory + "/output";
var version = "0.0.0";

var msbuildSettings = new MSBuildSettings {
    ArgumentCustomization = args => args.Append("/consoleloggerparameters:ErrorsOnly"),
    Configuration = configuration,
    ToolVersion = MSBuildToolVersion.VS2019,
    NodeReuse = false,
    WarningsAsError = true
}.WithProperty("OutDir", outputDirectory);

///////////////////////////////////////////////////////////////////////////////

Setup(setupContext =>
{
    if (setupContext.TargetTask.Name == "Package")
    {
        Information("Switching to Release Configuration for packaging...");
        configuration = "Release";

        msbuildSettings.Configuration = "Release";
    }

    IEnumerable<string> stdout;
    StartProcess ("git", new ProcessSettings {
        Arguments = "describe --tags --abbrev=0",
        RedirectStandardOutput = true,
    }, out stdout);
    List<String> result = new List<string>(stdout);
    version = String.IsNullOrEmpty(result[0]) ? "0.0.0" : result[0];

    StartProcess ("git", new ProcessSettings {
        Arguments = "rev-parse --short=8 HEAD",
        RedirectStandardOutput = true,
    }, out stdout);
    result = new List<string>(stdout);
    var packageId = String.IsNullOrEmpty(result[0]) ? "unknown" : result[0];

    var branch = "unknown";
    StartProcess ("git", new ProcessSettings {
        Arguments = "symbolic-ref --short HEAD",
        RedirectStandardOutput = true,
    }, out stdout);
    result = new List<string>(stdout);
    branch = String.IsNullOrEmpty(result[0]) ? "unknown" : result[0];

    version = $"{version}-{branch}.{packageId}";

    Information($"Package ID is '{packageId}' on branch '{branch}'");
});

Task("Default")
    .IsDependentOn("Compile");

Task("Clean")
    .Does(() =>
    {
        CleanDirectories(buildDirectory);
        CleanDirectories(baseDirectory + "/**/bin");
        CleanDirectories(baseDirectory + "/**/obj");
    });

Task("Init")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        CreateDirectory(buildDirectory);
        CreateDirectory(outputDirectory);
    });

Task("Version")
    .IsDependentOn("Init")
    .Does(() =>
    {
        Information("Marking this build as version: " + version);

        var assemblyVersion = version.Contains('-') ? version.Substring(0, version.IndexOf('-')) : version;

        CreateAssemblyInfo(buildDirectory + "/CommonAssemblyInfo.cs", new AssemblyInfoSettings {
            Version = assemblyVersion,
            FileVersion = assemblyVersion,
            InformationalVersion = version,
            Copyright = String.Format("(c) Julian Easterling {0}", DateTime.Now.Year),
            Company = String.Empty,
            Configuration = configuration
        });
    });

Task("Compile")
    .IsDependentOn("Init")
    .IsDependentOn("Version")
    .Does(() =>
    {
        var buildSettings = msbuildSettings.WithTarget("ReBuild");
        buildSettings.AddFileLogger(
                new MSBuildFileLogger {
                    LogFile = buildDirectory + "/msbuild.log" });

        NuGetRestore("JulianSnippets/JulianSnippets.csproj");
        MSBuild("JulianSnippets/JulianSnippets.csproj", buildSettings);
    });

Task("Package")
    .IsDependentOn("Compile")
    .Does(() =>
    {
        CreateDirectory(buildDirectory + "/packages");
        
        CopyFile(outputDirectory + "\\JulianSnippets.vsix", buildDirectory + "/packages/JulianSnippets.vsix");
    });

///////////////////////////////////////////////////////////////////////////////

RunTarget(target);

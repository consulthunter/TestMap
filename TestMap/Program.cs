/*
 * consulthunter
 * 2024-11-07
 * Initial entry point for the tool
 * Uses CommandLine for CLI Options
 * Program.cs
 */

using System.CommandLine;
using System.Text.Json;
using TestMap.App;
using TestMap.CLIOptions;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Targets;
using TestMap.Services;
using TestMap.Services.Configuration;
using TestMap.Services.ProjectDiscovery;
using TestMap.Services.ProjectDiscovery.Contracts;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap;

public class Program
{
    public static Func<SetupOptions, SetupService> SetupServiceFactory { get; set; } =
        options => new SetupService(options.BasePath);

    /// <summary>
    ///     Main
    /// </summary>
    /// <param name="args">Arguments passed from the CLI</param>
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = BuildRootCommand();
        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("TestMap");

        rootCommand.Subcommands.Add(CreateSetupCommand());
        rootCommand.Subcommands.Add(CreateCheckProjectsCommand());
        rootCommand.Subcommands.Add(CreatePipelineCommand(
            "collect-tests",
            "Collect tests from source code.",
            configPath => new CollectTestOptions
            {
                CollectConfigFilePath = configPath
            }));
        rootCommand.Subcommands.Add(CreatePipelineCommand(
            "generate-tests",
            "Generates tests for the repository.",
            configPath => new GenerateTestsOptions
            {
                GenTestsConfigFilePath = configPath
            }));
        rootCommand.Subcommands.Add(CreateExperimentCommand());
        rootCommand.Subcommands.Add(CreateTargetsCommand());

        return rootCommand;
    }

    private static Command CreateTargetsCommand()
    {
        var targets = new Command("targets", "Create and verify pinned repository target manifests.");
        targets.Subcommands.Add(CreateTargetsCreateCommand());
        targets.Subcommands.Add(CreateTargetsVerifyCommand());
        return targets;
    }

    private static Command CreateCheckProjectsCommand()
    {
        var file = new Option<string>("--file", "-f") { Description = "Pinned target manifest YAML path." };
        var output = new Option<string>("--output", "-o") { Description = "Stable project-check bundle YAML path." };
        var maxConcurrency = new Option<int?>("--max-concurrency") { Description = "Maximum concurrent exact-commit checks." };
        var policy = new Option<string>("--policy") { Description = $"Classification policy; supported: {ProjectCheckPolicy.Identifier}." };
        var config = CreateConfigOption("Optional configuration file supplying target path and concurrency defaults.");
        var command = new Command("check-projects", "Classify pinned repository revisions and publish reusable target manifests.");
        command.Options.Add(file);
        command.Options.Add(output);
        command.Options.Add(maxConcurrency);
        command.Options.Add(policy);
        command.Options.Add(config);
        command.SetAction(async parseResult =>
        {
            try
            {
                var configPath = parseResult.GetValue(config);
                TestMapConfig? configValue = null;
                if (!string.IsNullOrWhiteSpace(configPath))
                {
                    Utilities.Utilities.Load(configPath);
                    configValue = LoadMainConfiguration(configPath);
                }

                var inputPath = parseResult.GetValue(file) ?? configValue?.RuntimeConfig.FilePaths.TargetFilePath;
                if (string.IsNullOrWhiteSpace(inputPath)) throw new ArgumentException("--file or a configured target path is required.");
                if (string.IsNullOrWhiteSpace(configPath)) Utilities.Utilities.Load(inputPath);
                var selectedPolicy = parseResult.GetValue(policy) ?? ProjectCheckPolicy.Identifier;
                if (!string.Equals(selectedPolicy, ProjectCheckPolicy.Identifier, StringComparison.Ordinal))
                    throw new ArgumentException($"Unsupported project-check policy '{selectedPolicy}'.");
                var concurrency = parseResult.GetValue(maxConcurrency) ?? configValue?.RuntimeConfig.MaxConcurrency ?? 4;
                if (concurrency < 1) throw new ArgumentOutOfRangeException("--max-concurrency", "Concurrency must be positive.");

                var result = await CheckProjectsAsync(
                    inputPath,
                    parseResult.GetValue(output),
                    concurrency,
                    Environment.GetEnvironmentVariable("GITHUB_TOKEN"));
                var summary = result.Report.Summary;
                Console.WriteLine(
                    $"Checked: {summary.InputTargets}; tests detected: {summary.TestsDetected}; " +
                    $"no tests detected: {summary.NoTestsDetected}; indeterminate: {summary.Indeterminate}; " +
                    $"bundle: {Path.GetFullPath(result.BundlePath)}.");
                if (summary.Indeterminate > 0)
                {
                    var breakdown = string.Join(", ", summary.ByStatus
                        .Where(pair => pair.Value > 0 && pair.Key is not (ProjectCheckStatus.TestsDetected or ProjectCheckStatus.NoTestsDetected))
                        .Select(pair => $"{pair.Key}={pair.Value}"));
                    Console.Error.WriteLine($"Project screening is incomplete: {breakdown}.");
                    return 2;
                }
                return 0;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Console.Error.WriteLine($"check-projects failed: {exception.Message}");
                return 1;
            }
        });
        return command;
    }

    public static async Task<ProjectCheckPublicationResult> CheckProjectsAsync(
        string inputPath,
        string? outputPath = null,
        int maxConcurrency = 4,
        string? githubToken = null,
        IProjectCheckProbe? probe = null,
        CancellationToken cancellationToken = default)
    {
        if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        var fingerprint = new TargetFingerprintService();
        var targetSerializer = new TargetManifestSerializer(fingerprint);
        var targetSourceReader = new TargetSourceReader(targetSerializer, fingerprint);
        var source = await targetSourceReader.ReadAsync(inputPath, TargetSourceMode.PinnedManifest, cancellationToken);
        var manifest = source.Manifest ?? throw new InvalidDataException("check-projects requires a pinned target manifest.");
        var manifestSha256 = source.ManifestSha256 ?? throw new InvalidDataException("Target manifest fingerprint is unavailable.");
        var validator = new ProjectCheckContractValidator();
        var sanitizer = new ProjectCheckSanitizer();
        var policy = new ProjectTestPresencePolicy();
        probe ??= new GitHubProjectTreeProbe(
            new GitHubProjectTreeClient(githubToken, new GitHubProjectCheckFailureClassifier()),
            policy,
            sanitizer);
        var coordinator = new ProjectCheckCoordinator(probe, validator, sanitizer);
        var observations = await coordinator.CheckAsync(manifest, maxConcurrency, cancellationToken);
        var report = new ProjectCheckReportBuilder(validator).Build(
            manifest, inputPath, manifestSha256, observations);
        var checkSerializer = new ProjectCheckSerializer(validator);
        var publication = new ProjectCheckBundlePublisher(
            checkSerializer,
            targetSerializer,
            new ProjectCheckPartitionService(validator),
            validator,
            new ProjectCheckOutputPathResolver(),
            fingerprint,
            new AtomicFilePublisher());
        return await publication.PublishAsync(
            inputPath, manifestSha256, manifest, report, outputPath, cancellationToken);
    }

    private static Command CreateTargetsVerifyCommand()
    {
        var file = new Option<string>("--file", "-f") { Description = "Target manifest YAML path." };
        var statusOutput = new Option<string>("--status-output") { Description = "Verification CSV path." };
        var maxConcurrency = new Option<int?>("--max-concurrency") { Description = "Maximum concurrent repository probes." };
        var command = new Command("verify", "Verify every target and requested commit in a manifest.");
        command.Options.Add(file);
        command.Options.Add(statusOutput);
        command.Options.Add(maxConcurrency);
        command.SetAction(async parseResult =>
        {
            var manifestPath = parseResult.GetValue(file);
            if (string.IsNullOrWhiteSpace(manifestPath)) throw new ArgumentException("--file is required.");
            var outputPath = parseResult.GetValue(statusOutput);
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, $"{Path.GetFileNameWithoutExtension(manifestPath)}-status.csv");
            var concurrency = parseResult.GetValue(maxConcurrency) ?? 4;
            if (concurrency < 1) throw new ArgumentOutOfRangeException("--max-concurrency", "Concurrency must be positive.");
            var records = await VerifyTargetManifestAsync(manifestPath, outputPath, concurrency);
            Console.WriteLine($"Verified: {records.Count}; available: {records.Count(row => row.Status == TargetVerificationStatus.Available)}; unavailable: {records.Count(row => row.Status != TargetVerificationStatus.Available)}.");
        });
        return command;
    }

    public static async Task<IReadOnlyList<TargetVerificationRecord>> VerifyTargetManifestAsync(
        string manifestPath,
        string statusOutputPath,
        int maxConcurrency = 4,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = new TargetFingerprintService();
        var serializer = new TargetManifestSerializer(fingerprint);
        var source = await new TargetSourceReader(serializer, fingerprint)
            .ReadAsync(manifestPath, TargetSourceMode.PinnedManifest, cancellationToken);
        var manifest = source.Manifest
            ?? throw new InvalidDataException("Pinned target source did not contain a manifest.");
        var manifestSha256 = source.ManifestSha256
            ?? throw new InvalidDataException("Pinned target source did not contain a manifest hash.");
        var coordinator = new TargetVerificationCoordinator(new TargetVerificationService(new LibGitRemoteRepositoryProbe()));
        var records = await coordinator.VerifyAsync(manifest, manifestSha256, maxConcurrency, cancellationToken);
        await new TargetVerificationReportWriter(new AtomicFilePublisher()).WriteAsync(statusOutputPath, records, cancellationToken);
        return records;
    }

    private static Command CreateTargetsCreateCommand()
    {
        var input = new Option<string>("--input", "-i") { Description = "Delimited source file." };
        var url = new Option<string>("--url") { Description = "Single GitHub repository URL." };
        var output = new Option<string>("--output", "-o") { Description = "Target manifest YAML path." };
        var delimiter = new Option<TargetDelimiter>("--delimiter") { Description = "Auto, comma, or tab." };
        var rejections = new Option<string>("--rejections") { Description = "Optional rejection report path." };
        var resolutionOutput = new Option<string>("--resolution-output") { Description = "Optional repository resolution YAML path." };
        var command = new Command("create", "Create a pinned repository target manifest.");
        command.Options.Add(input);
        command.Options.Add(url);
        command.Options.Add(output);
        command.Options.Add(delimiter);
        command.Options.Add(rejections);
        command.Options.Add(resolutionOutput);
        command.SetAction(async parseResult =>
        {
            var inputPath = parseResult.GetValue(input);
            var repositoryUrl = parseResult.GetValue(url);
            var outputPath = parseResult.GetValue(output);
            var hasInput = !string.IsNullOrWhiteSpace(inputPath);
            var hasUrl = !string.IsNullOrWhiteSpace(repositoryUrl);
            if (hasInput == hasUrl)
            {
                Console.Error.WriteLine("targets create requires exactly one of --input or --url.");
                return 1;
            }

            if (hasUrl)
            {
                if (parseResult.GetResult(delimiter) is not null || !string.IsNullOrWhiteSpace(parseResult.GetValue(rejections)))
                {
                    Console.Error.WriteLine("--delimiter and --rejections are valid only with --input.");
                    return 1;
                }
                try
                {
                    Utilities.Utilities.Load();
                    var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                    var urlResult = await CreateSingleRepositoryTargetAsync(
                        repositoryUrl!, outputPath, parseResult.GetValue(resolutionOutput), token);
                    Console.WriteLine(
                        $"Repository: {urlResult.Resolution.Repository}; branch: {urlResult.Resolution.DefaultBranch}; " +
                        $"commit: {urlResult.Resolution.ResolvedCommit}; " +
                        $"access: {urlResult.Resolution.AuthenticationMode.ToString().ToLowerInvariant()}; " +
                        $"manifest: {Path.GetFullPath(urlResult.ManifestPath)}; " +
                        $"resolution: {Path.GetFullPath(urlResult.ResolutionPath)}.");
                    return 0;
                }
                catch (SingleRepositoryTargetCreationException exception)
                {
                    Console.Error.WriteLine(
                        $"Target resolution failed: {exception.Resolution.Status}; " +
                        $"reason: {exception.Resolution.ReasonKind}; " +
                        $"resolution: {Path.GetFullPath(exception.ResolutionPath)}.");
                    return 1;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    Console.Error.WriteLine($"Target creation failed: {exception.Message}");
                    return 1;
                }
            }

            if (!string.IsNullOrWhiteSpace(parseResult.GetValue(resolutionOutput)))
            {
                Console.Error.WriteLine("--resolution-output is valid only with --url.");
                return 1;
            }
            outputPath = string.IsNullOrWhiteSpace(outputPath)
                ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(inputPath!))!, $"{Path.GetFileNameWithoutExtension(inputPath)}-targets.yaml")
                : outputPath;

            var result = await CreateTargetManifestAsync(
                inputPath!,
                outputPath,
                parseResult.GetValue(delimiter),
                parseResult.GetValue(rejections));
            Console.WriteLine(
                $"Input: {result.TotalRecords}; emitted: {result.Manifest.Targets.Count}; " +
                $"deduplicated: {result.DeduplicatedRecords}; rejected: {result.RejectedRecords}.");
            return 0;
        });
        return command;
    }

    public static Task<SingleRepositoryTargetCreationResult> CreateSingleRepositoryTargetAsync(
        string url,
        string? outputPath = null,
        string? resolutionOutputPath = null,
        string? githubToken = null,
        IRepositoryResolutionClient? resolutionClient = null,
        CancellationToken cancellationToken = default)
    {
        var identity = new TargetIdentityService();
        var fingerprint = new TargetFingerprintService();
        var resolutionValidator = new SingleRepositoryResolutionValidator(fingerprint);
        var serializer = new SingleRepositoryResolutionSerializer(resolutionValidator);
        resolutionClient ??= new GitHubRepositoryResolutionClient(
            githubToken, new GitHubRepositoryResolutionFailureClassifier());
        var resolver = new SingleRepositoryResolver(
            resolutionClient,
            identity,
            new SingleRepositoryResolutionSanitizer(),
            resolutionValidator);
        var service = new SingleRepositoryTargetCreationService(
            new SingleRepositoryUrlParser(identity, fingerprint),
            resolver,
            serializer,
            new TargetManifestSerializer(fingerprint),
            identity,
            fingerprint,
            new SingleRepositoryTargetContractValidator(resolutionValidator, fingerprint),
            new SingleRepositoryTargetPathResolver(),
            new AtomicFilePublisher());
        var accessMode = string.IsNullOrWhiteSpace(githubToken)
            ? RepositoryAuthenticationMode.Anonymous
            : RepositoryAuthenticationMode.Authenticated;
        return service.CreateAsync(
            url, outputPath, resolutionOutputPath, accessMode, cancellationToken);
    }

    public static Task<TargetManifestCreationResult> CreateTargetManifestAsync(
        string inputPath,
        string outputPath,
        TargetDelimiter delimiter = TargetDelimiter.Auto,
        string? rejectionPath = null,
        CancellationToken cancellationToken = default)
    {
        var identity = new TargetIdentityService();
        var fingerprint = new TargetFingerprintService();
        var publisher = new AtomicFilePublisher();
        var service = new TargetManifestCreationService(
            new DelimitedTargetImportService(identity),
            new TargetManifestSerializer(fingerprint),
            new TargetRejectionReportWriter(),
            fingerprint,
            publisher);
        return service.CreateAsync(inputPath, outputPath, delimiter, rejectionPath, cancellationToken);
    }

    private static Command CreatePipelineCommand(
        string name,
        string description,
        Func<string, IPipelineOptions> createOptions)
    {
        var configOption = CreateConfigOption("Config File path.");
        var command = new Command(name, description);
        command.Options.Add(configOption);
        command.SetAction(async parseResult =>
        {
            try
            {
                var configPath = parseResult.GetValue(configOption) ?? string.Empty;
                await Run(createOptions(configPath));
                return 0;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Console.Error.WriteLine($"{name} failed: {exception.Message}");
                return 1;
            }
        });

        return command;
    }

    private static Command CreateExperimentCommand()
    {
        var configOption = CreateConfigOption("Path to the main TestMap configuration JSON file.");
        var command = new Command("experiment", "Run AI provider comparison experiments for test generation.");
        command.Options.Add(configOption);
        command.SetAction(async parseResult =>
        {
            await Run(new ExperimentOptions
            {
                ConfigFilePath = parseResult.GetValue(configOption) ?? string.Empty
            });
        });

        return command;
    }

    private static Command CreateSetupCommand()
    {
        var basePathOption = new Option<string>("--base-path", "-b")
        {
            Description = "Base Path for the project."
        };
        var overwriteOption = new Option<bool>("--overwrite", "-o")
        {
            Description = "Overwrite Config File."
        };
        var command = new Command("setup", "Generates the config file.");
        command.Options.Add(basePathOption);
        command.Options.Add(overwriteOption);
        command.SetAction(async parseResult =>
        {
            await Run(new SetupOptions
            {
                BasePath = parseResult.GetValue(basePathOption) ?? string.Empty,
                OverwriteFile = parseResult.GetValue(overwriteOption)
            });
        });

        return command;
    }

    private static Option<string> CreateConfigOption(string description)
    {
        return new Option<string>("--config", "-c")
        {
            Description = description
        };
    }

    /// <summary>
    /// Routes command-line arguments to the appropriate execution handler.
    /// </summary>
    /// <param name="obj">Parsed command-line options object.</param>
    private static async Task Run(object obj)
    {
        switch (obj)
        {
            case ExperimentOptions experimentOptions:
                await RunExperimentPipeline(experimentOptions);
                break;
            case IPipelineOptions pipelineOptions:
                await RunPipeline(pipelineOptions);
                break;
            case SetupOptions setupOptions:
                RunSetup(setupOptions);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown options type: {obj.GetType().Name}");
        }
    }

    /// <summary>
    /// Unified pipeline execution method for all pipeline-based commands.
    /// Loads configuration, initializes services, and runs the pipeline.
    /// </summary>
    /// <param name="options">Pipeline options implementing IPipelineOptions.</param>
    private static async Task RunPipeline(IPipelineOptions options)
    {
        // Load utilities and secrets
        Utilities.Utilities.Load(options.ConfigFilePath);

        // Load and bind configuration
        var configObj = LoadMainConfiguration(options.ConfigFilePath);

        // Configure service with run mode and secrets
        var configurationService = new ConfigurationService(configObj)
        {
            RunMode = options.Mode
        };
        configurationService.SetSecrets();

        // Create and run the pipeline coordinator
        var pipelineRunner = new ProjectRunCoordinator(configurationService);
        await pipelineRunner.RunAsync();
    }

    private static async Task RunExperimentPipeline(ExperimentOptions options)
    {
        Utilities.Utilities.Load(options.ConfigFilePath);

        var configObj = LoadMainConfiguration(options.ConfigFilePath);

        var configurationService = new ConfigurationService(configObj)
        {
            RunMode = options.Mode
        };
        configurationService.SetSecrets();

        var pipelineRunner = new ProjectRunCoordinator(configurationService);
        await pipelineRunner.RunAsync();
    }

    /// <summary>
    /// Generates the correct configuration for TestMap.
    /// </summary>
    /// <param name="options">Setup options parsed by CommandLine.</param>
    private static void RunSetup(SetupOptions options)
    {
        var setupService = SetupServiceFactory(options);
        setupService.Setup(options.OverwriteFile);
    }

    private static string ConfigurationLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Path.Join(Directory.GetCurrentDirectory(), "Config", "default-config.json");
        return path;
    }

    private static TestMapConfig LoadMainConfiguration(string path)
    {
        var json = File.ReadAllText(ConfigurationLocation(path));
        var configObj = JsonSerializer.Deserialize<TestMapConfig>(json, ConfigJsonSerializer.CreateOptions());

        return configObj
               ?? throw new InvalidOperationException(
                   $"Config file '{ConfigurationLocation(path)}' could not be parsed.");
    }
}

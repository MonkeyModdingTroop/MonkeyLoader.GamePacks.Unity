﻿using HarmonyLib;
using MonkeyLoader.Configuration;
using MonkeyLoader.Logging;
using MonkeyLoader.NuGet;
using MonkeyLoader.Patching;
using MonkeyLoader.Prepatching;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Zio;

namespace MonkeyLoader.Meta
{
    /// <summary>
    /// Contains all the metadata and references to loaded patchers from a mod file.
    /// </summary>
    public sealed class Mod : IConfigOwner, IShutdown
    {
        /// <summary>
        /// The search pattern for mod files.
        /// </summary>
        public const string SearchPattern = "*.nupkg";

        internal readonly HashSet<Assembly> PatcherAssemblies = new();
        internal readonly HashSet<Assembly> PrePatcherAssemblies = new();

        private const string PrePatchersFolderName = "pre-patchers";
        private readonly UPath[] _assemblyPaths;
        private readonly HashSet<string> _authors;
        private readonly HashSet<IEarlyMonkey> _earlyMonkeys = new();
        private readonly HashSet<IMonkey> _monkeys = new();
        private readonly HashSet<string> _tags;

        /// <summary>
        /// Gets the names of the authors of this mod.
        /// </summary>
        public IEnumerable<string> Authors => _authors.AsSafeEnumerable();

        /// <summary>
        /// Gets the config that this mod's (pre-)patcher(s) can use to load <see cref="ConfigSection"/>s.
        /// </summary>
        public Config Config { get; }

        /// <summary>
        /// Gets the path where this mod's config file should be.
        /// </summary>
        public string ConfigPath { get; }

        /// <summary>
        /// Gets the description of this mod.
        /// </summary>
        public string Description { get; }

        public bool EarlyMonkeyLoadError { get; private set; }

        /// <summary>
        /// Gets the available <see cref="IEarlyMonkey"/>s of this mod.
        /// </summary>
        public IEnumerable<IEarlyMonkey> EarlyMonkeys => _earlyMonkeys.AsSafeEnumerable();

        /// <summary>
        /// Gets the readonly file system of this mod's file.
        /// </summary>
        public IFileSystem FileSystem { get; }

        /// <summary>
        /// Gets the <see cref="HarmonyLib.Harmony"/> instance to be used by this mod's (pre-)patcher(s).
        /// </summary>
        public Harmony Harmony { get; }

        public bool HasPrePatchers { get; }

        /// <summary>
        /// Gets the path to the mod's icon inside the mod's <see cref="FileSystem">FileSystem</see>.<br/>
        /// <c>null</c> if it wasn't given or doesn't exist.
        /// </summary>
        public UPath? IconPath { get; }

        /// <summary>
        /// Gets the Url to the mod's icon on the web.<br/>
        /// <c>null</c> if it wasn't given or was invalid.
        /// </summary>
        public Uri? IconUrl { get; }

        /// <summary>
        /// Gets the unique identifier of this mod.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets whether this mod is a game pack.
        /// </summary>
        public bool IsGamePack { get; }

        /// <summary>
        /// Gets the <see cref="MonkeyLoader"/> instance that loaded this mod.
        /// </summary>
        public MonkeyLoader Loader { get; }

        public bool LoadError => EarlyMonkeyLoadError || MonkeyLoadError;

        /// <summary>
        /// Gets the absolute file path to this mod's file.
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// Gets the logger to be used by this mod.
        /// </summary>
        /// <remarks>
        /// Every mod instance has its own logger and can thus have a different <see cref="LoggingLevel"/>.<br/>
        /// They do all share the <see cref="Loader">Loader's</see> <see cref="MonkeyLoader.LoggingHandler">LoggingHandler</see> though.
        /// </remarks>
        public MonkeyLogger Logger { get; }

        public bool MonkeyLoadError { get; private set; }

        /// <summary>
        /// Gets the available <see cref="IMonkey"/>s of this mod.
        /// </summary>
        public IEnumerable<IMonkey> Monkeys => _monkeys.AsSafeEnumerable();

        public IEnumerable<UPath> PatcherAssemblyPaths => _assemblyPaths.Where(path => !path.FullName.Contains(PrePatchersFolderName));

        public IEnumerable<UPath> PrePatcherAssemblyPaths => _assemblyPaths.Where(path => path.FullName.Contains(PrePatchersFolderName));

        /// <summary>
        /// Gets the Url to this mod's project website.<br/>
        /// <c>null</c> if it wasn't given or was invalid.
        /// </summary>
        public Uri? ProjectUrl { get; }

        /// <summary>
        /// Gets the release notes for this mod's version.
        /// </summary>
        public string ReleaseNotes { get; }

        /// <summary>
        /// Gets whether this <see cref="Mod"/>'s <see cref="Shutdown"/> method failed when it was called.
        /// </summary>
        public bool ShutdownFailed { get; private set; } = false;

        /// <summary>
        /// Gets whether this <see cref="Mod"/>'s <see cref="Shutdown"/> method has been called.
        /// </summary>
        public bool ShutdownRan { get; private set; } = false;

        /// <summary>
        /// Gets the tags of this mod.
        /// </summary>
        public IEnumerable<string> Tags => _tags.AsSafeEnumerable();

        /// <summary>
        /// Gets the nice identifier of this mod.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets this mod's version.
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Creates a new <see cref="Mod"/> instance with the given <paramref name="monkeyLoader"/> and <paramref name="fileSystem"/> for the <paramref name="location"/>.<br/>
        /// The metadata gets loaded from a <c>.nuspec</c> file, which must be at the root of the file system.
        /// </summary>
        /// <param name="monkeyLoader">The loader instance that loaded this mod.</param>
        /// <param name="location">The absolute file path to the mod's file.</param>
        /// <param name="fileSystem">The file system of the mod's file.</param>
        /// <param name="isGamePack">Whether this mod is a game pack.</param>
        /// <exception cref="FileNotFoundException">When there's no <c>.nuspec</c> file at the root of the file system.</exception>
        public Mod(MonkeyLoader monkeyLoader, string location, IFileSystem fileSystem, bool isGamePack)
        {
            if (!(fileSystem.EnumerateFiles("/", "*.nuspec").SingleOrDefault() is UPath nuspecPath))
                throw new FileNotFoundException("Couldn't find required .nuspec file at the root of the mod's file system.", location);

            IsGamePack = isGamePack;

            using var nuspecStream = fileSystem.OpenFile(nuspecPath, FileMode.Open, FileAccess.Read);
            var nuspecReader = new NuspecReader(nuspecStream);

            Loader = monkeyLoader;
            Id = nuspecReader.GetId();
            Harmony = new Harmony(Id);

            Location = location;
            FileSystem = fileSystem;

            Title = nuspecReader.GetTitle();
            Version = nuspecReader.GetVersion().Version;
            Description = nuspecReader.GetDescription();
            ReleaseNotes = nuspecReader.GetReleaseNotes();

            Logger = new MonkeyLogger(monkeyLoader.Logger, Title);

            var iconPath = nuspecReader.GetIcon();
            if (fileSystem.FileExists(iconPath))
                IconPath = iconPath;
            else if (!string.IsNullOrWhiteSpace(iconPath))
                Logger.Warn(() => $"Icon Path [{iconPath}] is set but the file doesn't exist for mod: {location}");

            var iconUrl = nuspecReader.GetIconUrl();
            if (Uri.TryCreate(iconUrl, UriKind.Absolute, out var iconUri))
                IconUrl = iconUri;
            else if (!string.IsNullOrWhiteSpace(iconUrl))
                Logger.Warn(() => $"Icon Url [{iconUrl}] is set but is invalid for mod: {location}");

            var projectUrl = nuspecReader.GetProjectUrl();
            if (Uri.TryCreate(projectUrl, UriKind.Absolute, out var projectUri))
                ProjectUrl = projectUri;
            else if (!string.IsNullOrWhiteSpace(projectUrl))
                Logger.Warn(() => $"Project Url [{projectUrl}] is set but is invalid for mod: {location}");

            _tags = new(nuspecReader.GetTags().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            _authors = new(nuspecReader.GetAuthors().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(name => name.Trim()));

            ConfigPath = Path.Combine(Loader.Locations.Configs, $"{Id}.json");
            Config = new Config(this);

            var assemblyFolder = $"/lib/{NuGetManager.Framework.GetShortFolderName()}/";
            Logger.Trace(() => $"Checking package folder: {assemblyFolder}");

            if (!fileSystem.SmartDirectoryExists(assemblyFolder))
            {
                if (fileSystem.SmartDirectoryExists("/lib/"))
                {
                    assemblyFolder = "/lib/";
                    Logger.Error(() => $"No lib folder targeting the right framework [{assemblyFolder}] found for mod, falling back to lib: {location}");
                }
                else
                {
                    _assemblyPaths = Array.Empty<UPath>();
                    Logger.Error(() => $"No lib folder at all found for mod: {location}");

                    return;
                }
            }

            _assemblyPaths = fileSystem.EnumerateFiles("/", "*.dll", SearchOption.AllDirectories).Where(path => path.FullName.StartsWith(assemblyFolder, StringComparison.OrdinalIgnoreCase)).ToArray();
            HasPrePatchers = _assemblyPaths.Any(path => path.FullName.Contains(PrePatchersFolderName));

            Logger.Trace(() => $"Found the following Assembly Files to consider:{Environment.NewLine}{string.Join(Environment.NewLine, _assemblyPaths)}");
        }

        /// <summary>
        /// Efficiently checks, whether a given name is listed as an author for this mod.
        /// </summary>
        /// <param name="author">The name to check for.</param>
        /// <returns><c>true</c> if the given name is listed as an author for this mod.</returns>
        public bool HasAuthor(string author) => _authors.Contains(author);

        /// <summary>
        /// Efficiently checks, whether a given tag is listed for this mod.
        /// </summary>
        /// <param name="tag">The tag to check for.</param>
        /// <returns><c>true</c> if the given tag is listed for this mod.</returns>
        public bool HasTag(string tag) => _tags.Contains(tag);

        /// <summary>
        /// Lets this mod cleanup and shutdown.<br/>
        /// Must only be called once.
        /// </summary>
        /// <returns>Whether it ran successfully.</returns>
        /// <inheritdoc/>
        public bool Shutdown()
        {
            if (ShutdownRan)
                throw new InvalidOperationException("A monkey's Shutdown() method must only be called once!");

            ShutdownRan = true;

            Config.Save();

            try
            {
                foreach (var earlyMonkey in _earlyMonkeys)
                    ShutdownFailed |= !earlyMonkey.Shutdown();

                foreach (var monkey in _monkeys)
                    ShutdownFailed |= monkey.Shutdown();
            }
            catch (Exception ex)
            {
                ShutdownFailed = true;
                Logger.Error(() => ex.Format("A monkey's Shutdown threw an Exception:"));
            }

            return !ShutdownFailed;
        }

        internal void LoadEarlyMonkeys()
        {
            foreach (var prepatcherPath in PrePatcherAssemblyPaths)
            {
                try
                {
                    using var assemblyFile = FileSystem.OpenFile(prepatcherPath, FileMode.Open, FileAccess.Read);
                    using var memStream = new MemoryStream();
                    assemblyFile.CopyTo(memStream);

                    var assembly = Assembly.Load(memStream.ToArray());
                    Loader.AddJsonConverters(assembly);
                    PrePatcherAssemblies.Add(assembly);

                    foreach (var type in assembly.GetTypes().Instantiable<IEarlyMonkey>())
                    {
                        Logger.Debug(() => $"Found instantiable EarlyMonkey Type: {type.FullName}");
                        var monkey = MonkeyBase.GetInstance(type);
                        monkey.Mod = this;
                        _earlyMonkeys.Add((IEarlyMonkey)monkey);
                    }

                    Logger.Info(() => $"Found {_earlyMonkeys.Count} Early Monkeys!");
                }
                catch (Exception ex)
                {
                    EarlyMonkeyLoadError = true;
                    Logger.Error(() => ex.Format($"Error while loading Early Monkeys from assembly: {prepatcherPath}!"));
                }
            }
        }

        internal void LoadMonkeys()
        {
            // assemblies should be Mono.Cecil loaded before the Early ones, to allow pre-patchers access

            foreach (var patcherPath in PatcherAssemblyPaths)
            {
                try
                {
                    Logger.Debug(() => $"Loading patcher assembly from: {patcherPath}");

                    using var assemblyFile = FileSystem.OpenFile(patcherPath, FileMode.Open, FileAccess.Read);
                    using var memStream = new MemoryStream();
                    assemblyFile.CopyTo(memStream);

                    var assembly = Assembly.Load(memStream.ToArray());
                    Loader.AddJsonConverters(assembly);
                    PatcherAssemblies.Add(assembly);

                    Logger.Info(() => $"Loaded patcher assembly: {assembly.FullName}");

                    foreach (var type in assembly.GetTypes().Instantiable<MonkeyBase>())
                    {
                        Logger.Debug(() => $"Found instantiable Monkey Type: {type.FullName}");
                        var monkey = MonkeyBase.GetInstance(type);
                        monkey.Mod = this;
                        _monkeys.Add(monkey);
                    }

                    Logger.Info(() => $"Found {_monkeys.Count} Monkeys!");
                }
                catch (Exception ex)
                {
                    MonkeyLoadError = true;
                    Logger.Error(() => ex.Format($"Error while loading Monkeys from assembly: {patcherPath}!"));
                }
            }
        }
    }
}
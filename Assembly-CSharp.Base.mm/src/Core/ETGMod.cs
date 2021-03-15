using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Ionic.Zip;
using System.Collections;
using SGUI;
using ETGGUI;

/// <summary>
/// Main ETGMod class. Most of the "Mod the Gungeon" logic flows through here.
/// </summary>
public static partial class ETGMod {

    public readonly static Version BaseVersion = new Version(0, 4, 0);
    // The following line will be replaced by Travis.
    public readonly static int BaseTravisBuild = 0;
    /// <summary>
    /// Base version profile, used separately from BaseVersion.
    /// A higher profile ID means higher instability ("developerness").
    /// </summary>
    public readonly static Profile BaseProfile =
        #if TRAVIS
        new Profile(2, "travis");
        #elif DEBUG
        new Profile(1, "b2-debug");
        #else
        new Profile(0, "b2"); // no tag
        #endif

    public static string BaseUIVersion {
        get {
            string v = BaseVersion.ToString(3);

            if (BaseTravisBuild != 0) {
                v += "-";
                v += BaseTravisBuild;
            }

            if (!string.IsNullOrEmpty(BaseProfile.Name)) {
                v += "-";
                v += BaseProfile.Name;
            }

            return v;
        }
    }

    public static string GameFolder;
    public static string ModsDirectory;
    public static string ModsListFile;
    public static string RelinkCacheDirectory;
    public static string ResourcesDirectory;

    /// <summary>
    /// Used for CallInEachModule to call a method in each type of mod.
    /// </summary>
    public static List<ETGModule> AllMods = new List<ETGModule>();
    public static List<string> LoadedModPaths = new List<string>();
    private static List<Type> _ModuleTypes = new List<Type>();

    private static List<Dictionary<string, MethodInfo>> _ModuleMethods = new List<Dictionary<string, MethodInfo>>();

    public static List<ETGModule> GameMods = new List<ETGModule>();

	/*
    public static string[] LaunchArguments;

    private delegate string[] d_mono_runtime_get_main_args(); //ret MonoArray*
    */

    [Obsolete("Use StartGlobalCoroutine instead.")]
    public static Func<IEnumerator, Coroutine> StartCoroutine;
    public static Func<IEnumerator, Coroutine> StartGlobalCoroutine;
    public static Action<Coroutine> StopGlobalCoroutine;

    private static bool _Init = false;
    public static bool Initialized {
        get {
            return _Init;
        }
    }
    public static void Init() {
        if (_Init) {
            return;
        }
        _Init = true;

        /*
        LaunchArguments = PInvokeHelper.Mono.GetDelegate<d_mono_runtime_get_main_args>()();
        for (int i = 1; i < LaunchArguments.Length; i++) {
            string arg = LaunchArguments[i];
            if (arg == "--no-steam" || arg == "-ns") {
                Platform.DisableSteam = true;
            }
        }
        */

        GameFolder = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
		Debug.Log($"ETGMOD INIT: GAMEFOLDER = {GameFolder}");
        ModsDirectory = Path.Combine(GameFolder, "Mods");
        ModsListFile = Path.Combine(ModsDirectory, "mods.txt");
        RelinkCacheDirectory = Path.Combine(ModsDirectory, "RelinkCache");
        ResourcesDirectory = Path.Combine(GameFolder, "Resources");

        Application.logMessageReceived += ETGModDebugLogMenu.Logger;

        SGUIIMBackend.GetFont = (SGUIIMBackend backend) => FontConverter.GetFontFromdfFont((dfFont) patch_MainMenuFoyerController.Instance.VersionLabel.Font, 2);
        GameUIRoot.Instance.Manager.ConsumeMouseEvents = false;
        SGUIRoot.Setup();

        Debug.Log("ETGMod " + BaseUIVersion);
        Assets.HookUnity();
        Objects.HookUnity();
        Assembly.GetCallingAssembly().MapAssets();

        ETGModGUI.Create();

        LoadMods();

        Assets.Crawl(ResourcesDirectory);

        // Blindly check for all objects for the wanted stuff
        tk2dBaseSprite[] sprites = UnityEngine.Object.FindObjectsOfType<tk2dBaseSprite>();
        for (int i = 0; i < sprites.Length; i++) {
            tk2dBaseSprite sprite = sprites[i];
            if (sprite?.Collection == null) continue;
            if (sprite.Collection.spriteCollectionName == "ItemCollection") {
                Databases.Items.ItemCollection = sprite.Collection;
            }
            if (sprite.Collection.spriteCollectionName == "WeaponCollection") {
                Databases.Items.WeaponCollection = sprite.Collection;
            }
            if (sprite.Collection.spriteCollectionName == "WeaponCollection02") {
                Databases.Items.WeaponCollection02 = sprite.Collection;
            }
        }

        _InitializeAPIs();

        CallInEachModule(m => m.Init());
    }

    public static void Start() {
        ETGModGUI.Start();

        TestGunController.Add();
        // BalloonGunController.Add();

        dfInputManager manager = GameUIRoot.Instance.Manager.GetComponent<dfInputManager>();
        manager.Adapter = new SGUIDFInput(manager.Adapter);

        CallInEachModule(m => m.Start());

        // Needs to happen late as mods can add their own guns.
        StartGlobalCoroutine(ETGModGUI.ListAllItemsAndGuns());
    }

    private static void _InitializeAPIs() {
        Debug.Log("Initializing APIs");
        Gungeon.Game.Initialize();
    }

	public static void WriteModsFile()
	{
		using (StreamWriter writer = File.CreateText(ModsListFile))
		{
			writer.WriteLine("# Lines beginning with # are comment lines and thus ignored.");
			writer.WriteLine("# Each line here should either be the name of a mod .zip or the path to it.");
			writer.WriteLine("# The order in this .txt is the order in which the mods get loaded.");

            foreach (var modPath in LoadedModPaths)
            {
                writer.WriteLine(modPath);
            }
		}
	}

    private static List<string> ReadModsDirectory()
    {
        var mods = new List<string>();
        string[] files = Directory.GetFiles(ModsDirectory);
        foreach (var path in files)
        {
            string fileName = Path.GetFileName(path);
            if (fileName.EndsWithInvariant(".zip"))
            {
                mods.Add(path.Trim());
            }
        }

        files = Directory.GetDirectories(ModsDirectory);
        foreach (var path in files)
        {
            string dirName = Path.GetFileName(path);
            if (dirName != "RelinkCache")
            {
                mods.Add(path.Trim());
            }
        }

        return mods;
    }

    private static void LoadMods()
    {
        Debug.Log("Loading game mods...");

        if (!Directory.Exists(ModsDirectory))
        {
            Debug.Log("Mods directory not existing, creating...");
            Directory.CreateDirectory(ModsDirectory);
        }

        List<string> mods;
        if (!File.Exists(ModsListFile))
        {
            Debug.Log("Mods.txt does not exist. Reading directory.");
            mods = ReadModsDirectory();
        }
        else if (!TryParseModsFile(out mods))
        {
            Debug.Log("Mods.txt was not valid. Reading directory.");
            mods = ReadModsDirectory();
        }

        foreach (var mod in mods)
        {
            try
            {
                InitMod(mod);
            }
            catch (Exception e)
            {
                Debug.LogError("ETGMOD could not load mod " + mod + "! Check your output log / player log.");
                LogDetailed(e);
            }
        }
    }

    private static bool TryParseModsFile(out List<string> mods)
    {
        // Pre-run all lines to check if something's invalid
        try
        {
            string[] lines = File.ReadAllLines(ModsListFile);
            mods = new List<string>();
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                string trimmed = line.Trim();
                if (trimmed[0] == '#')
                {
                    continue;
                }

                string path = trimmed;
                string absolutePath = Path.Combine(ModsDirectory, path);
                if (!File.Exists(path) && !File.Exists(absolutePath) &&
                    !Directory.Exists(path) && !Directory.Exists(absolutePath))
                {
                    Debug.Log($"mods.txt: Could not find mod '{path}'");
                    continue;
                }

                mods.Add(path);
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.Log($"Failed to read mods.txt: {e.Message}");
            Debug.LogException(e);
            mods = null;
            return false;
        }
    }

    public static void LogDetailed(Exception e, string tag = null) {
        for (Exception e_ = e; e_ != null; e_ = e_.InnerException) {
            Console.WriteLine(e_.GetType().FullName + ": " + e_.Message + "\n" + e_.StackTrace);
            if (e_ is ReflectionTypeLoadException) {
                ReflectionTypeLoadException rtle = (ReflectionTypeLoadException) e_;
                for (int i = 0; i < rtle.Types.Length; i++) {
                    Console.WriteLine("ReflectionTypeLoadException.Types[" + i + "]: " + rtle.Types[i]);
                }
                for (int i = 0; i < rtle.LoaderExceptions.Length; i++) {
                    LogDetailed(rtle.LoaderExceptions[i], tag + (tag == null ? "" : ", ") + "rtle:" + i);
                }
            }
            if (e_ is TypeLoadException) {
                Console.WriteLine("TypeLoadException.TypeName: " + ((TypeLoadException) e_).TypeName);
            }
            if (e_ is BadImageFormatException) {
                Console.WriteLine("BadImageFormatException.FileName: " + ((BadImageFormatException) e_).FileName);
            }
        }
    }

    public static void InitMod(string path) {
        if (path.EndsWithInvariant(".zip")) {
            InitModZIP(path);
        } else {
            InitModDir(path);
        }
    }

    public static void InitModZIP(string archive)
    {
        Debug.Log("Initializing mod ZIP " + archive);

        string origArchive = archive;
        if (!File.Exists(archive))
        {
            // Probably a mod in the mod directory
            archive = Path.Combine(ModsDirectory, archive);
        }

        // Fallback metadata in case none is found
        ETGModuleMetadata metadata = new ETGModuleMetadata()
        {
            Name = Path.GetFileNameWithoutExtension(archive),
            Version = new Version(0, 0),
            DLL = "mod.dll"
        };
        Assembly asm = null;

        using (ZipFile zip = ZipFile.Read(archive))
        {
            // First read the metadata, ...
            Texture2D icon = null;
            var metadataFileEntry = zip["metadata.txt"];
            if (metadataFileEntry != null)
            {
                using (var stream = metadataFileEntry.OpenReader())
                {
                    metadata = ETGModuleMetadata.Parse(archive, "", stream);
                }
            }

            var iconEntry = zip["icon.png"];
            if (iconEntry != null)
            {
                icon = new Texture2D(2, 2);
                icon.name = "icon";
                var iconData = iconEntry.ExtractToArray();
                icon.LoadImage(iconData);
                icon.filterMode = FilterMode.Point;
                metadata.Icon = icon;
            }

            // ... then check if the mod runs on this profile ...
            if (!metadata.Profile.RunsOn(BaseProfile))
            {
                // Debug.LogWarning("http://www.windoof.org/sites/default/files/unsupported.gif");
                return;
            }

            // ... then check if the dependencies are loaded ...
            foreach (ETGModuleMetadata dependency in metadata.Dependencies)
            {
                if (!DependencyLoaded(dependency))
                {
                    Debug.LogWarning("DEPENDENCY " + dependency + " OF " + metadata + " NOT LOADED!");
                    return;
                }
            }

            // ... then everything else
            var asmLookup = new Dictionary<string, string>();
            ZipEntry dllEntry = null;
            foreach (ZipEntry entry in zip.Entries)
            {
                if (entry.FileName == metadata.DLL)
                {
                    dllEntry = entry;
                }
                else
                {
                    byte[] data = null;

                    if (!entry.IsDirectory)
                    {
                        if (entry.FileName.StartsWith("sprites/"))
                        {
                            data = entry.ExtractToArray();
                        }

                        if (entry.FileName.EndsWith(".dll"))
                        {
                            string fileName = Path.GetFileName(entry.FileName);
                            asmLookup[fileName] = entry.FileName;
                        }
                    }

                    Assets.AddMapping(entry.FileName, new AssetMetadata(archive, entry.FileName, data)
                    {
                        AssetType = entry.IsDirectory ? Assets.t_AssetDirectory : null
                    });
                }
            }

            if (dllEntry == null)
            {
                return;
            }

            // ... then add an AssemblyResolve handler for all the .zip-ped libraries
            if (asmLookup.Count > 0)
            {
                AppDomain.CurrentDomain.AssemblyResolve += metadata.GenerateModAssemblyResolver(asmLookup);
            }

            using (var ms = dllEntry.ExtractToMemoryStream())
            {
                if (metadata.Prelinked)
                {
                    asm = Assembly.Load(ms.GetBuffer());
                }
                else
                {
                    asm = metadata.GetRelinkedAssembly(ms);
                }
            }
        }

        if (asm == null)
        {
            return;
        }

        asm.MapAssets();

        Type[] types = asm.GetTypes();
        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            if (!typeof(ETGModule).IsAssignableFrom(type) || type.IsAbstract)
            {
                continue;
            }

            ETGModule module = (ETGModule)type.GetConstructor(Type.EmptyTypes).Invoke(Array<object>.Empty);

            module.Metadata = metadata;

            GameMods.Add(module);
            AllMods.Add(module);
            LoadedModPaths.Add(origArchive);
            _ModuleTypes.Add(type);
            _ModuleMethods.Add(new Dictionary<string, MethodInfo>());
        }

        Debug.Log("Mod " + metadata.Name + " initialized.");
    }

    public static void InitModDir(string dir) {
        Debug.Log("Initializing mod directory " + dir);

        string origDir = dir;
        if (!Directory.Exists(dir)) {
            // Probably a mod in the mod directory
            dir = Path.Combine(ModsDirectory, dir);
        }

        // Fallback metadata in case none is found
        ETGModuleMetadata metadata = new ETGModuleMetadata() {
            Name = Path.GetFileName(dir),
            Version = new Version(0, 0),
            DLL = "mod.dll"
        };
        Assembly asm = null;

        // First read the metadata, ...
        string metadataPath = Path.Combine(dir, "metadata.txt");
        if (File.Exists(metadataPath)) {
            using (FileStream fs = File.OpenRead(metadataPath)) {
                metadata = ETGModuleMetadata.Parse("", dir, fs);
            }
        }

        // ... then check if the dependencies are loaded ...
        foreach (ETGModuleMetadata dependency in metadata.Dependencies) {
            if (!DependencyLoaded(dependency)) {
                Debug.LogWarning("DEPENDENCY " + dependency + " OF " + metadata + " NOT LOADED!");
                return;
            }
        }

        // ... then everything else
        if (!File.Exists(metadata.DLL)) {
            return;
        }

        // ... then add an AssemblyResolve handler for all the .zip-ped libraries
        AppDomain.CurrentDomain.AssemblyResolve += metadata.GenerateModAssemblyResolver(null);

        if (metadata.Prelinked) {
            asm = Assembly.LoadFrom(metadata.DLL);
        } else {
            using (FileStream fs = File.OpenRead(metadata.DLL)) {
                asm = metadata.GetRelinkedAssembly(fs);
            }
        }

        asm.MapAssets();
        Assets.Crawl(dir);

        Type[] types = asm.GetTypes();
        for (int i = 0; i < types.Length; i++) {
            Type type = types[i];
            if (!typeof(ETGModule).IsAssignableFrom(type) || type.IsAbstract) {
                continue;
            }

            ETGModule module = (ETGModule) type.GetConstructor(Type.EmptyTypes).Invoke(Array<object>.Empty);

            module.Metadata = metadata;

            GameMods.Add(module);
            AllMods.Add(module);
            LoadedModPaths.Add(origDir);
            _ModuleTypes.Add(type);
            _ModuleMethods.Add(new Dictionary<string, MethodInfo>());
        }

        Debug.Log("Mod " + metadata.Name + " initialized.");
    }

    private static ResolveEventHandler GenerateModAssemblyResolver(this ETGModuleMetadata metadata, Dictionary<string, string> asmLookup)
    {
        if (!string.IsNullOrEmpty(metadata.Archive))
        {
            return delegate (object sender, ResolveEventArgs args)
            {
                string asmName = new AssemblyName(args.Name).Name + ".dll";
                if (!asmLookup.TryGetValue(asmName, out string entryFileName))
                {
                    return null;
                }

                using (ZipFile zip = ZipFile.Read(metadata.Archive))
                {
                    var entry = zip[entryFileName];
                    if (entry == null)
                        return null;

                    var asmData = entry.ExtractToArray();
                    return Assembly.Load(asmData);
                }
            };
        }

        if (!string.IsNullOrEmpty(metadata.Directory))
        {
            return delegate (object sender, ResolveEventArgs args)
            {
                string asmPath = Path.Combine(metadata.Directory, new AssemblyName(args.Name).Name + ".dll");
                if (!File.Exists(asmPath))
                {
                    return null;
                }

                return Assembly.LoadFrom(asmPath);
            };
        }

        return null;
    }

    public static void Exit() {
        CallInEachModule(m => m.Exit());
    }

    /// <summary>
    /// Checks if an dependency is loaded.
    /// Can be used by mods manually to f.e. activate / disable functionality if an API's (not) existing.
    /// Currently only checks the backends.
    /// </summary>
    /// <param name="dependency">Dependency to check for. Name and Version will be checked.</param>
    /// <returns></returns>
    public static bool DependencyLoaded(ETGModuleMetadata dependency) {
        string dependencyName = dependency.Name;
        Version dependencyVersion = dependency.Version;

        if (dependencyName == "Base") {
            if (BaseVersion.Major != dependencyVersion.Major) {
                return false;
            }
            if (BaseVersion.Minor < dependencyVersion.Minor) {
                return false;
            }
            return true;
        }

        return false;
    }

    [Obsolete("Use RunHook instead!")]
    public static T RunHooks<T>(this MulticastDelegate md, T val) {
        if (md == null) {
            return val;
        }

        object[] args = { val };

        Delegate[] ds = md.GetInvocationList();
        for (int i = 0; i < ds.Length; i++) {
            args[0] = ds[i].DynamicInvoke(args);
        }

        return (T) args[0];
    }

    /// <summary>
    /// Invokes all delegates in the invocation list, passing on the result to the next.
    /// </summary>
    /// <typeparam name="T">Type of the result.</typeparam>
    /// <param name="md">The multicast delegate.</param>
    /// <param name="val">The initial value.</param>
    /// <param name="args">Any other arguments that may be passed.</param>
    /// <returns>The result of all delegates, or the initial value if md == null.</returns>
    public static T RunHook<T>(this MulticastDelegate md, T val, params object[] args) {
        if (md == null) {
            return val;
        }

        object[] args_ = new object[args.Length + 1];
        args_[0] = val;
        Array.Copy(args, 0, args_, 1, args.Length);

        Delegate[] ds = md.GetInvocationList();
        for (int i = 0; i < ds.Length; i++) {
            args_[0] = ds[i].DynamicInvoke(args_);
        }

        return (T) args_[0];
    }
        
    /// <summary>
    /// Calls a method in every module.
    /// </summary>
    /// <param name="methodName">Method name of the method to call.</param>
    /// <param name="args">Arguments to pass - null for none.</param>
    public static void CallInEachModule(string methodName, object[] args = null) 
    {
        Type[] argsTypes;
        if (args == null) 
        {
            args = Array<object>.Empty;
            argsTypes = Type.EmptyTypes;
        }
        else
        {
            argsTypes = Type.GetTypeArray(args);
        }

        for (int i = 0; i < _ModuleTypes.Count; i++) {
            Dictionary<string, MethodInfo> moduleMethods = _ModuleMethods[i];
            MethodInfo method;
            if (moduleMethods.TryGetValue(methodName, out method)) {
                if (method == null) {
                    continue;
                }
                ReflectionHelper.InvokeMethod(method, AllMods[i], args);
                continue;
            }

            method = _ModuleTypes[i].GetMethod(methodName, argsTypes);
            moduleMethods[methodName] = method;
            if (method == null) {
                continue;
            }
            ReflectionHelper.InvokeMethod(method, AllMods[i], args);
        }
    }

    /// <summary>
    /// Calls a method in every module, passing down the result to the next call.
    /// </summary>
    /// <typeparam name="T">Type of the result.</typeparam>
    /// <param name="methodName">Method name of the method to call.</param>
    /// <param name="arg">Argument to pass.</param>
    public static T CallInEachModule<T>(string methodName, T arg) {
        Type[] argsTypes = { typeof(T) };
        T[] args = { arg };
        for (int i = 0; i < AllMods.Count; i++) {
            ETGModule module = AllMods[i];
            //TODO use module method cache
            MethodInfo method = module.GetType().GetMethod(methodName, argsTypes);
            if (method == null) {
                continue;
            }
            args[0] = (T) ReflectionHelper.InvokeMethod(method, module, args);
        }
        return args[0];
    }

    private static void CallInEachModule(Action<ETGModule> moduleAction)
    {
        if (moduleAction == null)
            return;

        foreach (var mod in AllMods)
        {
            moduleAction(mod);
        }
    }

    public class Profile {
        public readonly int Id;
        public readonly string Name;

        public Profile(int id, string name) {
            Id = id;
            Name = name;
        }

        public bool RunsOn(Profile p) {
            return Id <= p.Id;
        }
        public bool Runs() {
            return RunsOn(BaseProfile);
        }

        public override bool Equals(object obj) {
            Profile p = obj as Profile;
            if (p == null) {
                return false;
            }
            return p.Id == Id;
        }

        public override int GetHashCode() {
            return Id;
        }

        public static bool operator <(Profile a, Profile b) {
            if ((a == null) || (b == null)) {
                return false;
            }
            return a.Id < b.Id;
        }
        public static bool operator >(Profile a, Profile b) {
            if ((a == null) || (b == null)) {
                return false;
            }
            return a.Id > b.Id;
        }

        public static bool operator <=(Profile a, Profile b) {
            if ((a == null) || (b == null)) {
                return false;
            }
            return a.Id <= b.Id;
        }
        public static bool operator >=(Profile a, Profile b) {
            if ((a == null) || (b == null)) {
                return false;
            }
            return a.Id >= b.Id;
        }

        public static bool operator ==(Profile a, Profile b) {
            if ((a == null) || (b == null)) {
                return false;
            }
            return a.Id == b.Id;
        }
        public static bool operator !=(Profile a, Profile b) {
            return !(a == b);
        }
    }
}

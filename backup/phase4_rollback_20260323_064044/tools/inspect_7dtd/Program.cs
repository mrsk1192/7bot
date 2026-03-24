using System.Reflection;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: inspect_7dtd <managed-dir>");
    return 1;
}

var managedDir = Path.GetFullPath(args[0]);
if (!Directory.Exists(managedDir))
{
    Console.Error.WriteLine("Managed directory not found: " + managedDir);
    return 1;
}

AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) =>
{
    var shortName = new AssemblyName(eventArgs.Name).Name;
    if (string.IsNullOrWhiteSpace(shortName))
    {
        return null;
    }

    var candidate = Path.Combine(managedDir, shortName + ".dll");
    return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
};

var asm = Assembly.LoadFrom(Path.Combine(managedDir, "Assembly-CSharp.dll"));
Type[] types;
try
{
    types = asm.GetTypes();
}
catch (ReflectionTypeLoadException exception)
{
    types = exception.Types.Where(type => type != null).Cast<Type>().ToArray();
}

var tokens = new[]
{
    "MainMenu",
    "Continue",
    "LoadGame",
    "StartGame",
    "GameManager",
    "ConnectionManager",
    "SaveData",
    "Menu"
};

foreach (var token in tokens)
{
    Console.WriteLine("=== TOKEN " + token + " ===");
    foreach (var type in types
        .Where(type => (type.FullName ?? string.Empty).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
        .OrderBy(type => type.FullName)
        .Take(50))
    {
        Console.WriteLine(type.FullName);
    }

    Console.WriteLine();
}

foreach (var exactName in new[] { "XUiM_MainMenu", "XUiC_NewContinueGame", "XUiC_MainMenu", "GameManager", "ConnectionManager", "GamePrefs", "EnumGamePrefs" })
{
    var type = types.FirstOrDefault(candidate => candidate.Name == exactName);
    if (type == null)
    {
        Console.WriteLine("TYPE NOT FOUND: " + exactName);
        Console.WriteLine();
        continue;
    }

    Console.WriteLine("=== METHODS " + type.FullName + " ===");
    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    var methods = type.Name == "GamePrefs"
        ? type.GetMethods(flags).OrderBy(method => method.Name)
        : type.GetMethods(flags).Where(method => IsInteresting(method.Name)).OrderBy(method => method.Name);
    foreach (var method in methods)
    {
        Console.WriteLine($"{method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name + " " + parameter.Name))})");
    }

    if (type.Name == "EnumGamePrefs")
    {
        foreach (var name in Enum.GetNames(type))
        {
            Console.WriteLine("ENUM " + name);
        }
    }

    Console.WriteLine();
}

return 0;

static bool IsInteresting(string name)
{
    return name.IndexOf("continue", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("load", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("game", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("start", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("join", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("host", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("single", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("open", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("show", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("init", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("activate", StringComparison.OrdinalIgnoreCase) >= 0;
}

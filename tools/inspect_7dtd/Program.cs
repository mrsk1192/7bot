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

DumpNamedType(asm, "PlayerInputManager");
DumpNamedType(asm, "PlayerActionsLocal");
DumpNamedType(asm, "PlayerActionsBase");
DumpNamedType(asm, "PlayerActionSet");
DumpNamedType(asm, "PlayerAction");
DumpNamedType(asm, "PlayerMoveController");
DumpNamedType(asm, "vp_FPInput");
DumpNamedType(asm, "AvatarLocalPlayerController");
DumpNamedType(asm, "EntityPlayerLocal");
DumpMatchingTypes(asm, "Input");
DumpMatchingTypes(asm, "Move");

return 0;

static void DumpNamedType(Assembly asm, string typeName)
{
    var type = asm.GetType(typeName);
    if (type == null)
    {
        Console.WriteLine("TYPE NOT FOUND: " + typeName);
        Console.WriteLine();
        return;
    }

    DumpType(type);
}

static void DumpMatchingTypes(Assembly asm, string token)
{
    var matches = asm.GetTypes()
        .Where(t => t.FullName != null && t.FullName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
        .OrderBy(t => t.FullName)
        .Take(30)
        .ToList();

    foreach (var type in matches)
    {
        DumpType(type);
    }
}

static void DumpType(Type type)
{
    Console.WriteLine("TYPE: " + type.FullName);
    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    foreach (var member in type.GetMembers(flags)
        .Where(m => IsInteresting(m.Name))
        .OrderBy(m => m.MemberType)
        .ThenBy(m => m.Name))
    {
        Console.WriteLine("  " + Describe(member));
    }

    Console.WriteLine();
}

static bool IsInteresting(string name)
{
    return name.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("move", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("look", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("jump", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("crouch", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("sprint", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("action", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("interact", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("inventory", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("flash", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("reload", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("slot", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("toolbar", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("hotbar", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("console", StringComparison.OrdinalIgnoreCase) >= 0;
}

static string Describe(MemberInfo member)
{
    return member switch
    {
        PropertyInfo property => $"[Property] {property.PropertyType.Name} {property.Name}",
        FieldInfo field => $"[Field] {field.FieldType.Name} {field.Name}",
        MethodInfo method => $"[Method] {method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})",
        _ => $"[{member.MemberType}] {member.Name}"
    };
}

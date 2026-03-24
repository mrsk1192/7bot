using System.Reflection;
var managed = @"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\7DaysToDie_Data\Managed";
AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
    var name = new AssemblyName(e.Name).Name;
    var path = Path.Combine(managed, name + ".dll");
    return File.Exists(path) ? Assembly.LoadFrom(path) : null;
};
var asm = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
Type[] types;
try { types = asm.GetTypes(); }
catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).Cast<Type>().ToArray(); }
foreach (var token in new[]{"Inventory","WindowManager","PlayerUI","NGUIWindow","Quest","Map","Console"})
{
    Console.WriteLine($"=== TOKEN {token} ===");
    foreach (var t in types.Where(t => (t.FullName ?? "").Contains(token, StringComparison.OrdinalIgnoreCase)).OrderBy(t => t.FullName).Take(40))
        Console.WriteLine(t.FullName);
}
Console.WriteLine("=== GUIWindowManager exact ===");
var gwm = types.FirstOrDefault(t => t.Name == "GUIWindowManager");
if (gwm != null)
{
    var flags = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
    foreach (var m in gwm.GetMembers(flags).Where(m => m.Name.Contains("Window", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Open", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Close", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Show", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Hide", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Map", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Quest", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Console", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Menu", StringComparison.OrdinalIgnoreCase)).OrderBy(m => m.MemberType).ThenBy(m => m.Name))
        Console.WriteLine($"{m.MemberType} {m.Name}");
}
Console.WriteLine("=== Inventory exact ===");
var inv = types.FirstOrDefault(t => t.Name == "Inventory");
if (inv != null)
{
    var flags = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
    foreach (var m in inv.GetMembers(flags).Where(m => m.Name.Contains("hold", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("slot", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("select", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("item", StringComparison.OrdinalIgnoreCase)).OrderBy(m => m.MemberType).ThenBy(m => m.Name))
        Console.WriteLine($"{m.MemberType} {m.Name}");
}

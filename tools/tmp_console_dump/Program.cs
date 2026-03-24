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
foreach (var t in types.Where(t => (t.FullName ?? "").Contains("Console", StringComparison.OrdinalIgnoreCase)).OrderBy(t => t.FullName).Take(80))
{
    Console.WriteLine("TYPE: " + t.FullName);
    var flags = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
    foreach (var m in t.GetMembers(flags).Where(m => m.Name.Contains("Open", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Close", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Toggle", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Show", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Hide", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Console", StringComparison.OrdinalIgnoreCase)).OrderBy(m => m.MemberType).ThenBy(m => m.Name).Take(20))
    {
        if (m is MethodInfo mi) Console.WriteLine($"  Method {mi.ReturnType.Name} {mi.Name}({string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
        else Console.WriteLine($"  {m.MemberType} {m.Name}");
    }
}

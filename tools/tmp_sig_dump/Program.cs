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
var t = types.FirstOrDefault(x => x.Name == "GUIWindowManager");
if (t == null) return;
var flags = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
foreach (var m in t.GetMethods(flags).Where(m => new[]{"Open","OpenIfNotOpen","Close","CloseIfOpen","GetWindow","HasWindow","IsWindowOpen"}.Contains(m.Name)).OrderBy(m => m.Name))
{
    Console.WriteLine($"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName + " " + p.Name))})");
}

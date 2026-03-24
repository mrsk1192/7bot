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
var en = types.FirstOrDefault(t => t.Name == "EnumNGUIWindow");
if (en == null) { Console.WriteLine("EnumNGUIWindow not found"); return; }
foreach (var n in Enum.GetNames(en)) Console.WriteLine(n);

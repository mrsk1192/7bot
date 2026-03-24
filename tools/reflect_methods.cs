using System;
using System.IO;
using System.Linq;
using System.Reflection;

var baseDir = @"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\7DaysToDie_Data\Managed";
AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (s, e) => {
    var name = new AssemblyName(e.Name).Name + ".dll";
    var path = Path.Combine(baseDir, name);
    return File.Exists(path) ? Assembly.ReflectionOnlyLoadFrom(path) : null;
};
var asm = Assembly.ReflectionOnlyLoadFrom(Path.Combine(baseDir, "Assembly-CSharp.dll"));
foreach (var typeName in new[]{"XUiC_NewContinueGame","XUiC_MainMenuButtons"}) {
    var t = asm.GetType(typeName, false, false);
    Console.WriteLine("TYPE=" + typeName + " FOUND=" + (t != null));
    if (t == null) continue;
    foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).OrderBy(m => m.Name)) {
        Console.WriteLine(m.Name + " (" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
    }
}

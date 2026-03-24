using System;
using System.Linq;
using System.Reflection;
class P {
  static void Main() {
    var asm = Assembly.LoadFrom(@"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\7DaysToDie_Data\Managed\Assembly-CSharp.dll");
    foreach (var name in new[]{"XUiC_NewContinueGame","XUiC_MainMenu","XUi"}) {
      var t = asm.GetType(name);
      Console.WriteLine("TYPE="+name);
      if (t==null){Console.WriteLine("  <null>"); continue;}
      foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).OrderBy(m=>m.Name)) {
        Console.WriteLine($"  M {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p=>$"{p.ParameterType.Name} {p.Name}"))})");
      }
      foreach (var f in t.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).OrderBy(f=>f.Name)) {
        Console.WriteLine($"  F {f.FieldType.Name} {f.Name}");
      }
      foreach (var p in t.GetProperties(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).OrderBy(p=>p.Name)) {
        Console.WriteLine($"  P {p.PropertyType.Name} {p.Name}");
      }
    }
  }
}

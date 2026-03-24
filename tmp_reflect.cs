using System;
using System.Reflection;
class P {
  static void Main() {
    try {
      var asm = Assembly.LoadFrom(@"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\7DaysToDie_Data\Managed\Assembly-CSharp.dll");
      foreach (var t in asm.GetTypes()) {
        if (t.FullName == "EntityPlayerLocal" || t.FullName == "World" || t.FullName == "GameManager") {
          Console.WriteLine("TYPE "+t.FullName);
          foreach (var m in t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)) {
            if (m.Name.IndexOf("Biome", StringComparison.OrdinalIgnoreCase) >= 0) Console.WriteLine("M "+m);
          }
          foreach (var f in t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)) {
            if (f.Name.IndexOf("Biome", StringComparison.OrdinalIgnoreCase) >= 0) Console.WriteLine("F "+f.FieldType+" "+f.Name);
          }
          foreach (var p in t.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)) {
            if (p.Name.IndexOf("Biome", StringComparison.OrdinalIgnoreCase) >= 0) Console.WriteLine("P "+p.PropertyType+" "+p.Name);
          }
        }
      }
    } catch (Exception ex) { Console.WriteLine(ex); }
  }
}

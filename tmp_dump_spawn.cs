using System;
using System.IO;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        var asmPath = @"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\7DaysToDie_Data\Managed\Assembly-CSharp.dll";
        var asm = Assembly.LoadFrom(asmPath);
        
        var logger = new StreamWriter("C:\\AI\\7agent2\\signature_dump.txt");
        
        var gmType = asm.GetTypes().FirstOrDefault(t => t.Name == "GameManager");
        if (gmType != null) {
            logger.WriteLine("--- GameManager Methods ---");
            var methods = gmType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var m in methods) {
                if (m.Name.Contains("Spawn")) {
                    logger.WriteLine(string.Format("{0}({1})", m.Name, string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray())));
                }
            }
        }
        
        var xuiWindow = asm.GetTypes().FirstOrDefault(t => t.Name == "XUiC_SpawnSelectionWindow");
        if (xuiWindow != null) {
            logger.WriteLine("--- XUiC_SpawnSelectionWindow ---");
            var methods = xuiWindow.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var m in methods) {
                logger.WriteLine(string.Format("{0}({1})", m.Name, string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray())));
            }
        }
        
        logger.Close();
    }
}

using System;
using System.Reflection;

class Program
{
    static void Main()
    {
        var arrayType = typeof(int[]);
        Console.WriteLine($"Type: {arrayType.Name}");
        Console.WriteLine("\nAll public instance methods:");

        var methods = arrayType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        foreach (var m in methods)
        {
            if (m.Name == "Get" || m.Name == "Set" || m.Name == "Address")
            {
                Console.WriteLine($"  {m.Name}:");
                Console.WriteLine($"    DeclaringType: {m.DeclaringType?.FullName}");
                Console.WriteLine($"    IsSpecialName: {m.IsSpecialName}");
                Console.WriteLine($"    DeclaringType == typeof(Array): {m.DeclaringType == typeof(Array)}");
                Console.WriteLine($"    DeclaringType == typeof(object): {m.DeclaringType == typeof(object)}");
            }
        }
    }
}

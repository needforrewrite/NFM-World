using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

// ===================================================================
// Two-level interface inheritance: IDog : IBaseAnimal
// Simulates IInGameCar : ICar pattern (base NOT LuaVisible, derived IS)
// ===================================================================

public interface IBaseAnimal
{
    [LuaName] string Name { get; set; }
    [LuaName] int Age { get; set; }
}

[LuaVisible]
public partial interface IDog : IBaseAnimal
{
    [LuaName] string Breed { get; set; }
}

/// <summary>Concrete impl of IDog — NOT LuaVisible, relies on IDog's metatable.</summary>
public class Dog : IDog
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Breed { get; set; } = "";
}

// ===================================================================
// Three-level interface inheritance: ICar : IVehicle : ITransform
// All base interfaces NOT LuaVisible, only the leaf IS.
// ===================================================================

public interface IFixtureTransform
{
    [LuaName] double X { get; set; }
    [LuaName] double Y { get; set; }
    [LuaName] double Z { get; set; }
}

public interface IFixtureVehicle : IFixtureTransform
{
    [LuaName] int Speed { get; set; }
    [LuaName] string? DriverName { get; set; }
}

[LuaVisible]
public partial interface IFixtureCar : IFixtureVehicle
{
    [LuaName] string Model { get; set; }
    [LuaName] bool IsElectric { get; set; }
}

/// <summary>Concrete impl of IFixtureCar — NOT LuaVisible, relies on IFixtureCar's metatable.</summary>
public class FixtureCar : IFixtureCar
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public int Speed { get; set; }
    public string? DriverName { get; set; }
    public string Model { get; set; } = "";
    public bool IsElectric { get; set; }
}

// ===================================================================
// Interface inheritance with methods
// ===================================================================

public interface IHasName
{
    [LuaName] string GetName();
    [LuaName] void SetName(string name);
}

public interface IHasAge
{
    [LuaName] int GetAge();
    [LuaName] void SetAge(int age);
}

[LuaVisible]
public partial interface IPerson : IHasName, IHasAge
{
    [LuaName] string? Email { get; set; }
}

public class Person : IPerson
{
    private string _name = "";
    private int _age;

    public string? Email { get; set; }

    public string GetName() => _name;
    public void SetName(string name) => _name = name;
    public int GetAge() => _age;
    public void SetAge(int age) => _age = age;
}

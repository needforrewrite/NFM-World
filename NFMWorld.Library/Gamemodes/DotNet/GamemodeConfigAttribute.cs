namespace NFMWorldLibrary.Gamemodes.DotNet;

[AttributeUsage(AttributeTargets.Assembly)]
public class GamemodeAttribute : Attribute
{
    public required string Name { get; set; }
    public required string Description { get; set; }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class GamemodeConfigPropertyAttribute : Attribute
{
    public required string Name { get; set; }
    public required Type Type { get; set; }
    public required string Label { get; set; }
    public required string Description { get; set; }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class GamemodeConfigPropertyOptionAttribute(string property) : Attribute
{
    public string Property { get; } = property;
    public required string Label { get; set; }
    public required string Value { get; set; }
}

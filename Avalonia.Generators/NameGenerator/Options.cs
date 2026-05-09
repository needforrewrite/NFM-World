namespace Avalonia.Generators.NameGenerator;

internal enum Behavior
{
    OnlyProperties = 0,
    InitializeComponent = 1,
    WithXamlXCompilation = 2,
}

internal enum ViewFileNamingStrategy
{
    ClassName = 0,
    NamespaceAndClassName = 1,
}

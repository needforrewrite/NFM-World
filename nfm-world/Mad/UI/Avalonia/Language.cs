// ReSharper disable once CheckNamespace
namespace Avalonia.Metadata;

public interface IAddChild
{
    void AddChild(object child);
}

public interface IAddChild<T> : IAddChild
{
    void AddChild(T child);
}

/// <summary>
/// Defines the property that contains the object's content in markup.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ContentAttribute : Attribute;

/// <summary>
/// Indicates that a collection type should be processed as being whitespace significant by a XAML processor.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class WhitespaceSignificantCollectionAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class TrimSurroundingWhitespaceAttribute : Attribute;

/// <summary>
/// Maps an XML namespace to a CLR namespace for use in XAML.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class XmlnsDefinitionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XmlnsDefinitionAttribute"/> class.
    /// </summary>
    /// <param name="xmlNamespace">The URL of the XML namespace.</param>
    /// <param name="clrNamespace">The CLR namespace.</param>
    public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace)
    {
        XmlNamespace = xmlNamespace;
        ClrNamespace = clrNamespace;
    }

    /// <summary>
    /// Gets or sets the URL of the XML namespace.
    /// </summary>
    public string XmlNamespace { get; }

    /// <summary>
    /// Gets or sets the CLR namespace.
    /// </summary>
    public string ClrNamespace { get; }
}


/// <summary>
/// Marks a class as usable during XAML initialization.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class UsableDuringInitializationAttribute : Attribute
{
    public UsableDuringInitializationAttribute(bool usable)
    {
            
    }
}

/// <summary>
/// Defines the property that contains the object's content in markup.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TemplateContentAttribute : Attribute
{
    public Type? TemplateResultType { get; set; }
}
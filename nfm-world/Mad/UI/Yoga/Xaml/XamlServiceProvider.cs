using Avalonia.Markup.Xaml;

namespace NFMWorld.UI.Yoga.Xaml;

/// <summary>
/// Simple service provider implementation for XAML runtime.
/// </summary>
public class XamlServiceProvider(IRootObjectProvider rootObjectProvider, IUriContext uriContext, IProvideValueTarget provideValueTarget) : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IRootObjectProvider))
            return rootObjectProvider;
        if (serviceType == typeof(IUriContext))
            return uriContext;
        if (serviceType == typeof(IProvideValueTarget))
            return provideValueTarget;
        return null;
    }
}

/// <summary>
/// Root object provider implementation.
/// </summary>
public class RootObjectProviderImpl : IRootObjectProvider
{
    public object? RootObject { get; set; }
    public object? IntermediateRootObject { get; set; }
}

/// <summary>
/// URI context implementation.
/// </summary>
public class UriContextImpl : IUriContext
{
    public Uri? BaseUri { get; set; }
}

/// <summary>
/// Provide value target implementation.
/// </summary>
public class ProvideValueTargetImpl : IProvideValueTarget
{
    public object? TargetObject { get; set; }
    public object? TargetProperty { get; set; }
}

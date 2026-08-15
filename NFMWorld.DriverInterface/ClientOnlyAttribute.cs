namespace NFMWorld.DriverInterface;

/// <summary>
/// Methods with this attribute can only be called from within NFMWorld assembly or with
/// <see cref="ClientServer.RunIfOnClient"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public class ClientOnlyAttribute : Attribute;
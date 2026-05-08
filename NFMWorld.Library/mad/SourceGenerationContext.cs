using System.Text.Json.Serialization;
using NFMWorldLibrary.Rad;

namespace NFMWorldLibrary;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Rad3dWheelDef))]
[JsonSerializable(typeof(Rad3dRimsDef))]
[JsonSerializable(typeof(Rad3dBoxDef))]
[JsonSerializable(typeof(Rad3dPoly))]
[JsonSerializable(typeof(Rad3dWheelDef[]))]
[JsonSerializable(typeof(Rad3dRimsDef[]))]
[JsonSerializable(typeof(Rad3dBoxDef[]))]
[JsonSerializable(typeof(Rad3dPoly[]))]
[JsonSerializable(typeof(Rad3d))]
[JsonSerializable(typeof(CarStats))]
[JsonSerializable(typeof(Color3))]
[JsonSerializable(typeof(Int3))]
[JsonSerializable(typeof(Vector3))]
[JsonSerializable(typeof(Color3[]))]
[JsonSerializable(typeof(Int3[]))]
[JsonSerializable(typeof(Vector3[]))]
[JsonSerializable(typeof(Vector2[]))]
public partial class SourceGenerationContext : JsonSerializerContext;
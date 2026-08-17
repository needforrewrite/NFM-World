using Lua;
using Lua.Standard;
using NFMWorld.LuaSourceGenerator.Generator;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Gamemodes.Lua;

public class LuaGamemodeConfig
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public List<LuaGamemodeProperty> Properties { get; set; } = [];

    public static LuaGamemodeConfig LoadConfig(string path)
    {
        var state = LuaState.Create(LuaNfmwPlatform.Instance);
        state.OpenStandardLibraries();
        LuaVisibleTypeRegistry.RegisterAll(state);

        LuaGamemodeConfig? config = null;
        RegisterFunction("DefineGamemodeConfig", (context, ct) =>
        {
            var table = context.GetArgument<LuaTable>(0);

            config = MarshalConfig(table);

            return ValueTask.FromResult(context.Return());
        });

        state.DoFile($"data/gamemodes/{path}/config.lua");

        return config ?? new LuaGamemodeConfig()
        {
            Name = "N/A",
            Description = "N/A"
        };

        void RegisterFunction(string name, Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> fn)
            => state!.Environment[name] = new LuaFunction(name, fn);

        LuaValue[] Call(string name, params ReadOnlySpan<LuaValue> arguments)
        {
            if (state == null ||
                !state.Environment.TryGetValue(name, out var value) ||
                !value.TryRead<LuaFunction>(out var function))
            {
                return [LuaValue.Nil];
            }

            try
            {
                return state.Call(function, arguments);
            }
            catch (Exception ex)
            {
                Logging.Error($"[LuaGamemode:{path}] {name} failed: {ex.Message}");
            }
            return [LuaValue.Nil];
        }
    }

    private static LuaGamemodeConfig MarshalConfig(LuaTable table)
    {
        table.TryGetValue("name", out var name);
        table.TryGetValue("description", out var description);

        var config = new LuaGamemodeConfig
        {
            Name = name.ToString(),
            Description = description.ToString()
        };

        if (table.TryGetValue("properties", out var properties) && properties.TryRead<LuaTable>(out var propertiesTable))
        {
            foreach (var (i, prop) in propertiesTable)
            {
                if (prop.TryRead<LuaTable>(out var propTable))
                {
                    propTable.TryGetValue("name", out var propName);
                    propTable.TryGetValue("type", out var propType);
                    propTable.TryGetValue("description", out var propDescription);

                    var propValue = new LuaGamemodeProperty
                    {
                        Name = propName.ToString(),
                        Type = Enum.TryParse<LuaGamemodePropertyType>(propType.ToString(), true, out var parsed)
                            ? parsed
                            : default,
                        Description = propDescription.ToString()
                    };
                    config.Properties.Add(propValue);

                    if (propTable.TryGetValue("options", out var options) &&
                        options.TryRead<LuaTable>(out var optionsTable))
                    {
                        foreach (var (j, option) in optionsTable)
                        {
                            if (option.TryRead<LuaTable>(out var optionTable))
                            {
                                optionTable.TryGetValue("label", out var optionLabel);
                                optionTable.TryGetValue("value", out var optionValue);
                                propValue.Options.Add(new LuaGamemodePropertyOption
                                {
                                    Label = optionLabel.ToString(),
                                    Value = optionValue.Read<object>()
                                });
                            }
                        }
                    }
                }
            }
        }

        return config;
    }

    /// <summary>
    /// Marshals this config back into a <see cref="LuaTable"/>, mirroring the shape
    /// expected by <see cref="MarshalConfig"/>.
    /// </summary>
    public LuaTable ToLuaTable()
    {
        var table = new LuaTable
        {
            ["name"] = Name,
            ["description"] = Description
        };

        var properties = new LuaTable();
        var propertyIndex = 1;
        foreach (var property in Properties)
        {
            var propertyTable = new LuaTable
            {
                ["name"] = property.Name,
                ["type"] = property.Type.ToString()
            };

            if (property.Label is not null)
            {
                propertyTable["label"] = property.Label;
            }

            if (property.Description is not null)
            {
                propertyTable["description"] = property.Description;
            }

            if (property.Options.Count > 0)
            {
                var options = new LuaTable();
                var optionIndex = 1;
                foreach (var option in property.Options)
                {
                    options[optionIndex++] = new LuaTable
                    {
                        ["label"] = option.Label,
                        ["value"] = LuaValue.FromObject(option.Value)
                    };
                }

                propertyTable["options"] = options;
            }

            properties[propertyIndex++] = propertyTable;
        }

        table["properties"] = properties;

        return table;
    }
}

public class LuaGamemodeProperty
{
    /// <summary>
    /// Lua name of this property.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Lua type of this property.
    /// </summary>
    public required LuaGamemodePropertyType Type { get; set; }

    /// <summary>
    /// Display name for this property.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Display description for this property.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets gamemode options to be shown as a dropdown.
    /// </summary>
    public List<LuaGamemodePropertyOption> Options { get; set; } = [];
}

public class LuaGamemodePropertyOption
{
    /// <summary>
    /// The display name of this option.
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// The Lua value of this option. String, double or boolean.
    /// </summary>
    public required object Value { get; set; }
}

public enum LuaGamemodePropertyType
{
    String, Number, Boolean
}
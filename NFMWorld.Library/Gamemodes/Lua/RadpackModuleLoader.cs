using Lua;

namespace NFMWorldLibrary.Gamemodes.Lua;

public class RadpackModuleLoader(Dictionary<string, string> files) : ILuaModuleLoader
{
    public bool Exists(string moduleName)
    {
        return files.ContainsKey(moduleName);
    }

    public ValueTask<LuaModule> LoadAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        if (files.TryGetValue(moduleName, out var contents))
        {
            return ValueTask.FromResult(new LuaModule(moduleName, contents));
        }
            
        throw new LuaModuleNotFoundException(moduleName);
    }
}
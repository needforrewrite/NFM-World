using nfm_world_library.Lua;

namespace NFMWorld.Library.Test;

[TestClass]
public sealed class LuaManagerTests
{
    [TestMethod]
    public void TestLoadLuaWithContext()
    {
        LuaManager.InitializeLua();
        var table = LuaManager.LoadLuaWithContext("sample.lua", new Dictionary<string, object>()
        {
            { "key1", "value1" },
            { "key2", 42 },
            { "key3", true }
        });

        Assert.IsInstanceOfType<LuaFunction>(table["f"]);
    }
}
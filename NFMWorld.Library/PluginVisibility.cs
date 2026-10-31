using FixedMathSharp;
using FixedMathSharp.Utility;
using NFMWorld.DriverInterface;
using NFMWorld.PluginSystem;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;

// Vendor types reachable from the plugin-visible surface (BackendCar/CarPhysics/Control fields).
[assembly: VendorPluginVisibleAttribute<fix64>(Visible.IncludingAllMembersOfType)]
[assembly: VendorPluginVisibleAttribute<f64Vector3>(Visible.IncludingAllMembersOfType)]
[assembly: VendorPluginVisibleAttribute<DeterministicRandom>(Visible.IncludingAllMembersOfType)]
[assembly: VendorPluginVisibleAttribute(typeof(FixedMath), Visible.IncludingAllMembersOfType)]

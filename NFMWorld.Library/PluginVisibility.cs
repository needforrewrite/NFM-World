using FixedMathSharp;
using FixedMathSharp.Utility;
using Maxine.Extensions.Collections;
using NFMWorld.DriverInterface;
using NFMWorld.PluginSystem;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;

// Vendor types reachable from the plugin-visible surface (BackendCar/CarPhysics/Control fields).
[assembly: VendorPluginVisible<fix64>(Visible.IncludingAllMembersOfType)]
[assembly: VendorPluginVisible<f64Vector3>(Visible.IncludingAllMembersOfType)]
[assembly: VendorPluginVisible<DeterministicRandom>(Visible.IncludingAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(FixedMath), Visible.IncludingAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray2<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray3<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray4<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray5<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray6<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray7<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray8<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray9<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray10<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray11<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray12<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray13<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray14<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray15<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]
[assembly: VendorPluginVisible(typeof(SafeInlineArray16<>), Visible.IncludingAnyGenericInstantiationAndAllMembersOfType)]

namespace NFMWorld.Mad;

public enum PolyType
{
    // Put glass last so when rendering it is last in the render order due to alpha sorting
    Flat, Light, BrakeLight, ReverseLight, Finish, Fullbright, Glass,
    
    MaxValue = Glass
}
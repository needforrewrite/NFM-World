declare class Color3
    implements System_IEquatable_NFMWorldLibrary_Util_Color3_
{
    r: number;
    g: number;
    b: number;
    darker(): Color3;
    brighter(): Color3;
    static readonly factor: number;
    static fromHSB(hue: number, saturation: number, brightness: number): Color3;
}

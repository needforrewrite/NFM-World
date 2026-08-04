declare class Color3
    implements Color3
{
    r: number;
    g: number;
    b: number;
    darker(): Color3;
    brighter(): Color3;
    static factor: number;
    static fromSpan(span: ReadOnlySpan_short): Color3;
    static fromHSB(hue: number, saturation: number, brightness: number): Color3;
}

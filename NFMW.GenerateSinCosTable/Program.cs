// See https://aka.ms/new-console-template for more information

using NFMWorldLibrary.FixedMath;

var table = new List<long>();
for (int angle = 0; angle < 36000; angle++)
{
    var actualAngle = angle / 100d;
    var sin = Math.Sin(actualAngle * Math.PI / 180);
    Console.WriteLine(((fix64)sin));
    table.Add(((fix64)sin).Raw);
}

using var s = new StreamWriter(File.OpenWrite("SinTable.txt"));
s.WriteLine("private static readonly ReadOnlySpan<long> SinTable => [");
foreach (var value in table)
{
    s.WriteLine($"    {value}L,");
}
s.WriteLine("];");
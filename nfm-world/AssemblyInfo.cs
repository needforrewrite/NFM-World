using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NFMWorld")]
// The browser head compiles WorldGame.cs the same way the desktop exe does, so it reaches the
// same internals that file does.
[assembly: InternalsVisibleTo("NFMWorld.Browser")]
// See https://aka.ms/new-console-template for more information

using Maxine.RustBindGen;
using NFMWorldLibrary;

Console.WriteLine(RustBindGen.GenerateRustBindings(typeof(BackendGameSparker).Assembly));
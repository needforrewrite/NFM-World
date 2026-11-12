namespace TightPack;

/// <summary>Wire-format constants shared by the writer, the reader and generated code.</summary>
public static class TightPackCode
{
    /// <summary>Collection header value that marks a null reference.</summary>
    public const int NullCollection = -1;

    /// <summary>Union header byte that introduces a 16-bit tag.</summary>
    public const byte WideTag = 250;

    /// <summary>Object header byte that marks a null reference.</summary>
    public const byte NullObject = 255;

    /// <summary>Object header value that introduces a reference id.</summary>
    public const byte ReferenceId = 250;

    /// <summary>Maximum object nesting before the serializer assumes a cycle.</summary>
    public const int DepthLimit = 1000;
}

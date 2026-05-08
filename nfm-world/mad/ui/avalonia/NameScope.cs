using Avalonia.Controls;
using NFMWorld.UI.Yoga;

namespace NFMWorld.UI.Avalonia;

public sealed class NameScope(Node node) : INameScope
{
    public object? Find(string name)
    {
        return Find<Node>(name);
    }
    
    /// <summary>
    /// Recursively finds a child node by name.
    /// </summary>
    public T? Find<T>(string name) where T : Node
    {
        return FindChildByNameRecursive<T>(node, name);
    }

    private static T? FindChildByNameRecursive<T>(Node parent, string name) where T : Node
    {
        if (parent is Box box)
        {
            foreach (var child in box.Children)
            {
                if (child.Name == name && child is T typed)
                    return typed;

                var found = FindChildByNameRecursive<T>(child, name);
                if (found != null)
                    return found;
            }
        }

        return null;
    }
}
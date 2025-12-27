using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using SoftFloat;

namespace NFMWorld;

/// <summary>
/// Represents a rectangular boundary for quadtree nodes
/// </summary>
public struct f64Bounds(fix64 x, fix64 y, fix64 width, fix64 height)
{
    public fix64 X = x;
    public fix64 Y = y;
    public fix64 Width = width;
    public fix64 Height = height;

    public readonly fix64 Left => X;
    public readonly fix64 Right => X + Width;
    public readonly fix64 Top => Y;
    public readonly fix64 Bottom => Y + Height;
    public readonly fix64 CenterX => X + Width * fix64.Half;
    public readonly fix64 CenterY => Y + Height * fix64.Half;

    public bool Contains(fix64 px, fix64 py)
    {
        return px >= Left && px <= Right && py >= Top && py <= Bottom;
    }

    public bool Intersects(f64Bounds other)
    {
        return !(other.Left > Right || other.Right < Left || other.Top > Bottom || other.Bottom < Top);
    }
}

/// <summary>
/// Interface for objects that can be stored in a quadtree
/// </summary>
public interface IQuadObject
{
    f64Bounds GetBounds();
}

/// <summary>
/// A node in the quadtree hierarchy
/// </summary>
internal class QuadNode<T>(int level, f64Bounds bounds)
    where T : IQuadObject
{
    private struct QuadNodeArray : IEnumerable<QuadNode<T>>
    {
        private InlineArray4<QuadNode<T>> Nodes;
    
        public QuadNode<T> this[int index]
        {
            get => Nodes[index];
            set => Nodes[index] = value;
        }
    
        public int Length => 4;
        
        public NodesEnumerator GetEnumerator()
        {
            return new NodesEnumerator(this);
        }

        internal struct NodesEnumerator(QuadNodeArray quadNodeArray) : IEnumerator<QuadNode<T>>
        {
            private int _currentIndex = -1;
            
            public bool MoveNext()
            {
                _currentIndex++;
                return _currentIndex < quadNodeArray.Length;
            }

            public void Reset()
            {
                _currentIndex = -1;
            }

            public QuadNode<T> Current => quadNodeArray[_currentIndex];

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }
        }

        IEnumerator<QuadNode<T>> IEnumerable<QuadNode<T>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private const int MaxObjects = 10;
    private const int MaxLevels = 8;

    private f64Bounds _bounds = bounds;
    private readonly List<T> _objects = [];
    private QuadNodeArray? _nodes;

    public void Clear()
    {
        _objects.Clear();

        if (_nodes is {} nodes)
        {
            foreach (var t in nodes)
            {
                t.Clear();
            }

            _nodes = null;
        }
    }

    public void TrimExcess()
    {
        _objects.TrimExcess();
        if (_nodes is {} nodes)
        {
            foreach (var t in nodes)
            {
                t.TrimExcess();
            }
        }
    }

    [MemberNotNull(nameof(_nodes))]
    private QuadNodeArray Split()
    {
        fix64 subWidth = _bounds.Width / fix64.Two;
        fix64 subHeight = _bounds.Height / fix64.Two;
        fix64 x = _bounds.X;
        fix64 y = _bounds.Y;

        var nodes = new QuadNodeArray
        {
            [0] = new QuadNode<T>(level + 1, new f64Bounds(x + subWidth, y, subWidth, subHeight)), // NE
            [1] = new QuadNode<T>(level + 1, new f64Bounds(x, y, subWidth, subHeight)), // NW
            [2] = new QuadNode<T>(level + 1, new f64Bounds(x, y + subHeight, subWidth, subHeight)), // SW
            [3] = new QuadNode<T>(level + 1, new f64Bounds(x + subWidth, y + subHeight, subWidth, subHeight)) // SE
        };
        _nodes = nodes;
        return nodes;
    }

    private int GetIndex(f64Bounds objBounds)
    {
        int index = -1;
        fix64 verticalMidpoint = _bounds.X + (_bounds.Width / fix64.Two);
        fix64 horizontalMidpoint = _bounds.Y + (_bounds.Height / fix64.Two);

        bool topQuadrant = (objBounds.Y < horizontalMidpoint && objBounds.Bottom < horizontalMidpoint);
        bool bottomQuadrant = (objBounds.Y > horizontalMidpoint);

        if (objBounds.X < verticalMidpoint && objBounds.Right < verticalMidpoint)
        {
            if (topQuadrant)
                index = 1; // NW
            else if (bottomQuadrant)
                index = 2; // SW
        }
        else if (objBounds.X > verticalMidpoint)
        {
            if (topQuadrant)
                index = 0; // NE
            else if (bottomQuadrant)
                index = 3; // SE
        }

        return index;
    }

    public void Insert(T obj)
    {
        {
            if (_nodes is { } nodes)
            {
                int index = GetIndex(obj.GetBounds());

                if (index != -1)
                {
                    nodes[index].Insert(obj);
                    return;
                }
            }
        }

        _objects.Add(obj);

        if (_objects.Count > MaxObjects && level < MaxLevels)
        {
            if (_nodes is not {} nodes)
                nodes = Split();

            int i = 0;
            while (i < _objects.Count)
            {
                int index = GetIndex(_objects[i].GetBounds());
                if (index != -1)
                {
                    nodes[index].Insert(_objects[i]);
                    _objects.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }
    }

    public void Retrieve(List<T> returnObjects, f64Bounds area)
    {
        int index = GetIndex(area);
        if (index != -1 && _nodes is {} nodes)
        {
            nodes[index].Retrieve(returnObjects, area);
        }
        else
        {
            if (_nodes is {} nodes1)
            {
                foreach (var t in nodes1)
                {
                    if (t._bounds.Intersects(area))
                        t.Retrieve(returnObjects, area);
                }
            }
        }

        returnObjects.AddRange(_objects);
    }
    
    public IEnumerable<T> RetrieveEnumerable(f64Bounds area)
    {
        int index = GetIndex(area);
        if (index != -1 && _nodes is {} nodes)
        {
            foreach (var obj in nodes[index].RetrieveEnumerable(area))
                yield return obj;
        }
        else
        {
            if (_nodes is {} nodes1)
            {
                foreach (var t in nodes1)
                {
                    if (t._bounds.Intersects(area))
                    {
                        foreach (var obj in t.RetrieveEnumerable(area))
                            yield return obj;
                    }
                }
            }
        }

        foreach (var obj in _objects)
            yield return obj;
    }

    public void RetrievePoint(List<T> returnObjects, fix64 x, fix64 y)
    {
        if (_nodes is {} nodes)
        {
            int index = GetIndex(new f64Bounds(x, y, 0, 0));
            if (index != -1)
            {
                nodes[index].RetrievePoint(returnObjects, x, y);
            }
            else
            {
                foreach (var t in nodes)
                {
                    if (t._bounds.Contains(x, y))
                        t.RetrievePoint(returnObjects, x, y);
                }
            }
        }

        returnObjects.AddRange(_objects);
    }
    
    public IEnumerable<T> RetrievePointEnumerable(fix64 x, fix64 y)
    {
        if (_nodes is {} nodes)
        {
            int index = GetIndex(new f64Bounds(x, y, 0, 0));
            if (index != -1)
            {
                foreach (var obj in nodes[index].RetrievePointEnumerable(x, y))
                    yield return obj;
            }
            else
            {
                foreach (var t in nodes)
                {
                    if (t._bounds.Contains(x, y))
                    {
                        foreach (var obj in t.RetrievePointEnumerable(x, y))
                            yield return obj;
                    }
                }
            }
        }

        foreach (var obj in _objects)
            yield return obj;
    }

    public int CountObjects()
    {
        int count = _objects.Count;
        if (_nodes is {} nodes)
        {
            foreach (var t in nodes)
                count += t.CountObjects();
        }
        return count;
    }

    public void GetAllObjects(List<T> list)
    {
        list.AddRange(_objects);
        if (_nodes is {} nodes)
        {
            foreach (var t in nodes)
                t.GetAllObjects(list);
        }
    }

    public IEnumerable<T> EnumerateObjects()
    {
        foreach (var obj in _objects)
            yield return obj;

        if (_nodes is {} nodes)
        {
            foreach (var t in nodes)
            {
                foreach (var obj in t.EnumerateObjects())
                    yield return obj;
            }
        }
    }
}

/// <summary>
/// Quadtree spatial data structure for efficient 2D collision detection and queries
/// </summary>
public class QuadTree<T> where T : IQuadObject
{
    private QuadNode<T> _root;
    private f64Bounds _bounds;

    public QuadTree(fix64 x, fix64 y, fix64 width, fix64 height)
    {
        _bounds = new f64Bounds(x, y, width, height);
        _root = new QuadNode<T>(0, _bounds);
    }

    public QuadTree(f64Bounds bounds)
    {
        _bounds = bounds;
        _root = new QuadNode<T>(0, _bounds);
    }

    public void Clear()
    {
        _root.Clear();
    }

    public void Insert(T obj)
    {
        _root.Insert(obj);
    }

    public List<T> Retrieve(f64Bounds area)
    {
        var returnObjects = new List<T>();
        _root.Retrieve(returnObjects, area);
        return returnObjects;
    }

    public void Retrieve(List<T> returnObjects, f64Bounds area)
    {
        _root.Retrieve(returnObjects, area);
    }

    public List<T> RetrievePoint(fix64 x, fix64 y)
    {
        var returnObjects = new List<T>();
        _root.RetrievePoint(returnObjects, x, y);
        return returnObjects;
    }

    public void RetrievePoint(List<T> returnObjects, fix64 x, fix64 y)
    {
        _root.RetrievePoint(returnObjects, x, y);
    }

    public int CountObjects()
    {
        return _root.CountObjects();
    }

    public List<T> GetAllObjects()
    {
        var list = new List<T>();
        _root.GetAllObjects(list);
        return list;
    }

    public void Rebuild(IEnumerable<T> objects)
    {
        Clear();
        foreach (var obj in objects)
            Insert(obj);
    }

    public void TrimExcess()
    {
        _root.TrimExcess();
    }

    public f64Bounds Bounds => _bounds;
}

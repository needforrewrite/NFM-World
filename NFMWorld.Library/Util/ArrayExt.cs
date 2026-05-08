namespace NFMWorldLibrary.Util;

public static class ArrayExt
{
    private static readonly System.Random Rng = new();

    public static T[] CloneArray<T>(this T[] arr)
    {
        var to = new T[arr.Length];
        arr.CopyTo(to, 0);
        return to;
    }

    public static T[] Slice<T>(this T[,] arr2, int i)
    {
        var len = arr2.GetLength(1);
        var arr = new T[len];
        for (var j = 0; j < len; j++)
        {
            arr[j] = arr2[i, j];
        }
        return arr;
    }

    public static T[][] New<T>(int l0, int l1)
    {
        var arr = new T[l0][];
        for (var i = 0; i < l0; i++)
        {
            arr[i] = new T[l1];
        }
        return arr;
    }

    public static T[][][] New<T>(int l0, int l1, int l2)
    {
        var arr = new T[l0][][];
        for (var i = 0; i < l0; i++)
        {
            arr[i] = new T[l1][];
            for (var j = 0; j < l0; j++)
            {
                arr[i][j] = new T[l2];
            }
        }
        return arr;
    }

    public static void Sort<T>(T[] arr)
    {
//TODO
        Array.Sort(arr);
    }

    public static void Sort<T>(T[] arr, int from, int to)
    {
        Array.Sort(arr, from, to);
    }

    public static List<T> Shuffle<T>(this List<T> list)
    {
        var n = list.Count;
        while (n > 1)
        {
            n--;
            var k = Rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
        return list;
    }

    public static void Sort<T>(T[] arr, Comparer<T> comparator)
    {
        Array.Sort(arr, comparator);
    }

    public static void Sort<T>(T[,] arr, Comparer<T[]> comparator)
    {
        Array.Sort(arr, comparator);
    }

    ///<summary>Finds the index of the first item matching an expression in an enumerable.</summary>
    ///<param name="items">The enumerable to search.</param>
    ///<param name="predicate">The expression to test the items against.</param>
    ///<returns>The index of the first matching item, or -1 if no items match.</returns>
    public static int FindIndex<T>(this IEnumerable<T> items, Func<T, bool> predicate)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        var retVal = 0;
        foreach (var item in items)
        {
            if (predicate(item))
            {
                return retVal;
            }

            retVal++;
        }
        return -1;
    }

    ///<summary>Finds the index of the first occurrence of an item in an enumerable.</summary>
    ///<param name="items">The enumerable to search.</param>
    ///<param name="target">The item to find.</param>
    ///<returns>The index of the first matching item, or -1 if the item was not found.</returns>
    public static int IndexOf<T>(this IEnumerable<T> items, T target)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var retVal = 0;
        foreach (var item in items)
        {
            if (Equals(item, target))
            {
                return retVal;
            }

            retVal++;
        }
        return -1;
    }
}
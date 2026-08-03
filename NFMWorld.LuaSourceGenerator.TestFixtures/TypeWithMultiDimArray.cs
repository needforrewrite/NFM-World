using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test;

[LuaVisible(Name = "TypeWithMultiDimArray")]
public class TypeWithMultiDimArray
{
    // 2D array property
    public int[,]? Matrix { get; set; }

    // 3D array property
    public float[,,]? Tensor { get; set; }

    // Constructors
    public TypeWithMultiDimArray()
    {
    }

    public TypeWithMultiDimArray(int rows, int cols)
    {
        Matrix = new int[rows, cols];
    }

    public TypeWithMultiDimArray(int[,] matrix)
    {
        Matrix = matrix;
    }

    // Methods
    public void InitializeMatrix(int rows, int cols)
    {
        Matrix = new int[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                Matrix[i, j] = i * cols + j;
            }
        }
    }

    public int GetRows()
    {
        return Matrix?.GetLength(0) ?? 0;
    }

    public int GetCols()
    {
        return Matrix?.GetLength(1) ?? 0;
    }

    public int GetValueAt(int row, int col)
    {
        if (Matrix == null) return -1;
        return Matrix[row, col];
    }

    public void SetValueAt(int row, int col, int value)
    {
        if (Matrix != null)
        {
            Matrix[row, col] = value;
        }
    }

    public int SumAll()
    {
        if (Matrix == null) return 0;
        int sum = 0;
        for (int i = 0; i < Matrix.GetLength(0); i++)
        {
            for (int j = 0; j < Matrix.GetLength(1); j++)
            {
                sum += Matrix[i, j];
            }
        }
        return sum;
    }

    // Static methods
    public static int[,] CreateMatrix(int rows, int cols)
    {
        return new int[rows, cols];
    }

    public static int[,] CreateIdentityMatrix(int size)
    {
        var matrix = new int[size, size];
        for (int i = 0; i < size; i++)
        {
            matrix[i, i] = 1;
        }
        return matrix;
    }
}

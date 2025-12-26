using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class Transform
{
    public IReadOnlyList<GameObject> Children { get; set; } = [];

    public Vector3 Position {
        get;
        set
        {
            MatrixWorldNeedsUpdate = true;
            field = value;
        }
    } = Vector3.Zero;
    public Euler Rotation {
        get;
        set
        {
            MatrixWorldNeedsUpdate = true;
            field = value;
        }
    } = new();

    public Transform? Parent
    {
        get;
        set
        {
            MatrixWorldNeedsUpdate = true;
            field = value;
        }
    }

    public virtual Matrix MatrixWorld
    {
        get
        {
            if (MatrixWorldNeedsUpdate)
            {
                var ownMatrixWorld = Matrix.CreateFromEuler(Rotation) * Matrix.CreateTranslation(Position);
                if (Parent != null)
                {
                    ownMatrixWorld = ownMatrixWorld * Parent.MatrixWorld;
                }

                field = ownMatrixWorld;
                MatrixWorldNeedsUpdate = false;
            }

            return field;
        }
    }

    private bool MatrixWorldNeedsUpdate
    {
        get => field || (Parent?.MatrixWorldNeedsUpdate ?? false);
        set;
    }

    public virtual void GameTick()
    {
    }
}
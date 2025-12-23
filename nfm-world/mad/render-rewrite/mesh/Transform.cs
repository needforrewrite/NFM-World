using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class Transform
{
    public Vector3 Position {
        get;
        set
        {
            _matrixWorldNeedsUpdate = true;
            field = value;
        }
    } = Vector3.Zero;
    public Euler Rotation {
        get;
        set
        {
            _matrixWorldNeedsUpdate = true;
            field = value;
        }
    } = new();

    public Transform? Parent
    {
        get;
        set
        {
            _matrixWorldNeedsUpdate = true;
            field = value;
        }
    }

    public virtual Matrix MatrixWorld
    {
        get
        {
            if (_matrixWorldNeedsUpdate)
            {
                var ownMatrixWorld = Matrix.CreateFromEuler(Rotation) * Matrix.CreateTranslation(Position);
                if (Parent != null)
                {
                    ownMatrixWorld = ownMatrixWorld * Parent.MatrixWorld;
                }

                field = ownMatrixWorld;
                _matrixWorldNeedsUpdate = false;
            }

            return field;
        }
    }

    private bool _matrixWorldNeedsUpdate = true;
    
    public virtual void GameTick()
    {
    }
}
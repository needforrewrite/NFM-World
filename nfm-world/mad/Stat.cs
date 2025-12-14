using System.Numerics;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class Stat
{
    internal readonly int[] Swits;
    internal readonly float[] Acelf;
    internal readonly int Handb;
    internal readonly float Airs;
    internal readonly int Airc;
    internal readonly int Turn;
    internal readonly float Grip;
    internal readonly float Bounce;
    internal readonly float Simag;
    internal readonly float Moment;
    internal readonly float Comprad;
    internal readonly int Push;
    internal readonly float Revpush;
    internal readonly int Lift;
    internal readonly int Revlift;
    internal readonly int Powerloss;
    internal readonly int Flipy;
    internal readonly int Msquash;
    internal readonly int Clrad;
    internal readonly float Dammult;
    internal readonly int Maxmag;
    internal readonly float Dishandle;
    internal readonly float Outdam;
    internal readonly int Cclass;
    internal readonly string Names;
    internal readonly int Enginsignature;

    internal readonly bool Include;
    internal readonly string Createdby = "";
    internal readonly int Publish;

    public Stat(int[] swits, float[] acelf, int handb, float airs, int airc, int turn, float grip, float bounce,
        float simag, float moment, float comprad, int push, float revpush, int lift, int revlift, int powerloss,
        int flipy, int msquash, int clrad, float dammult, int maxmag, float dishandle, float outdam, int cclass,
        string names, int enginsignature,
        bool include, string createdby, int publish) : this(swits,
        acelf,
        handb,
        airs,
        airc,
        turn,
        grip,
        bounce,
        simag,
        moment,
        comprad,
        push,
        revpush,
        lift,
        revlift,
        powerloss,
        flipy,
        msquash,
        clrad,
        dammult,
        maxmag,
        dishandle,
        outdam,
        cclass,
        names,
        enginsignature)
    {
        Include = include;
        Createdby = createdby;
        Publish = publish;
    }

    public Stat(int[] swits, float[] acelf, int handb, float airs, int airc, int turn, float grip, float bounce,
        float simag, float moment, float comprad, int push, float revpush, int lift, int revlift, int powerloss,
        int flipy, int msquash, int clrad, float dammult, int maxmag, float dishandle, float outdam, int cclass,
        string names, int enginsignature)
    {
        Swits = swits.CloneArray();
        Acelf = acelf.CloneArray();

        Handb = handb;
        Airs = airs;
        Airc = airc;
        Turn = turn;
        Grip = grip;
        Bounce = bounce;
        Simag = simag;
        Moment = moment;
        Comprad = comprad;
        Push = push;
        Revpush = revpush;
        Lift = lift;
        Revlift = revlift;
        Powerloss = powerloss;
        Flipy = flipy;
        Msquash = msquash;
        Clrad = clrad;
        Dammult = dammult;
        Maxmag = maxmag;
        Dishandle = dishandle;
        Outdam = outdam;
        Cclass = cclass;
        Names = names;
        Enginsignature = enginsignature;
    }

    public Stat(Stat clone)
    {
        Swits = clone.Swits.CloneArray();
        Acelf = clone.Acelf.CloneArray();

        Handb = clone.Handb;
        Airs = clone.Airs;
        Airc = clone.Airc;
        Turn = clone.Turn;
        Grip = clone.Grip;
        Bounce = clone.Bounce;
        Simag = clone.Simag;
        Moment = clone.Moment;
        Comprad = clone.Comprad;
        Push = clone.Push;
        Revpush = clone.Revpush;
        Lift = clone.Lift;
        Revlift = clone.Revlift;
        Powerloss = clone.Powerloss;
        Flipy = clone.Flipy;
        Msquash = clone.Msquash;
        Clrad = clone.Clrad;
        Dammult = clone.Dammult;
        Maxmag = clone.Maxmag;
        Dishandle = clone.Dishandle;
        Outdam = clone.Outdam;
        Cclass = clone.Cclass;
        Names = clone.Names;
        Enginsignature = clone.Enginsignature;
    }


    public Stat(int cn)
    {
        Swits = CarDefine.Swits.Slice(cn);
        Acelf = CarDefine.Acelf.Slice(cn);

        Handb = CarDefine.Handb[cn];
        Airs = CarDefine.Airs[cn];
        Airc = CarDefine.Airc[cn];
        Turn = CarDefine.Turn[cn];
        Grip = CarDefine.Grip[cn];
        Bounce = CarDefine.Bounce[cn];
        Simag = CarDefine.Simag[cn];
        Moment = CarDefine.Moment[cn];
        Comprad = CarDefine.Comprad[cn];
        Push = CarDefine.Push[cn];
        Revpush = CarDefine.Revpush[cn];
        Lift = CarDefine.Lift[cn];
        Revlift = CarDefine.Revlift[cn];
        Powerloss = CarDefine.Powerloss[cn];
        Flipy = CarDefine.Flipy[cn];
        Msquash = CarDefine.Msquash[cn];
        Clrad = CarDefine.Clrad[cn];
        Dammult = CarDefine.Dammult[cn];
        Maxmag = CarDefine.Maxmag[cn];
        Dishandle = CarDefine.Dishandle[cn];
        Outdam = CarDefine.Outdam[cn];
        Cclass = CarDefine.Cclass[cn];
        Names = CarDefine.Names[cn];
        Enginsignature = CarDefine.Enginsignature[cn];

        if (cn >= CarDefine.Sixteen)
        {
            Include = CarDefine.Include[cn];
            Createdby = CarDefine.Createdby[cn];
            Publish = CarDefine.Publish[cn];
        }
    }

    public float Score
    {
        get
        {
            var score = 0f;
            score += CalculateScoreFromArr(SliceColumn(CarDefine.Swits, 0).ToArray(), Swits[0], 20);
            score += CalculateScoreFromArr(SliceColumn(CarDefine.Acelf, 0).ToArray(), Acelf[0], 20);
            score += CalculateScoreFromArr(SliceColumn(CarDefine.Swits, 1).ToArray(), Swits[1], 30);
            score += CalculateScoreFromArr(SliceColumn(CarDefine.Acelf, 1).ToArray(), Acelf[1], 30);
            score += CalculateScoreFromArr(SliceColumn(CarDefine.Swits, 2).ToArray(), Swits[2], 50);
            score += CalculateScoreFromArr(SliceColumn(CarDefine.Acelf, 2).ToArray(), Acelf[2], 50);
            score += CalculateScoreFromArr(CarDefine.Handb, Handb, 20);
            score += CalculateScoreFromArr(CarDefine.Airs, Airs); 
            score += CalculateScoreFromArr(CarDefine.Airc, Airc, 200);
            score += CalculateScoreFromArr(CarDefine.Turn, Turn, 30);
            score += CalculateScoreFromArr(CarDefine.Grip, Grip, 50);
            score += CalculateScoreFromArr(CarDefine.Bounce, Bounce, 200);
            // score += CalculateScoreFromArr(CarDefine.Simag, Simag);
            score += CalculateScoreFromArr(CarDefine.Moment, Moment, 200);
            score += CalculateScoreFromArr(CarDefine.Comprad, Comprad, 10);
            score += CalculateScoreFromArr(CarDefine.Push, Push, 60);
            score += CalculateScoreFromArr(CarDefine.Revpush, Revpush, -60);
            score += CalculateScoreFromArr(CarDefine.Lift, Lift, 60);
            score += CalculateScoreFromArr(CarDefine.Revlift, Revlift, -60);
            score += CalculateScoreFromArr(CarDefine.Powerloss, Powerloss);
            // score += CalculateScoreFromArr(CarDefine.Flipy, Flipy);
            // score += CalculateScoreFromArr(CarDefine.Msquash, Msquash);
            // score += CalculateScoreFromArr(CarDefine.Clrad, Clrad);
            score += CalculateScoreFromArr(CarDefine.Dammult, Dammult, -50);
            score += CalculateScoreFromArr(CarDefine.Maxmag, Maxmag);
            // score += CalculateScoreFromArr(CarDefine.Dishandle, Dishandle);
            // score += CalculateScoreFromArr(CarDefine.Outdam, Outdam);
            return (score * score * score) / 100_000f;
        }
    }
    // https://stackoverflow.com/a/32652216
    public static IEnumerable<T> SliceRow<T>(T[,] array, int row)
    {
        for (var i = array.GetLowerBound(1); i <= array.GetUpperBound(1); i++)
        {
            yield return array[row, i];
        }
    }

    public static IEnumerable<T> SliceColumn<T>(T[,] array, int column)
    {
        for (var i = array.GetLowerBound(0); i <= array.GetUpperBound(0); i++)
        {
            yield return array[i, column];
        }
    }
    
    static float CalculateScoreFromElement(float min, float max, float actual, float contribution = 100)
    {
        return UMath.InverseLerp(min, max, actual) * contribution;
    }

    static float CalculateScoreFromArr(int[] arr, int value, float contribution = 100)
    {
        var min = arr.Where(x => x != 0).Min();
        var max = arr.Where(x => x != 0).Max();
        return CalculateScoreFromElement(min, max, value, contribution);
    }
    static float CalculateScoreFromArr(float[] arr, float value, float contribution = 100)
    {
        var min = arr.Where(x => x != 0).Min();
        var max = arr.Where(x => x != 0).Max();
        return CalculateScoreFromElement(min, max, value, contribution);
    }
}
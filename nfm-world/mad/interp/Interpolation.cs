namespace NFMWorld.Interp;

public class Interpolation {
    public static int InterpolateCoord(int current, int prev, float ratio) {
        float diff = current - prev;
        float interp = (diff * ratio) + prev;
        return (int)interp;
    }

    public static float InterpolateAngle(float current, float prev, float ratio) {
        float diff = current - prev;
        /*
         * this could go 359->1 or 1->359, so check the size of the movement, if too big
         * then subtract
         */
        if (diff > 270f) {
            diff -= 360f;
        } else if (diff < -270f) {
            diff += 360f;
        }
        float interp = (diff * ratio) + prev;
        return interp;
    }
}
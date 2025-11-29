namespace NFMWorld.Mad.Interp;

public class Interpolation {
    public static int InterpolateCoord(int current, int prev, float ratio) {
        int diff = current - prev;
        int interp = (int) (diff * ratio) + prev;
        return interp;
    }

    public static int InterpolateAngle(int current, int prev, float ratio) {
        int diff = current - prev;
        /*
         * this could go 359->1 or 1->359, so check the size of the movement, if too big
         * then subtract
         */
        if (diff > 270) {
            diff -= 360;
        } else if (diff < -270) {
            diff += 360;
        }
        int interp = (int)((diff * ratio) + prev);
        return interp;
    }
}
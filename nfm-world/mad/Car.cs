namespace NFMWorld.Mad;

public class Car
{
    public ContO Conto;
    public Mad Mad;
    public Stat Stat;
    public Control Control;

    public Car(Stat stat, int im, ContO carConto, int x, int z)
    {
        Conto = new ContO(carConto, x, Medium.Ground, z, 0);
        Mad = new Mad(stat, im);
        Stat = stat;
        Mad.Reseto(im, Conto);
        Control = new Control();
    }

    public void Drive()
    {
        Mad.Drive(Control, Conto);
    }
}
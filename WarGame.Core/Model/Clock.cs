namespace WarGame.Core.Model;

public sealed class Clock
{
    public DateOnly Date { get; private set; }
    public int Day { get; private set; }
    public int Speed { get; set; } = 1;    // 0 = pausa, 1..4
    public bool Paused => Speed == 0;

    public Clock(DateOnly start) => Date = start;

    public void Advance() { Date = Date.AddDays(1); Day++; }
}

namespace MatchThree.Engine.Rules;

public sealed class IdSource
{
    private int next;

    public IdSource(int next = 0)
    {
        this.next = next;
    }

    public int Next() => next++;
}
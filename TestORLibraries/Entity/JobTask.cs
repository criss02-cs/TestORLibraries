namespace TestORLibraries.Entity;

public record JobTask(int Machine, int Duration, int TaskId)
{
    public override string ToString()
    {
        return $"Machine_{Machine}_Duration_{Duration}_TaskId_{TaskId}";
    }
}

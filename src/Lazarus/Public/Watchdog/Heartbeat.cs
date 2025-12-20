namespace Lazarus.Public.Watchdog;

public record Heartbeat
{
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public Exception? Exception { get; init; }
}

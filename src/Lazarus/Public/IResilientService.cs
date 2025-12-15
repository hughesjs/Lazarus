namespace Lazarus.Public;

public interface IResilientService: IAsyncDisposable
{
    public Task PerformLoop(CancellationToken cancellationToken);
    public string Name { get; }
}

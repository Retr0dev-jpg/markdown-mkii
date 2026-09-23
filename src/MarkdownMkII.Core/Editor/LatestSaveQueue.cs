namespace MarkdownMkII.Core.Editor;
/// <summary>Serializes saves and drains edits that arrive while a snapshot is being written.</summary>
public sealed class LatestSaveQueue<T>
    where T : class
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public bool IsSaving { get; private set; }

    public event EventHandler? StateChanged;
    public async Task<bool> FlushAsync(Func<bool, T?> capture, Func<T, bool, Task> persist, bool checkpoint = false)
    {
        await gate.WaitAsync();
        var wrote = false;
        try
        {
            var force = checkpoint;
            while (capture(force)is { } snapshot)
            {
                if (!IsSaving)
                {
                    IsSaving = true;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }

                await persist(snapshot, checkpoint);
                wrote = true;
                force = false;
            }

            return wrote;
        }
        finally
        {
            IsSaving = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
            gate.Release();
        }
    }
}

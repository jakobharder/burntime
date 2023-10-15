namespace Burntime.Platform.Resource;

public class LoadingCounter
{
    public int LoadingStack { get; private set; }

    public void IncreaseLoadingCount()
    {
        lock (this)
            LoadingStack++;
    }

    public void DecreaseLoadingCount()
    {
        lock (this)
            LoadingStack--;
    }

    public void Reset()
    {
        lock (this)
            LoadingStack = 0;
    }
}

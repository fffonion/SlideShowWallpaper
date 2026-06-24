using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace SlideShowWallpaper;

public static class WinUiAppHost
{
    public static void Start()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}

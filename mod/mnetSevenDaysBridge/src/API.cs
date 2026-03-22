using System;

public class API : IModApi
{
    private static readonly object SyncRoot = new object();
    private static mnetSevenDaysBridge.BridgeLifecycle lifecycle;

    public void InitMod(Mod modInstance)
    {
        lock (SyncRoot)
        {
            if (lifecycle != null)
            {
                return;
            }

            if (modInstance == null)
            {
                throw new ArgumentNullException(nameof(modInstance));
            }

            lifecycle = new mnetSevenDaysBridge.BridgeLifecycle(modInstance.Path);
            lifecycle.Start();
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }
    }

    private static void OnProcessExit(object sender, EventArgs args)
    {
        lock (SyncRoot)
        {
            if (lifecycle == null)
            {
                return;
            }

            lifecycle.Dispose();
            lifecycle = null;
        }
    }
}

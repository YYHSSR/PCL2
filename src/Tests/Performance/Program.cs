using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

internal static class Program {
    [STAThread]
    private static int Main(string[] args) {
        if (args.Length != 1) { Console.Error.WriteLine("Pass the standalone launcher EXE path."); return 1; }
        Environment.SetEnvironmentVariable("PCL_SETTINGS_PROFILE", "PCLPerf_" + Guid.NewGuid().ToString("N"));
        var assembly = Assembly.LoadFile(Path.GetFullPath(args[0]));
        var app = (Application)Activator.CreateInstance(assembly.GetType("PCL.Application")!, true)!;
        Assembly.Load("MeloongCore").GetType("MeloongCore.Main")!.GetMethod("Init")!.Invoke(null, ["PCLPerf"]);
        app.GetType().GetMethod("InitializeComponent")!.Invoke(app, null);
        // Isolate the animation engine; pumping must not open the launcher or start downloads.
        typeof(Application).GetField("_startupUri", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(app, null);
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Startup -= (StartupEventHandler)Delegate.CreateDelegate(typeof(StartupEventHandler), app,
            app.GetType().GetMethod("Application_Startup", BindingFlags.NonPublic | BindingFlags.Instance)!);
        assembly.GetType("PCL.ModBase")!.GetMethod("RunInUi", Type.EmptyTypes)!.Invoke(null, null);
        var animation = assembly.GetType("PCL.ModAnimation")!;
        var groups = (IDictionary)animation.GetField("AniGroups")!.GetValue(null)!;
        var start = animation.GetMethods().Single(m => m.Name == "AniStart" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(IList));
        var code = animation.GetMethod("AaCode")!;
        var tick = animation.GetMethod("AniTimer")!;
        for (int i = 0; i < 1000; i++)
            start.Invoke(null, [new ArrayList { code.Invoke(null, [new ThreadStart(() => { }), 60000, false]) }, "perf-" + i, false]);
        animation.GetField("AniLastTick", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, long.MaxValue);
        for (int i = 0; i < 5; i++) tick.Invoke(null, [16d]);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 120; i++) tick.Invoke(null, [16d]);
        watch.Stop();
        double tickMs = watch.Elapsed.TotalMilliseconds;
        groups.Clear();
        int posted = 0;
        app.Dispatcher.Hooks.OperationPosted += (_, _) => {
            if (new StackTrace(false).GetFrames().Any(f => f.GetMethod()?.DeclaringType?.FullName?.StartsWith("PCL.ModAnimation") == true))
                Interlocked.Increment(ref posted);
        };
        animation.GetMethod("AniStart", Type.EmptyTypes)!.Invoke(null, null);
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
        Console.WriteLine("BENCHMARK animation_1000_groups_120_ticks_ms=" + tickMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        Console.WriteLine("BENCHMARK idle_animation_dispatcher_posts_2s=" + posted);
        // Old launchers keep a foreground animation thread alive after Application.Shutdown.
        Environment.Exit(0);
        return 0;
    }
}

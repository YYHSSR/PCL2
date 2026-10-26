using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using MeloongCore;
using MeloongCore.Extensions;

internal static class Program {
    private static int passed;
    private static void Check(bool condition, string message) {
        if (!condition) throw new Exception(message);
    }
    private static void Run(string name, Action action) {
        action();
        passed++;
        Console.WriteLine("PASS " + name);
    }
    private static T OnWorker<T>(Func<T> action) => Task.Run(action).GetAwaiter().GetResult();
    [STAThread]
    private static int Main() {
        string profile = "PCL2Smoke_" + Guid.NewGuid().ToString("N");
        string temp = Path.Combine(Path.GetTempPath(), profile);
        Environment.SetEnvironmentVariable("PCL_SETTINGS_PROFILE", profile);
        Environment.SetEnvironmentVariable("PCL_CACHE_PATH", temp);
        MeloongCore.Main.Init("PCL2Smoke");
        PCL.Application? app = null;
        try {
            // Initialize the application's UI-thread marker before worker tests.
            PCL.ModBase.RunInUi();
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            app = (PCL.Application)Activator.CreateInstance(typeof(PCL.Application), true)!;
            app.InitializeComponent();
            app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            // Pumping the dispatcher must not launch the real window or its network workers.
            typeof(System.Windows.Application).GetField("_startupUri", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(app, null);
            app.Startup -= (System.Windows.StartupEventHandler)Delegate.CreateDelegate(typeof(System.Windows.StartupEventHandler), app,
                typeof(PCL.Application).GetMethod("Application_Startup", BindingFlags.NonPublic | BindingFlags.Instance)!);
            Run("Embedded resolver does not return a parent DLL for satellite resources", () => {
                try {
                    Assembly.Load("Newtonsoft.Json.resources, Culture=zh-CN");
                    throw new Exception("A missing satellite resource resolved to the main assembly");
                } catch (FileNotFoundException) { }
            });
            Run("Build configuration marker is correct", () => {
                string config = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
                int expected = config == "Debug" ? 100 : config == "Release" ? 50 : 0;
                int actual = (int)typeof(PCL.ModBase).GetField("BuildType")!.GetRawConstantValue()!;
                Check(actual == expected, $"Expected build marker {expected}, got {actual}");
            });
            Run("DPAPI round trip and randomized ciphertext", () => {
                var encrypt = typeof(PCL.Settings).GetMethod("ProtectSetting", BindingFlags.NonPublic | BindingFlags.Static)!;
                var decrypt = typeof(PCL.Settings).GetMethod("UnprotectSetting", BindingFlags.NonPublic | BindingFlags.Static)!;
                string a = (string)encrypt.Invoke(null, ["fake-test-token", "test-key"])!;
                string b = (string)encrypt.Invoke(null, ["fake-test-token", "test-key"])!;
                Check(a.StartsWith("dpapi:v1:") && a != b && !a.Contains("fake-test-token"), "Ciphertext is not protected/randomized");
                Check((string)decrypt.Invoke(null, [a, "test-key"])! == "fake-test-token", "Round trip failed");
                try { decrypt.Invoke(null, [a, "wrong-key"]); throw new Exception("Wrong entropy was accepted"); }
                catch (TargetInvocationException ex) when (ex.InnerException is CryptographicException) { }
            });
            Run("Credential save never writes plaintext", () => {
                PCL.Settings.Set("CacheMsV2OAuthRefresh", "fake-refresh-value");
                using var key = Registry.CurrentUser.OpenSubKey("Software\\" + profile)!;
                string stored = (string)key.GetValue("CacheMsV2OAuthRefresh")!;
                Check(stored.StartsWith("dpapi:v1:") && !stored.Contains("fake-refresh-value"), "Plaintext was stored");
            });
            Run("Legacy DES cache migrates to DPAPI", () => {
                string plain = "fake-legacy-value";
                byte[] keyBytes = Encoding.UTF8.GetBytes(("PCL0000-0000-0000-0000").GetStableHashCode().ToString().EnsureLength('X', 8));
                using var des = new DESCryptoServiceProvider();
                using var transform = des.CreateEncryptor(keyBytes, Encoding.UTF8.GetBytes("95168702"));
                byte[] bytes = Encoding.UTF8.GetBytes(plain);
                string legacy = Convert.ToBase64String(transform.TransformFinalBlock(bytes, 0, bytes.Length));
                using var key = Registry.CurrentUser.CreateSubKey("Software\\" + profile)!;
                key.SetValue("CacheMsV2OAuthRefresh", legacy);
                PCL.Settings.Entries["CacheMsV2OAuthRefresh"].ValueCache.Clear();
                Check(PCL.Settings.Get<string>("CacheMsV2OAuthRefresh") == plain, "Legacy value was not read");
                Check(((string)key.GetValue("CacheMsV2OAuthRefresh")!).StartsWith("dpapi:v1:"), "Legacy value was not migrated");
            });
            Run("Corrupt cache is preserved for recovery", () => {
                using var key = Registry.CurrentUser.CreateSubKey("Software\\" + profile)!;
                key.SetValue("CacheMsV2OAuthRefresh", "dpapi:v1:corrupt");
                PCL.Settings.Entries["CacheMsV2OAuthRefresh"].ValueCache.Clear();
                Check(PCL.Settings.Get<string>("CacheMsV2OAuthRefresh") == "", "Corrupt cache should return the default");
                Check((string)key.GetValue("CacheMsV2OAuthRefresh")! == "dpapi:v1:corrupt", "Corrupt cache was overwritten");
            });
            Run("Certificate option is removed and platform validation remains unmodified", () => {
                Check(!PCL.Settings.Entries.ContainsKey("ToolDownloadCert"), "Certificate toggle is still registered");
                Check(typeof(PCL.ModNet).GetMethod("ShouldValidateSslCertificateOnLogin") == null, "Obsolete validation switch remains");
                Check(ServicePointManager.ServerCertificateValidationCallback == null, "Platform certificate validation was overridden");
            });
            using var server = new LocalServer();
            Run("Backup URL is used after primary failure", () => {
                string result = OnWorker(() => PCL.ModNet.NetRequestByClientRetry(server.Url + "fail", BackupUrl: server.Url + "ok"));
                Check(result == "ok", "Backup did not succeed");
            });
            Run("All failed requests terminate", () => {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try { OnWorker(() => PCL.ModNet.NetRequestByClientMultiple(server.Url + "fail", ThreadCount: 3, Timeout: 2500)); throw new Exception("Expected failure"); }
                catch (HttpRequestException) { }
                Check(watch.ElapsedMilliseconds < 3000, "All failures hung");
            });
            Run("First successful response wins", () => {
                string result = OnWorker(() => PCL.ModNet.NetRequestByClientMultiple(server.Url + "race", ThreadCount: 3, Timeout: 2500));
                Check(result != "1", "Waited for the slow first request");
            });
            Run("Overall timeout is bounded", () => {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try { OnWorker(() => PCL.ModNet.NetRequestByClientMultiple(server.Url + "slow", ThreadCount: 3, Timeout: 250)); throw new Exception("Expected timeout"); }
                catch (TimeoutException) { }
                Check(watch.ElapsedMilliseconds < 1500, "Deadline was not respected");
            });
            Run("POST requests are not duplicated", () => {
                Check(OnWorker(() => PCL.ModNet.NetRequestByClientMultiple(server.Url + "post", HttpMethod.Post, ThreadCount: 3, Content: "test", Timeout: 2500)) == "ok", "POST failed");
                Check(server.PostRequests == 1, "POST was sent more than once");
            });
            Run("ZIP path traversal is rejected", () => {
                Directory.CreateDirectory(temp);
                string archive = Path.Combine(temp, "invalid.zip");
                using (var z = ZipFile.Open(archive, ZipArchiveMode.Create)) {
                    using var writer = new StreamWriter(z.CreateEntry("../outside.txt").Open());
                    writer.Write("test");
                }
                try { FileUtils.ExtractToDirectory(archive, Path.Combine(temp, "extract")); throw new Exception("Traversal accepted"); }
                catch (InvalidDataException) { }
                Check(!File.Exists(Path.Combine(temp, "outside.txt")), "ZIP escaped the target directory");
            });
            Run("G1 policy keeps conservative Java 17 defaults and gates legacy options", () => {
                var normal = PCL.LaunchJvmPolicy.G1Arguments(new Version(17, 0, 20), false, false, true);
                Check(normal.SequenceEqual(new[] { "-XX:+UseG1GC", "-XX:+PerfDisableSharedMem" }), "Unexpected default GC tuning");
                var tuned = PCL.LaunchJvmPolicy.G1Arguments(new Version(17, 0, 20), true, true, true);
                Check(tuned.Contains("-XX:MaxGCPauseMillis=100") && tuned.Contains("-XX:+UseStringDeduplication"), "Optional tuning missing");
                Check(!tuned.Any(a => a.Contains("G1HeapRegionSize") || a.Contains("G1NewSizePercent")), "Heap layout was forced");
                Check(!PCL.LaunchJvmPolicy.G1Arguments(new Version(7, 0, 80), false, true, false).Contains("-XX:+UseStringDeduplication"), "Unsupported Java 7 option");
                Check(!PCL.LaunchJvmPolicy.G1Arguments(new Version(8, 0, 19), false, true, false).Contains("-XX:+UseStringDeduplication"), "Unsupported Java 8 update");
                Check(!Convert.ToBoolean(PCL.Settings.GetDefault("LaunchOptG1Tune")) && !Convert.ToBoolean(PCL.Settings.GetDefault("LaunchOptPreTouch")), "Aggressive tuning remains enabled by default");
            });
            Run("Forge 1.20.1 accepts Java 17 and rejects Java 8", () => {
                var version = new PCLCS.McVersion("1.20.1");
                version.ModLoaderVersions[PCLCS.ModLoaders.Forge] = "47.3.0";
                var instance = new PCL.ModMinecraft.McInstance(Path.Combine(temp, "forge-1.20.1")) {
                    Version = version,
                    JsonObject = Newtonsoft.Json.Linq.JObject.Parse("{\"id\":\"forge-1.20.1\",\"javaVersion\":{\"majorVersion\":17}}")
                };
                var requirement = PCL.ModJava.GetJavaRequirement(instance).Range;
                Check(requirement.Contains(new Version(17, 0, 20)), "Java 17 was rejected");
                Check(!requirement.Contains(new Version(8, 0, 450)) && !requirement.Contains(new Version(22, 0)), "Incompatible Java accepted");
            });
            Run("CurseForge key is encrypted, configurable and scoped to HTTPS official hosts", () => {
                string? savedEnv = Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY");
                try {
                    Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", null);
                    PCL.Settings.Set("ToolCurseForgeApiKey", "test-local-key");
                    using var key = Registry.CurrentUser.OpenSubKey("Software\\" + profile)!;
                    string stored = (string)key.GetValue("ToolCurseForgeApiKey")!;
                    Check(stored.StartsWith("dpapi:v1:") && !stored.Contains("test-local-key"), "API Key stored in plaintext");
                    Check(PCL.CurseForgeAccess.GetApiKey() == "test-local-key", "Saved key unavailable");
                    Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", "test-environment-key");
                    foreach (string host in new[] { "api.curseforge.com", "edge.forgecdn.net", "api.curseforge.com.example.org", "modrinth.com" }) {
                        using var request = new HttpRequestMessage(HttpMethod.Get, "https://" + host + "/test");
                        PCL.CurseForgeAccess.ApplyHeaders(request.RequestUri, request);
                        Check(request.Headers.Contains("x-api-key") == (host == "api.curseforge.com" || host == "edge.forgecdn.net"), "API key host scope error");
                    }
                    using var insecure = new HttpRequestMessage(HttpMethod.Get, "http://api.curseforge.com/test");
                    try { PCL.CurseForgeAccess.ApplyHeaders(insecure.RequestUri, insecure); throw new Exception("HTTP accepted"); }
                    catch (InvalidOperationException) { }
                    PCL.Settings.Set("ToolCurseForgeApiKey", "");
                } finally { Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", savedEnv); }
            });
            Run("CurseForge redirects do not leak API keys or downgrade HTTPS", () => {
                string? savedEnv = Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY");
                Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", "test-only-key");
                try {
                    using var handler = new RedirectHandler("https://cdn.example.org/file.jar");
                    using var client = new HttpClient(handler);
                    using var request = new HttpRequestMessage(HttpMethod.Get, "https://edge.forgecdn.net/file.jar");
                    using var response = OnWorker(() => PCL.CurseForgeAccess.SendAsync(client, request, CancellationToken.None).GetAwaiter().GetResult());
                    Check(response.IsSuccessStatusCode && handler.KeySeen.SequenceEqual(new[] { true, false }), "Key leaked across a redirect");
                    using var downgrade = new RedirectHandler("http://cdn.example.org/file.jar");
                    using var downgradeClient = new HttpClient(downgrade);
                    using var downgradeRequest = new HttpRequestMessage(HttpMethod.Get, "https://edge.forgecdn.net/file.jar");
                    try { OnWorker(() => PCL.CurseForgeAccess.SendAsync(downgradeClient, downgradeRequest, CancellationToken.None).GetAwaiter().GetResult()); throw new Exception("HTTPS downgrade accepted"); }
                    catch (HttpRequestException) { }
                    Check(downgrade.KeySeen.Count == 1, "An insecure redirect was followed");
                } finally { Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", savedEnv); }
            });
            Run("Mod directory snapshots stay nonblocking and launch reads refresh", () => {
                string folder = Path.Combine(temp, "mods");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "a.JAR"), "");
                File.WriteAllText(Path.Combine(folder, "b.zip"), "");
                File.WriteAllText(Path.Combine(folder, "c.litemod"), "");
                File.WriteAllText(Path.Combine(folder, "disabled.jar.disabled"), "");
                Check(PCL.ModDirectoryCache.GetCount(folder, true) == 3, "Mod count is incorrect");
                File.WriteAllText(Path.Combine(folder, "new.jar"), "");
                Check(PCL.ModDirectoryCache.GetCount(folder) == 3, "A cached UI read rescanned synchronously");
                Check(PCL.ModDirectoryCache.GetCount(folder, true) == 4, "Launch worker did not refresh");
                Check(PCL.ModDirectoryCache.GetCount(Path.Combine(temp, "missing-mods"), true) == 0, "Missing mods folder was not handled");
            });
            Run("Virtualized controls initialize exactly once", () => {
                int genericCalls = 0, calls = 0;
                var generic = new PCL.MyVirtualizingElement<System.Windows.Controls.Border>(() => { genericCalls++; return new(); });
                var plain = new PCL.MyVirtualizingElement(() => { calls++; return new System.Windows.Controls.Border(); });
                Check(ReferenceEquals(generic.Init(), generic.Init()) && genericCalls == 1, "Generic initializer ran twice");
                Check(ReferenceEquals(plain.Init(), plain.Init()) && calls == 1, "Initializer ran twice");
            });
            Run("Animation callbacks preserve replacement, cancellation and chaining", () => {
                var clock = typeof(PCL.ModAnimation).GetField("AniLastTick", BindingFlags.NonPublic | BindingFlags.Static)!;
                object oldClock = clock.GetValue(null)!;
                int replaced = 0, canceled = 0, chained = 0;
                try {
                    clock.SetValue(null, long.MaxValue);
                    PCL.ModAnimation.AniStart(PCL.ModAnimation.AaCode(() => PCL.ModAnimation.AniStart(PCL.ModAnimation.AaCode(() => replaced++), "test-replace")), "test-replace");
                    clock.SetValue(null, long.MaxValue);
                    PCL.ModAnimation.AniTimer(16);
                    Check(replaced == 0 && PCL.ModAnimation.AniIsRun("test-replace"), "A replacement was removed with the old group");
                    PCL.ModAnimation.AniTimer(16);
                    Check(replaced == 1 && !PCL.ModAnimation.AniIsRun("test-replace"), "Replacement did not complete");
                    PCL.ModAnimation.AniStart(PCL.ModAnimation.AaCode(() => canceled++), "test-cancel");
                    PCL.ModAnimation.AniStop("test-cancel");
                    PCL.ModAnimation.AniTimer(16);
                    Check(canceled == 0, "Canceled callback ran");
                    PCL.ModAnimation.AniStart(new System.Collections.ArrayList {
                        PCL.ModAnimation.AaCode(() => chained++), PCL.ModAnimation.AaCode(() => chained++, After: true)
                    }, "test-chain");
                    clock.SetValue(null, long.MaxValue);
                    PCL.ModAnimation.AniTimer(16);
                    Check(chained == 1, "Chained callback ran before its predecessor finished");
                    PCL.ModAnimation.AniTimer(16);
                    Check(chained == 2, "Chained callback was lost");
                } finally {
                    foreach (string name in new[] { "test-replace", "test-cancel", "test-chain" }) PCL.ModAnimation.AniStop(name);
                    clock.SetValue(null, oldClock);
                }
            });
            Run("Viewport loading and page timers follow visibility and unload", () => {
                int initialized = 0;
                bool lastInitialized = false;
                var panel = new System.Windows.Controls.StackPanel();
                for (int i = 0; i < 300; i++) {
                    int index = i;
                    panel.Children.Add(new PCL.MyVirtualizingElement<System.Windows.Controls.Border>(() => {
                        initialized++;
                        if (index == 299) lastInitialized = true;
                        return new() { Height = 30 };
                    }) { Height = 30 });
                }
                var scroll = new System.Windows.Controls.ScrollViewer { Content = panel };
                var window = new System.Windows.Window { Content = scroll, Width = 240, Height = 180, Left = -20000, Top = -20000, Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
                var refresh = new PCL.VisiblePageTimer(scroll, () => { });
                try {
                    window.Show();
                    PumpUntil(() => initialized > 0, 1500);
                    Check(initialized < 20, "Offscreen list items were initialized eagerly");
                    Check(refresh.IsRunning, "Visible page timer did not start");
                    scroll.ScrollToEnd();
                    PumpUntil(() => lastInitialized, 1500);
                    Check(initialized < 40, "Scrolling initialized the whole list");
                    scroll.Visibility = System.Windows.Visibility.Collapsed;
                    Check(!refresh.IsRunning, "Hidden page timer is still running");
                    scroll.Visibility = System.Windows.Visibility.Visible;
                    Check(refresh.IsRunning, "Visible page timer did not restart");
                    window.Content = null;
                    PumpUntil(() => !refresh.IsRunning, 1500);
                    window.Content = scroll;
                    scroll.ScrollToHome();
                    PumpUntil(() => refresh.IsRunning, 1500);
                } finally { window.Close(); }
            });
            Run("Delayed animation callbacks finish without a rendered window and stop idle work", () => {
                bool finished = false;
                PCL.ModAnimation.AniStart();
                PCL.ModAnimation.AniStart(PCL.ModAnimation.AaCode(() => finished = true, Delay: 100), "test-hidden-window");
                PumpUntil(() => finished && PCL.ModAnimation.AniGroups.Count == 0, 3000);
                Check(!PCL.ModAnimation.AniIsRun("test-hidden-window"), "Completed group remains registered");
                Check(!(bool)typeof(PCL.ModAnimation).GetField("RenderingSubscribed", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!, "Empty animation engine remains subscribed");
                var fallback = (System.Windows.Threading.DispatcherTimer)typeof(PCL.ModAnimation).GetField("RenderFallback", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                Check(!fallback.IsEnabled, "Empty animation engine fallback is still running");
            });
            Run("Argument replacement uses the escape handler of each call", () => {
                string escaped = PCL.ModMain.ArgumentReplace("{pcl_version_code}", s => "[" + s + "]");
                string plain = PCL.ModMain.ArgumentReplace("{pcl_version_code}");
                Check(escaped.StartsWith("[") && escaped.EndsWith("]") && !plain.StartsWith("["), $"Escape handler leaked between calls: {escaped} / {plain}");
            });
            Run("Task temp cleanup runs once under concurrency", () => {
                var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => PCL.ModMain.TryClearTaskTemp())).ToArray();
                Check(Task.WaitAll(tasks, 10000), "Concurrent cleanup did not finish");
                string folder = PCL.ModMain.RequestTaskTempFolder();
                PCL.ModMain.TryClearTaskTemp();
                Check(Directory.Exists(folder), "Task temp folder was deleted after the first cleanup");
            });
            Run("Mutually inheriting versions do not overflow the stack", () => {
                string folder = Path.Combine(temp, "inherit-mc") + "\\";
                foreach (var (name, parent) in new[] { ("A", "B"), ("B", "A") }) {
                    Directory.CreateDirectory(Path.Combine(folder, "versions", name));
                    File.WriteAllText(Path.Combine(folder, "versions", name, name + ".json"), $"{{\"id\":\"{name}\",\"inheritsFrom\":\"{parent}\",\"libraries\":[]}}");
                }
                string saved = PCL.ModMinecraft.McFolderSelected;
                try {
                    PCL.ModMinecraft.McFolderSelected = folder;
                    var json = OnWorker(() => new PCL.ModMinecraft.McInstance("A").JsonObject);
                    Check(json != null, "Instance JSON was not loaded");
                } finally { PCL.ModMinecraft.McFolderSelected = saved; }
            });
            Run("Removed pages, routes, events and settings are absent", () => {
                var assembly = typeof(PCL.Application).Assembly;
                Check(Array.TrueForAll(assembly.GetTypes(), t => !t.Name.StartsWith("PageOther") && !t.Name.StartsWith("PageLink") && t.Name != "PageSetupLink" && t.Name != "HelpEntry" && t.Name != "PageSetupUI"), "Removed page is still compiled");
                Check(!Enum.IsDefined(typeof(PCL.FormMain.PageType), 2) && !Enum.IsDefined(typeof(PCL.FormMain.PageType), 4) && !Enum.IsDefined(typeof(PCL.FormMain.PageType), 9), "Removed route is still available");
                Check(!Enum.IsDefined(typeof(PCL.CustomEvent.EventType), 23), "Removed multiplayer event is still available");
                Check((int)PCL.FormMain.PageType.Setup == 3 && (int)PCL.FormMain.PageType.InstanceSelect == 5, "Existing route IDs changed");
                Check((int)PCL.CustomEvent.EventType.执行命令 == 4, "Existing event IDs changed");
                foreach (string key in PCL.Settings.Entries.Keys)
                    Check(!key.StartsWith("SystemDebug") && !key.StartsWith("UiHiddenOther") && !key.StartsWith("ToolUpdate") && !key.StartsWith("Link") && key != "ToolHelpChinese" && key != "SystemSystemCache" && key != "UiHiddenPageOther", "Removed setting remains: " + key);
                Check(new System.Resources.ResourceManager("PCL.Resources", assembly).GetObject("Help") == null, "Help archive remains embedded");
                foreach (string control in new[] { "BtnTitleSelect2", "BtnTitleSelect4" })
                    Check(typeof(PCL.FormMain).GetField(control, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null, "Removed navigation control remains: " + control);
                using var baml = new System.Resources.ResourceReader(assembly.GetManifestResourceStream("Plain Craft Launcher 2.g.resources")!);
                foreach (System.Collections.DictionaryEntry entry in baml)
                    Check(!((string)entry.Key).Contains("pageother") && !((string)entry.Key).Contains("pagelink") && !((string)entry.Key).Contains("pagesetuplink"), "Removed XAML remains embedded");
            });
            Run("Chinese-only settings XAML loads with exactly two cards", () => {
                Check(System.Globalization.CultureInfo.CurrentCulture.Name == "zh-CN" && System.Globalization.CultureInfo.CurrentUICulture.Name == "zh-CN", "Launcher is not fixed to Chinese");
                var page = new PCL.PageSetupSystem();
                var panel = (System.Windows.Controls.StackPanel)page.FindName("PanMain");
                Check(panel.Children.Count == 2 && ((PCL.MyCard)panel.Children[0]).Title == "下载" && ((PCL.MyCard)panel.Children[1]).Title == "社区资源", "Unexpected system settings cards");
                var overall = new PCL.PageInstanceOverall();
                Check(overall.FindName("PanDisplay") == null, "Instance personalization card remains");
                _ = new PCL.PageLaunchRight();
                var launch = new PCL.PageSetupLaunch();
                var instanceSetup = new PCL.PageInstanceSetup();
                foreach (var control in new System.Windows.DependencyObject[] { launch, instanceSetup, page })
                    CheckSettingKeys(control);
                _ = new PCL.PageInstanceExport();
                app.Shutdown();
            });
            Console.WriteLine($"RESULT {passed} passed");
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine(ex);
            return 1;
        } finally {
            app?.Shutdown();
            // The randomized profile belongs only to this test process.
            if (profile.StartsWith("PCL2Smoke_") && profile.Length == 42)
                Registry.CurrentUser.DeleteSubKeyTree("Software\\" + profile, false);
        }
    }

    private static void CheckSettingKeys(System.Windows.DependencyObject control) {
        string key = PCL.SettingService.GetKey(control);
        Check(key == null || PCL.Settings.Entries.ContainsKey(key), "XAML references an unknown setting: " + key);
        foreach (object child in System.Windows.LogicalTreeHelper.GetChildren(control))
            if (child is System.Windows.DependencyObject dependency) CheckSettingKeys(dependency);
    }

    private static void PumpUntil(Func<bool> condition, int timeout) {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(5) };
        timer.Tick += (_, _) => { if (condition() || watch.ElapsedMilliseconds >= timeout) { timer.Stop(); frame.Continue = false; } };
        timer.Start();
        try { System.Windows.Threading.Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Check(condition(), "Timed out waiting for dispatcher work");
    }

    private sealed class RedirectHandler(string destination) : HttpMessageHandler {
        internal readonly List<bool> KeySeen = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
            KeySeen.Add(request.Headers.Contains("x-api-key"));
            var response = new HttpResponseMessage(KeySeen.Count == 1 ? HttpStatusCode.Redirect : HttpStatusCode.OK);
            if (KeySeen.Count == 1) response.Headers.Location = new Uri(destination);
            return Task.FromResult(response);
        }
    }

    private sealed class LocalServer : IDisposable {
        private readonly HttpListener listener = new();
        private int raceRequests;
        public int PostRequests;
        public readonly string Url;
        public LocalServer() {
            var port = new TcpListener(IPAddress.Loopback, 0);
            port.Start();
            int number = ((IPEndPoint)port.LocalEndpoint).Port;
            port.Stop();
            Url = "http://127.0.0.1:" + number + "/";
            listener.Prefixes.Add(Url);
            listener.Start();
            _ = Task.Run(async () => {
                while (listener.IsListening) {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync(); } catch { return; }
                    _ = Task.Run(async () => {
                        try {
                            string path = ctx.Request.Url!.AbsolutePath;
                            string value = "ok";
                            if (path == "/fail") ctx.Response.StatusCode = 500;
                            if (path == "/slow") await Task.Delay(1800);
                            if (path == "/post") { Interlocked.Increment(ref PostRequests); await Task.Delay(700); }
                            if (path == "/race") {
                                int index = Interlocked.Increment(ref raceRequests);
                                await Task.Delay(index == 1 ? 900 : 25);
                                value = index.ToString();
                            }
                            byte[] bytes = Encoding.UTF8.GetBytes(value);
                            ctx.Response.ContentLength64 = bytes.Length;
                            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                        } catch { }
                        finally { try { ctx.Response.Close(); } catch { } }
                    });
                }
            });
        }
        public void Dispose() { listener.Stop(); listener.Close(); }
    }
}

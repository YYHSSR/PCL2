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
        try {
            // Initialize the application's UI-thread marker before worker tests.
            PCL.ModBase.RunInUi();
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var app = (PCL.Application)Activator.CreateInstance(typeof(PCL.Application), true)!;
            app.InitializeComponent();
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
            Run("Certificate errors cannot be bypassed", () => {
                PCL.ModNet.ShouldValidateSslCertificateOnLogin(false);
                var validate = ServicePointManager.ServerCertificateValidationCallback!;
                Check(!validate(null!, null, null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors), "Invalid certificate accepted");
                Check(validate(null!, null, null, System.Net.Security.SslPolicyErrors.None), "Valid certificate rejected");
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
            Run("Removed pages, routes, events and settings are absent", () => {
                var assembly = typeof(PCL.Application).Assembly;
                Check(Array.TrueForAll(assembly.GetTypes(), t => !t.Name.StartsWith("PageOther") && t.Name != "HelpEntry" && t.Name != "PageSetupUI"), "Removed page is still compiled");
                Check(!Enum.IsDefined(typeof(PCL.FormMain.PageType), 4) && !Enum.IsDefined(typeof(PCL.FormMain.PageType), 9), "Removed route is still available");
                Check((int)PCL.FormMain.PageType.Setup == 3 && (int)PCL.FormMain.PageType.InstanceSelect == 5, "Existing route IDs changed");
                Check((int)PCL.CustomEvent.EventType.执行命令 == 4, "Existing event IDs changed");
                foreach (string key in PCL.Settings.Entries.Keys)
                    Check(!key.StartsWith("SystemDebug") && !key.StartsWith("UiHiddenOther") && !key.StartsWith("ToolUpdate") && key != "ToolHelpChinese" && key != "SystemSystemCache" && key != "UiHiddenPageOther", "Removed setting remains: " + key);
                Check(new System.Resources.ResourceManager("PCL.Resources", assembly).GetObject("Help") == null, "Help archive remains embedded");
                Check(typeof(PCL.FormMain).GetField("BtnTitleSelect4", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null, "Removed navigation control remains");
                using var baml = new System.Resources.ResourceReader(assembly.GetManifestResourceStream("Plain Craft Launcher 2.g.resources")!);
                foreach (System.Collections.DictionaryEntry entry in baml)
                    Check(!((string)entry.Key).Contains("pageother"), "Removed XAML remains embedded");
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

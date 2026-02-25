using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace LaunchpadAuth;

/// <summary>
/// Minimal WebView2 app that navigates to a NASA LAUNCHPAD-enabled site
/// and handles CAC/PIV smart card authentication.
/// </summary>
static class Program
{
    // Default if no config provided
    const string DefaultUrl = "https://id.nasa.gov/";
    const string DefaultTitle = "NASA access LAUNCHPAD";

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.SetCompatibleTextRenderingDefault(false);

        // Load config: look for config.json next to the exe, then in CWD
        var (url, title) = LoadConfig(args);

        var form = new Form
        {
            Text = title,
            Width = 1200,
            Height = 900,
            StartPosition = FormStartPosition.CenterScreen,
            WindowState = FormWindowState.Maximized,
        };

        var statusLabel = new Label
        {
            Text = $"Navigating to {url} \u2014 authenticate with your CAC if prompted.",
            Dock = DockStyle.Bottom,
            Height = 28,
            Padding = new Padding(6, 0, 0, 0),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            BackColor = System.Drawing.Color.FromArgb(240, 240, 240),
        };

        var webView = new WebView2 { Dock = DockStyle.Fill };

        form.Controls.Add(webView);
        form.Controls.Add(statusLabel);

        // Poll for the Windows Security PIN dialog and bring it to front
        var credTimer = CredentialFocusTimer.Start();

        form.Load += async (s, e) =>
        {
            try
            {
                await webView.EnsureCoreWebView2Async();

                // Show current URL in status bar
                webView.CoreWebView2.NavigationStarting += (sender, navArgs) =>
                {
                    statusLabel.Text = $"Loading: {navArgs.Uri}";
                };

                bool sawAuthPage = false;

                string statusFile = Path.Combine(
                    Path.GetTempPath(), "launchpad_auth_status.txt");

                webView.CoreWebView2.NavigationCompleted += async (sender, navArgs) =>
                {
                    string current = webView.CoreWebView2.Source ?? "";

                    if (!navArgs.IsSuccess)
                    {
                        string error = navArgs.WebErrorStatus.ToString();
                        statusLabel.Text = $"Error: {error}";
                        statusLabel.BackColor = System.Drawing.Color.FromArgb(255, 200, 200);
                        form.Text = $"{title} \u2014 Error";
                        try { File.WriteAllText(statusFile,
                            $"ERROR: {error} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nURL: {current}"); }
                        catch { }
                        return;
                    }

                    // Check page content for LAUNCHPAD-specific auth errors
                    try
                    {
                        // Look for the error heading element specifically, not full page text
                        string errorText = await webView.CoreWebView2.ExecuteScriptAsync(
                            "document.querySelector('h1,h2,h3,.error,.alert')?.innerText || ''");
                        errorText = System.Text.Json.JsonSerializer.Deserialize<string>(errorText) ?? "";

                        if (errorText.Contains("Smartcard authentication failed", StringComparison.OrdinalIgnoreCase)
                            || errorText.Contains("No client certificate", StringComparison.OrdinalIgnoreCase))
                        {
                            // Also grab the detail text
                            string detail = await webView.CoreWebView2.ExecuteScriptAsync(
                                "document.querySelector('main,article,.content,#content')?.innerText?.substring(0,300) || document.body?.innerText?.substring(0,300) || ''");
                            detail = System.Text.Json.JsonSerializer.Deserialize<string>(detail) ?? "";
                            detail = detail.Trim();

                            statusLabel.Text = $"Auth failed \u2014 {errorText.Trim()}";
                            statusLabel.BackColor = System.Drawing.Color.FromArgb(255, 200, 200);
                            form.Text = $"{title} \u2014 Auth Failed";
                            try { File.WriteAllText(statusFile,
                                $"AUTH FAILED at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{detail}"); }
                            catch { }
                            return;
                        }
                    }
                    catch { }

                    bool onAuthPage =
                        current.Contains("auth.", StringComparison.OrdinalIgnoreCase);

                    if (onAuthPage)
                    {
                        sawAuthPage = true;
                        statusLabel.Text = "Waiting for authentication... enter your CAC PIN when prompted.";
                    }
                    else if (sawAuthPage)
                    {
                        statusLabel.Text = $"Authenticated \u2014 {current}";
                        statusLabel.BackColor = System.Drawing.Color.FromArgb(200, 255, 200);
                        form.Text = $"{title} \u2014 Authenticated";
                        try { File.WriteAllText(statusFile,
                            $"Authenticated at {DateTime.Now:yyyy-MM-dd HH:mm:ss}"); }
                        catch { }
                    }
                    else
                    {
                        statusLabel.Text = $"Loaded \u2014 {current}";
                    }
                };

                // Inject Edge cookies for pre-authenticated sessions
                await EdgeCookieExtractor.InjectCookies(webView.CoreWebView2, url);

                webView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"WebView2 failed to initialize.\n\n{ex.Message}\n\n"
                    + "Ensure WebView2 Runtime is installed:\n"
                    + "https://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                    title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                form.Close();
            }
        };

        form.FormClosing += (s, e) =>
        {
            credTimer.Stop();
            credTimer.Dispose();
        };

        Application.Run(form);
    }

    static (string url, string title) LoadConfig(string[] args)
    {
        // Allow config path as first CLI argument
        string? configPath = args.Length > 0 ? args[0] : null;

        if (configPath == null)
        {
            // Look next to executable, then in current directory
            string exeDir = AppContext.BaseDirectory;
            string candidate = Path.Combine(exeDir, "config.json");
            if (File.Exists(candidate))
                configPath = candidate;
            else if (File.Exists("config.json"))
                configPath = "config.json";
        }

        if (configPath != null && File.Exists(configPath))
        {
            try
            {
                string json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string url = root.TryGetProperty("url", out var u)
                    ? u.GetString() ?? DefaultUrl : DefaultUrl;
                string title = root.TryGetProperty("title", out var t)
                    ? t.GetString() ?? DefaultTitle : DefaultTitle;

                Console.WriteLine($"Config: {configPath}");
                Console.WriteLine($"  url:   {url}");
                Console.WriteLine($"  title: {title}");
                return (url, title);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: failed to read {configPath}: {ex.Message}");
            }
        }

        Console.WriteLine($"No config.json found — using default: {DefaultUrl}");
        return (DefaultUrl, DefaultTitle);
    }
}

/// <summary>
/// Polls for the Windows Security dialog (smart card PIN entry) and
/// brings it to the foreground so the user doesn't miss the prompt.
/// </summary>
static class CredentialFocusTimer
{
    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool BringWindowToTop(IntPtr hWnd);

    public static System.Windows.Forms.Timer Start()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 500 };
        timer.Tick += (s, e) => FocusWindowByTitle("Windows Security");
        timer.Start();
        return timer;
    }

    static void FocusWindowByTitle(string titleContains)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            if (sb.ToString().Contains(titleContains, StringComparison.OrdinalIgnoreCase))
            {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (found == IntPtr.Zero) return;

        // AttachThreadInput trick to bypass Windows focus-stealing prevention
        var foreground = GetForegroundWindow();
        uint foreThread = GetWindowThreadProcessId(foreground, out _);
        uint curThread = GetCurrentThreadId();

        if (foreThread != curThread)
            AttachThreadInput(curThread, foreThread, true);

        SetForegroundWindow(found);
        BringWindowToTop(found);

        if (foreThread != curThread)
            AttachThreadInput(curThread, foreThread, false);
    }
}

/// <summary>
/// Extracts cookies from the Edge browser's cookie database and injects
/// them into WebView2 to skip re-authentication when the user already
/// has a valid Edge session.
/// </summary>
static class EdgeCookieExtractor
{
    public static async Task InjectCookies(CoreWebView2 webView, string targetUrl)
    {
        try
        {
            var uri = new Uri(targetUrl);
            string domain = uri.Host;
            // Extract top-level domain for broad cookie matching (e.g. nasa.gov)
            string[] parts = domain.Split('.');
            string topDomain = parts.Length >= 2
                ? parts[^2] + "." + parts[^1]
                : domain;

            byte[]? key = GetEncryptionKey();
            if (key == null) return;

            var cookies = ExtractCookies(key, topDomain);
            var manager = webView.CookieManager;

            int count = 0;
            foreach (var c in cookies)
            {
                var cookie = manager.CreateCookie(c.Name, c.Value, c.Domain, c.Path);
                cookie.IsSecure = c.Secure;
                cookie.IsHttpOnly = c.HttpOnly;
                if (c.ExpiresUtc > 0)
                {
                    var expires = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        .AddTicks(c.ExpiresUtc * 10); // Chrome epoch: microseconds since 1601
                    cookie.Expires = expires.ToLocalTime();
                }
                manager.AddOrUpdateCookie(cookie);
                count++;
            }

            if (count > 0)
                Console.WriteLine($"Injected {count} Edge cookies for {topDomain}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cookie injection skipped: {ex.Message}");
        }
    }

    static byte[]? GetEncryptionKey()
    {
        string localState = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data", "Local State");

        if (!File.Exists(localState)) return null;

        string json = File.ReadAllText(localState);
        using var doc = JsonDocument.Parse(json);
        string encryptedKeyB64 = doc.RootElement
            .GetProperty("os_crypt")
            .GetProperty("encrypted_key")
            .GetString()!;

        byte[] encryptedKey = Convert.FromBase64String(encryptedKeyB64);
        // Strip "DPAPI" prefix (5 bytes)
        byte[] keyBytes = new byte[encryptedKey.Length - 5];
        Array.Copy(encryptedKey, 5, keyBytes, 0, keyBytes.Length);

        return ProtectedData.Unprotect(keyBytes, null, DataProtectionScope.CurrentUser);
    }

    record CookieData(string Name, string Value, string Domain, string Path,
        long ExpiresUtc, bool Secure, bool HttpOnly);

    static List<CookieData> ExtractCookies(byte[] key, string topDomain)
    {
        var result = new List<CookieData>();

        string edgeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data", "Default");

        // Cookie DB may be in Default/Cookies or Default/Network/Cookies
        string cookiesDb = Path.Combine(edgeDir, "Network", "Cookies");
        if (!File.Exists(cookiesDb))
            cookiesDb = Path.Combine(edgeDir, "Cookies");
        if (!File.Exists(cookiesDb)) return result;

        // Copy to temp to avoid DB lock from Edge
        string tempDb = Path.Combine(Path.GetTempPath(), $"launchpad_cookies_{Guid.NewGuid():N}.db");
        File.Copy(cookiesDb, tempDb, true);

        try
        {
            using var conn = new SqliteConnection($"Data Source={tempDb};Mode=ReadOnly");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT name, encrypted_value, host_key, path,
                       expires_utc, is_secure, is_httponly
                FROM cookies
                WHERE host_key LIKE @domain";
            cmd.Parameters.AddWithValue("@domain", $"%{topDomain}%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string name = reader.GetString(0);
                byte[] encryptedValue = (byte[])reader[1];
                string domain = reader.GetString(2);
                string path = reader.GetString(3);
                long expiresUtc = reader.GetInt64(4);
                bool secure = reader.GetInt64(5) != 0;
                bool httpOnly = reader.GetInt64(6) != 0;

                string value = DecryptCookieValue(encryptedValue, key);
                if (!string.IsNullOrEmpty(value))
                    result.Add(new CookieData(name, value, domain, path,
                        expiresUtc, secure, httpOnly));
            }
        }
        finally
        {
            try { File.Delete(tempDb); } catch { }
        }

        return result;
    }

    static string DecryptCookieValue(byte[] encrypted, byte[] key)
    {
        if (encrypted.Length < 15) return "";

        // v10/v20 prefix = AES-256-GCM encrypted
        string prefix = Encoding.UTF8.GetString(encrypted, 0, 3);
        if (prefix != "v10" && prefix != "v20") return "";

        byte[] nonce = new byte[12];
        Array.Copy(encrypted, 3, nonce, 0, 12);

        int cipherLen = encrypted.Length - 3 - 12 - 16;
        if (cipherLen <= 0) return "";

        byte[] ciphertext = new byte[cipherLen];
        byte[] tag = new byte[16];
        Array.Copy(encrypted, 15, ciphertext, 0, cipherLen);
        Array.Copy(encrypted, encrypted.Length - 16, tag, 0, 16);

        byte[] plaintext = new byte[cipherLen];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}

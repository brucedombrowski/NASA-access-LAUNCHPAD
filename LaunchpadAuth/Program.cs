using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
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
    const string DefaultTitle = "NASA LAUNCHPAD Auth";

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

                webView.CoreWebView2.NavigationCompleted += (sender, navArgs) =>
                {
                    string current = webView.CoreWebView2.Source ?? "";
                    bool onAuthPage =
                        current.Contains("launchpad", StringComparison.OrdinalIgnoreCase)
                        || current.Contains("auth.", StringComparison.OrdinalIgnoreCase)
                        || current.Contains("id.nasa.gov", StringComparison.OrdinalIgnoreCase);

                    if (onAuthPage)
                    {
                        statusLabel.Text = "Waiting for authentication... enter your CAC PIN when prompted.";
                    }
                    else
                    {
                        statusLabel.Text = $"Authenticated \u2014 {current}";
                        form.Text = $"{title} \u2014 Authenticated";
                    }
                };

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

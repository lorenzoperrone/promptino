using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Promptino.Storage.Settings;
using Promptino.Platform;

namespace Promptino.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        LoadInitialLocale();
    }

    private void LoadInitialLocale()
    {
        var settingsPath = new WindowsAppDataPathProvider().GetSettingsFilePath();
        var language = "Auto";
        try
        {
            if (System.IO.File.Exists(settingsPath))
            {
                var json = System.IO.File.ReadAllText(settingsPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Language", out var prop))
                {
                    language = prop.GetString() ?? "Auto";
                }
            }
        }
        catch
        {
            // Ignored
        }

        SetLanguage(language);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "ResourceInclude is constructed with known locale URIs (en.axaml / it.axaml) " +
        "that are compiled into the assembly. The trimmer cannot statically verify AvaresXamlLoader.Load " +
        "at this call site, but only two well-known resource files are ever loaded.")]
    public static void SetLanguage(string language)
    {
        if (Application.Current is not App app) return;

        var locale = "en";
        if (language == "Auto")
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (culture == "it")
            {
                locale = "it";
            }
        }
        else
        {
            locale = language;
        }

        // Find and remove any existing dictionary from Locales
        IResourceProvider? oldDict = null;
        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict is ResourceInclude ri && ri.Source != null && ri.Source.ToString().Contains("/Assets/Locales/"))
            {
                oldDict = ri;
                break;
            }
        }

        try
        {
            var uri = new Uri($"avares://Promptino.App/Assets/Locales/{locale}.axaml");
            var newInclude = new ResourceInclude(uri)
            {
                Source = uri
            };
            
            if (oldDict != null)
            {
                app.Resources.MergedDictionaries.Remove(oldDict);
            }
            app.Resources.MergedDictionaries.Add(newInclude);
        }
        catch (Exception outerEx)
        {
            try
            {
                if (oldDict == null)
                {
                    var fallbackUri = new Uri("avares://Promptino.App/Assets/Locales/en.axaml");
                    var newInclude = new ResourceInclude(fallbackUri) { Source = fallbackUri };
                    app.Resources.MergedDictionaries.Add(newInclude);
                }
            }
            catch (Exception innerEx)
            {
                Console.WriteLine("=== RESOURCE LOAD EXCEPTION ===");
                Console.WriteLine(outerEx.ToString());
                Console.WriteLine(innerEx.ToString());
                Console.WriteLine("===============================");
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) => (desktop.MainWindow as MainWindow)?.PrepareForShutdown();
            AppDomain.CurrentDomain.UnhandledException += (_, _) => (desktop.MainWindow as MainWindow)?.PrepareForShutdown();
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                (desktop.MainWindow as MainWindow)?.PrepareForShutdown();
                e.SetObserved();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

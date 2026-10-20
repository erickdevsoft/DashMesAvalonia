using Avalonia;
using System;
using System.Globalization;

namespace DashAvalonia;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // The UI is in Spanish: keep the user's Spanish locale, or fall back to es-EC for numbers and dates.
        if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName != "es")
        {
            var spanish = CultureInfo.GetCultureInfo("es-EC");
            CultureInfo.DefaultThreadCurrentCulture = spanish;
            CultureInfo.DefaultThreadCurrentUICulture = spanish;
            CultureInfo.CurrentCulture = spanish;
            CultureInfo.CurrentUICulture = spanish;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}

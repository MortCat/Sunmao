using System.Windows;

namespace Sunmao.Samples.ThemeGallery;

/// <summary>Starts the interactive gallery, or exports screenshots with <c>--export &lt;folder&gt;</c>.</summary>
public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var exportIndex = Array.IndexOf(e.Args, "--export");
        if (exportIndex >= 0)
        {
            var folder = exportIndex + 1 < e.Args.Length ? e.Args[exportIndex + 1] : "gallery-screenshots";
            GalleryExporter.Export(folder);
            Shutdown();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}

using System.Windows.Controls;
using System.Windows.Media;
using Sunmao.Wpf.Theme;

namespace Sunmao.Samples.ThemeGallery;

/// <summary>Shows every style and control of the theme in one scrolling page.</summary>
public partial class GalleryView : UserControl
{
    /// <summary>Creates the view, listing the icons of <paramref name="theme"/>.</summary>
    /// <param name="theme">Theme whose <c>Icon*</c> geometries are listed.</param>
    public GalleryView(SunmaoTheme theme)
    {
        InitializeComponent();
        IconList.ItemsSource = IconEntries(theme);
        SampleGrid.ItemsSource = new[]
        {
            new SampleRow("J-1041", "Done", 240, "09:42:10"),
            new SampleRow("J-1042", "Queued", 0, "09:42:14"),
            new SampleRow("J-1043", "Done", 118, "09:42:19"),
            new SampleRow("J-1044", "Failed", 12, "09:42:25")
        };
    }

    private static IReadOnlyList<IconEntry> IconEntries(SunmaoTheme theme)
    {
        var entries = new List<IconEntry>();
        foreach (var dictionary in theme.MergedDictionaries)
        {
            foreach (var key in dictionary.Keys.OfType<string>().Where(key => key.StartsWith("Icon", StringComparison.Ordinal)))
            {
                if (dictionary[key] is Geometry geometry)
                {
                    entries.Add(new IconEntry(key, geometry));
                }
            }
        }

        return entries.OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>One icon in the gallery.</summary>
    /// <param name="Name">Resource key.</param>
    /// <param name="Geometry">The stroked geometry.</param>
    public sealed record IconEntry(string Name, Geometry Geometry);

    /// <summary>One row of the sample data grid.</summary>
    /// <param name="Job">Job identifier.</param>
    /// <param name="Status">Job status.</param>
    /// <param name="Records">Records written.</param>
    /// <param name="Time">Start time.</param>
    public sealed record SampleRow(string Job, string Status, int Records, string Time);
}

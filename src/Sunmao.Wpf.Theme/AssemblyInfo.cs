using System.Windows;
using System.Windows.Markup;

// Default control templates live in Themes/Generic.xaml of this assembly.
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

// One XAML namespace for the whole package: xmlns:sm="urn:sunmao-wpf".
[assembly: XmlnsDefinition("urn:sunmao-wpf", "Sunmao.Wpf.Theme")]
[assembly: XmlnsDefinition("urn:sunmao-wpf", "Sunmao.Wpf.Theme.Controls")]
[assembly: XmlnsDefinition("urn:sunmao-wpf", "Sunmao.Wpf.Theme.Dialogs")]
[assembly: XmlnsPrefix("urn:sunmao-wpf", "sm")]

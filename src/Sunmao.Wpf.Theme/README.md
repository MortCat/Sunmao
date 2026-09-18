# Sunmao.Wpf.Theme

The complete look for WPF applications: design tokens, the Walnut Light/Dark palettes, a High
Contrast palette, Standard/Touch density, control styles, stroked icons and common controls. The
behaviour layer (MVVM, UI polling) is in `Sunmao.Wpf`; this package only covers the look.

Screenshots are in [`docs/images/theme-gallery`](../../docs/images/theme-gallery). The interactive
sample is `samples/Sunmao.Samples.ThemeGallery` (`--export <folder>` regenerates the screenshots).

## Usage

```xml
<!-- App.xaml -->
<Application xmlns:sm="urn:sunmao-wpf" ...>
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <sm:SunmaoTheme Variant="System" Density="Standard" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>

<!-- MainWindow.xaml -->
<Window Style="{DynamicResource AppWindow}" ...>
  <Button Style="{DynamicResource PrimaryButton}" Content="Start" />
  <sm:StatusChip Text="Online" Kind="Success" ShowDot="True" />
</Window>
```

Switching at run time (for example from a settings page):

```csharp
var theme = ThemeManager.Current;
theme.Variant = ThemeVariant.Dark;              // Light / Dark / HighContrast / System
theme.Density = ThemeDensity.Touch;             // touch panels
theme.Accent = Color.FromRgb(0x7E, 0x57, 0xC2); // brand colour; null restores the palette accent
```

`Variant="System"` follows the Windows app light/dark setting and high contrast mode, and updates
when they change.

## Layers

| Layer | Contents | Rule |
|---|---|---|
| Palette | `Themes/Palettes/Light.xaml`, `Dark.xaml`; High Contrast is built from `SystemColors` | all variants define exactly the same keys (tested) |
| Density | `Themes/Density/Standard.xaml`, `Touch.xaml` | Touch hit targets are at least 44 px (tested) |
| Foundation | `Themes/Shared/Foundation.xaml` (corners, font, text styles, Card, AppWindow, focus visual), `Icons.xaml` | no colours; colours always via `DynamicResource` |
| Styles | `Themes/Styles/*.xaml`: buttons, inputs, lists, DataGrid, tabs, navigation | mostly implicit styles for every control of a type |
| Controls | `Controls/*` + `Themes/Generic.xaml` | lookless: re-templatable, and content slots accept `x:Name` |

## Main resource keys

- **Surfaces and text**: `BackgroundBrush`, `SurfaceBrush`, `SurfaceSoftBrush`, `SurfaceMutedBrush`,
  `SurfaceHoverBrush`, `LineBrush`, `LineStrongBrush`, `TextPrimaryBrush`, `TextSecondaryBrush`,
  `TextTertiaryBrush` (meta text such as timestamps and identifiers only)
- **Accent**: `AccentBrush`, `AccentHoverBrush`, `AccentPressedBrush`, `AccentSoftBrush`,
  `AccentSecondaryBrush`, `TextOnAccentBrush`, `FocusBrush`
- **Chrome (application bar)**: `ChromeBrush`, `ChromeHoverBrush`, `ChromeSelectedBrush`,
  `TextOnChromeBrush`, `TextOnChromeMutedBrush`, `TextOnChromeSelectedBrush`, `OnlineBrush`
- **Status**: `{Success|Warning|Danger|Info}Brush`, `…SoftBrush`, `…TextBrush`, `DangerLineBrush`
- **Button styles**: `PrimaryButton`, `SecondaryButton` (default), `GhostButton`, `DangerButton`,
  `IconButton`, `ChromeIconButton`, `LinkButton`
- **Other styles**: `ToggleSwitch` (CheckBox), `ThinProgressBar`, `ChromeBar`, `ChromeTab`,
  `SegmentedHost`, `SegmentTab`, `FilterTab`, `Card`, `Panel`, `Divider`, `AppWindow`
- **Text styles**: `PageTitleText`, `PageSubtitleText`, `SectionTitleText`, `RowTitleText`,
  `BodyText`, `CaptionText`, `MetaText`
- **Icons**: `Icon*` (24×24 stroked icons shown with `IconGlyph`); the gallery screenshots list them
  all

## Controls

`IconGlyph`, `StatusChip`, `ConnectionPill`, `InfoCard`, `MetricTile`, `NoticeBar`, `PageHeader`,
`EmptyState`, `ListRow`, `CheckRow`, `DropZone`, and `ConfirmationDialogService`, which implements
`IConfirmationDialogService`. Status is always expressed as a `StatusKind` (Neutral, Success,
Warning, Danger, Info), never as a colour.

## Contrast

Tests enforce these ratios for Light and Dark: primary and secondary text ≥ 4.5:1, tertiary text
≥ 3:1, status text on its own tint ≥ 4.5:1, text on the accent ≥ 4.5:1. A custom accent picks black
or white text automatically to keep 4.5:1.

## Do not

- Hard-code colours in views or controls. A new colour gets the same key in every variant.
- Use icon fonts. New icons follow the 24×24 stroked style with round caps.
- Build text into controls (button captions, messages). The application passes it in, so it can be
  localized.

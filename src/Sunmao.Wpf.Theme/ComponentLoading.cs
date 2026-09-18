namespace Sunmao.Wpf.Theme;

/// <summary>
/// Serializes <c>Application.LoadComponent</c> calls made by this assembly.
/// </summary>
/// <remarks>
/// WPF reads compiled XAML through a process-wide resource package whose part is not thread-safe, so
/// two UI threads loading at the same moment can corrupt its stream list. Loads are short, never wait
/// on another thread, and run no user code, so a plain lock is safe here.
/// </remarks>
internal static class ComponentLoading
{
    private static readonly object Gate = new();

    public static T Run<T>(Func<T> load)
    {
        lock (Gate)
        {
            return load();
        }
    }

    public static void Run(Action load)
    {
        lock (Gate)
        {
            load();
        }
    }
}

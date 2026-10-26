using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;

namespace Miside.Camera.Desktop;

public sealed class Localization : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources = new("Miside.Camera.Desktop.Resources.Strings", typeof(Localization).Assembly);
    public static Localization Current { get; } = new();
    private CultureInfo culture = CultureInfo.GetCultureInfo("en");
    public string Language => culture.Name;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string this[string key] => Resources.GetString(key, culture) ?? throw new MissingManifestResourceException(key);

    public void Use(string language)
    {
        string code = language is "en" or "ru" ? language : "zh-CN";
        if (culture.Name == code) return;
        culture = CultureInfo.GetCultureInfo(code);
        PropertyChanged?.Invoke(this, new("Item[]"));
    }

    public static string T(string key, params object[] arguments) => string.Format(Current.culture, Current[key], arguments);
    public static LocalizedText M(string key, params object[] arguments) => new(key, arguments);
    public static LocalizedException E(string key, params object[] arguments) => new(M(key, arguments));
    public static LocalizedText MessageFor(Exception error) => error is LocalizedException localized ? localized.Text : M("ExternalMessage", error.Message);
}

public sealed record LocalizedText(string Key, params object[] Arguments)
{
    public override string ToString() => Localization.T(Key, Arguments);
}

public sealed class LocalizedException(LocalizedText text, Exception? inner = null) : IOException(null, inner)
{
    public LocalizedText Text { get; } = text;
    public override string Message => Text.ToString();
}

public sealed record LanguageOption(string Code, string Name);

public sealed class LogLine(DateTime time, LocalizedText message) : Observable
{
    public string Text => time.ToString("HH:mm:ss") + "  " + message;
    public void Refresh() => Changed(nameof(Text));
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{key}]") { Source = Localization.Current }.ProvideValue(serviceProvider);
}

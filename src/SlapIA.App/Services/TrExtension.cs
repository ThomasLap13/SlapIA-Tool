using System.Windows.Data;
using System.Windows.Markup;

namespace SlapIA.App.Services;

/// <summary>
/// XAML usage: Text="{loc:Tr SomeKey}". Binds to LocalizationService.Instance's indexer, so it
/// re-evaluates live (no view reload) whenever LocalizationService.SetLanguage() is called.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public class TrExtension : MarkupExtension
{
    public string Key { get; set; }

    public TrExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}

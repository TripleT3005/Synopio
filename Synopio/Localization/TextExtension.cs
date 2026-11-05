using System.Windows.Data;
using System.Windows.Markup;

namespace Synopio.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class TextExtension : MarkupExtension
{
    // Constructor
    public TextExtension()
    {
    }

    public TextExtension(string key)
    {
        Key = key;
    }

    // Properties
    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    // Methods
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}

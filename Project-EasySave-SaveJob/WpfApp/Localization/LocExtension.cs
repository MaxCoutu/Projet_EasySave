using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace Projet.Wpf.Localization
{
    [MarkupExtensionReturnType(typeof(BindingExpression))]
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocExtension(string key) => Key = key;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding($"[{Key}]")
            {
                Source = LocExtensionProvider.Instance,
                Mode = BindingMode.OneWay
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using POSApp.Core.Services;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// The words on the register. Bound from XAML as
    /// <c>{Binding [sale.save], Source={x:Static helpers:PhraseBook.Current}}</c>.
    /// An empty override falls back to the language the shop picked.
    /// </summary>
    public sealed class PhraseBook : INotifyPropertyChanged
    {
        public static PhraseBook Current { get; } = new();

        private ShopTextSettings _settings = ShopTextSettings.Default;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ShopTextSettings Settings => _settings;

        public string this[string key] => ShopPhrases.Resolve(_settings.Language, key, _settings.Overrides);

        public void Apply(ShopTextSettings settings)
        {
            _settings = settings ?? ShopTextSettings.Default;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(BindingIndexer));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Settings)));
        }

        /// <summary>WPF listens for this when a binding uses the indexer.</summary>
        private const string BindingIndexer = "Item[]";
    }

    /// <summary>One editable line on the wording screen. Blank custom text uses <see cref="DefaultText"/>.</summary>
    public sealed class PhraseRow : INotifyPropertyChanged
    {
        public PhraseRow(string key, string group, string defaultText, string customText)
        {
            Key = key;
            Group = group;
            _defaultText = defaultText;
            _customText = customText;
        }

        public string Key { get; }
        public string Group { get; }

        private string _defaultText;
        public string DefaultText
        {
            get => _defaultText;
            set { _defaultText = value; OnPropertyChanged(); }
        }

        private string _customText;
        public string CustomText
        {
            get => _customText;
            set { _customText = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static ObservableCollection<PhraseRow> Load(string? language, IReadOnlyDictionary<string, string>? overrides)
        {
            overrides ??= new Dictionary<string, string>();
            var rows = new ObservableCollection<PhraseRow>();
            foreach (var phrase in ShopPhrases.Catalog)
            {
                overrides.TryGetValue(phrase.Key, out var custom);
                rows.Add(new PhraseRow(phrase.Key, phrase.Group, ShopPhrases.BuiltIn(language, phrase.Key), custom ?? string.Empty));
            }
            return rows;
        }

        /// <summary>Only lines the shop actually changed. A line equal to the built-in text is not stored.</summary>
        public static Dictionary<string, string> CollectOverrides(IEnumerable<PhraseRow> rows) =>
            rows.Where(r => !string.IsNullOrWhiteSpace(r.CustomText)
                            && !string.Equals(r.CustomText.Trim(), r.DefaultText, StringComparison.Ordinal))
                .ToDictionary(r => r.Key, r => r.CustomText.Trim());
    }
}

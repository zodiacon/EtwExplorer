using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EtwExplorer.Infrastructure {
    /// <summary>
    /// <c>TabControlProperties.IsCached="True"</c> keeps every tab's content element alive instead of
    /// letting the <see cref="TabControl"/> rebuild it from the DataTemplate on each selection change,
    /// so per-tab UI state (scroll position, filters, selection) survives switching tabs.
    /// </summary>
    public static class TabControlProperties {
        public static readonly DependencyProperty IsCachedProperty = DependencyProperty.RegisterAttached(
            "IsCached", typeof(bool), typeof(TabControlProperties), new PropertyMetadata(false, OnIsCachedChanged));

        public static bool GetIsCached(DependencyObject obj) => (bool)obj.GetValue(IsCachedProperty);
        public static void SetIsCached(DependencyObject obj, bool value) => obj.SetValue(IsCachedProperty, value);

        static readonly DependencyProperty CacheProperty = DependencyProperty.RegisterAttached(
            "Cache", typeof(ContentCache), typeof(TabControlProperties));

        static void OnIsCachedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not TabControl tabControl)
                return;

            if ((bool)e.NewValue) {
                if (tabControl.IsLoaded)
                    Attach(tabControl);
                else
                    tabControl.Loaded += OnTabControlLoaded;
            }
            else {
                tabControl.Loaded -= OnTabControlLoaded;
                (tabControl.GetValue(CacheProperty) as ContentCache)?.Detach();
                tabControl.ClearValue(CacheProperty);
            }
        }

        static void OnTabControlLoaded(object sender, RoutedEventArgs e) {
            var tabControl = (TabControl)sender;
            tabControl.Loaded -= OnTabControlLoaded;
            Attach(tabControl);
        }

        static void Attach(TabControl tabControl) {
            if (tabControl.GetValue(CacheProperty) != null)
                return;

            tabControl.ApplyTemplate();
            var host = tabControl.Template?.FindName("PART_SelectedContentHost", tabControl) as ContentPresenter;
            if (host == null)
                return; // unknown template — leave the default (non-cached) behaviour in place

            tabControl.SetValue(CacheProperty, new ContentCache(tabControl, host));
        }

        sealed class ContentCache {
            readonly TabControl _tabControl;
            readonly ContentPresenter _host;
            readonly Grid _panel = new Grid();
            readonly Dictionary<object, ContentPresenter> _presenters = new Dictionary<object, ContentPresenter>();

            public ContentCache(TabControl tabControl, ContentPresenter host) {
                _tabControl = tabControl;
                _host = host;

                // The template's presenter is re-pointed at a panel holding one presenter per tab item;
                // only the selected item's presenter is visible.
                _host.ContentTemplate = null;
                _host.ContentTemplateSelector = null;
                _host.ContentStringFormat = null;
                _host.Content = _panel;

                _tabControl.SelectionChanged += OnSelectionChanged;
                if (_tabControl.ItemsSource is INotifyCollectionChanged collection)
                    collection.CollectionChanged += OnItemsChanged;
                ShowSelected();
            }

            public void Detach() {
                _tabControl.SelectionChanged -= OnSelectionChanged;
                if (_tabControl.ItemsSource is INotifyCollectionChanged collection)
                    collection.CollectionChanged -= OnItemsChanged;
                _panel.Children.Clear();
                _presenters.Clear();
                _host.ClearValue(ContentPresenter.ContentProperty);
            }

            void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
                if (ReferenceEquals(e.OriginalSource, _tabControl))
                    ShowSelected();
            }

            void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e) {
                if (e.Action == NotifyCollectionChangedAction.Reset) {
                    var live = new HashSet<object>();
                    foreach (var item in _tabControl.Items)
                        live.Add(item);
                    foreach (var stale in new List<object>(_presenters.Keys))
                        if (!live.Contains(stale))
                            Remove(stale);
                }
                else if (e.OldItems != null) {
                    foreach (var item in e.OldItems)
                        Remove(item);
                }
                ShowSelected();
            }

            void Remove(object item) {
                if (_presenters.Remove(item, out var presenter))
                    _panel.Children.Remove(presenter);
            }

            void ShowSelected() {
                var selected = _tabControl.SelectedItem;
                if (selected != null && !_presenters.ContainsKey(selected)) {
                    var presenter = new ContentPresenter {
                        Content = selected,
                        ContentTemplate = _tabControl.ContentTemplate,
                        ContentTemplateSelector = _tabControl.ContentTemplateSelector,
                        ContentStringFormat = _tabControl.ContentStringFormat,
                    };
                    _presenters[selected] = presenter;
                    _panel.Children.Add(presenter);
                }

                foreach (var pair in _presenters)
                    pair.Value.Visibility = ReferenceEquals(pair.Key, selected) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}

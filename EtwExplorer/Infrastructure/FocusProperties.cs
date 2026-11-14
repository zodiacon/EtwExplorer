using System.Windows;
using System.Windows.Input;

namespace EtwExplorer.Infrastructure {
	public static class FocusProperties {
		public static readonly DependencyProperty FocusOnLoadProperty = DependencyProperty.RegisterAttached(
			"FocusOnLoad", typeof(bool), typeof(FocusProperties), new PropertyMetadata(false, OnFocusOnLoadChanged));

		public static bool GetFocusOnLoad(DependencyObject obj) => (bool)obj.GetValue(FocusOnLoadProperty);
		public static void SetFocusOnLoad(DependencyObject obj, bool value) => obj.SetValue(FocusOnLoadProperty, value);

		static void OnFocusOnLoadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			if (d is not FrameworkElement element)
				return;

			if ((bool)e.NewValue)
				element.Loaded += OnLoaded;
			else
				element.Loaded -= OnLoaded;
		}

		static void OnLoaded(object sender, RoutedEventArgs e) {
			var element = (FrameworkElement)sender;
			// Defer so the focus request runs after the surrounding window has finished activating.
			element.Dispatcher.BeginInvoke(() => {
				element.Focus();
				Keyboard.Focus(element);
			}, System.Windows.Threading.DispatcherPriority.Input);
		}
	}
}

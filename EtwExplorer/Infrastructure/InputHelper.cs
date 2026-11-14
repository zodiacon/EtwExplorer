using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace EtwExplorer.Infrastructure {
    /// <summary>
    /// Turns the <see cref="MenuItem.InputGestureText"/> of a menu's child items into real
    /// <see cref="KeyBinding"/>s on the hosting window, so the displayed shortcuts actually work.
    /// </summary>
    public static class InputHelper {
        public static readonly DependencyProperty AttachInputBindingsProperty = DependencyProperty.RegisterAttached(
            "AttachInputBindings", typeof(bool), typeof(InputHelper), new PropertyMetadata(false, OnAttachInputBindingsChanged));

        public static bool GetAttachInputBindings(DependencyObject obj) => (bool)obj.GetValue(AttachInputBindingsProperty);
        public static void SetAttachInputBindings(DependencyObject obj, bool value) => obj.SetValue(AttachInputBindingsProperty, value);

        static readonly KeyGestureConverter GestureConverter = new KeyGestureConverter();

        static void OnAttachInputBindingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not MenuItem menu || DesignerProperties.GetIsInDesignMode(menu))
                return;

            if ((bool)e.NewValue)
                menu.Loaded += OnMenuLoaded;
            else
                menu.Loaded -= OnMenuLoaded;
        }

        static void OnMenuLoaded(object sender, RoutedEventArgs e) {
            var menu = (MenuItem)sender;
            menu.Loaded -= OnMenuLoaded;

            var window = Window.GetWindow(menu);
            if (window == null)
                return;

            foreach (var item in menu.Items) {
                if (item is not MenuItem child || child.Command == null || string.IsNullOrWhiteSpace(child.InputGestureText))
                    continue;

                KeyGesture gesture;
                try {
                    gesture = GestureConverter.ConvertFromInvariantString(child.InputGestureText) as KeyGesture;
                }
                catch (NotSupportedException) {
                    continue;
                }
                if (gesture == null)
                    continue;

                var binding = new KeyBinding(child.Command, gesture) { CommandTarget = child.CommandTarget };
                BindingOperations.SetBinding(binding, InputBinding.CommandParameterProperty,
                    new Binding(nameof(MenuItem.CommandParameter)) { Source = child });
                window.InputBindings.Add(binding);
            }
        }
    }
}

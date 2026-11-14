using System.Windows;

namespace EtwExplorer.Infrastructure {
	sealed class MessageBoxService : IMessageBoxService {
		Window? _owner;

		public void SetOwner(Window owner) {
			_owner = owner;
		}

		public MessageBoxResult ShowMessage(string text, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None) {
			var owner = _owner != null && _owner.IsLoaded ? _owner : Application.Current?.MainWindow;
			return owner != null
				? MessageBox.Show(owner, text, caption, button, image)
				: MessageBox.Show(text, caption, button, image);
		}
	}
}

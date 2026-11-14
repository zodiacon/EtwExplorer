using System.Windows;

namespace EtwExplorer.Infrastructure {
    public interface IFileDialogService {
        string GetFileForOpen(string filter, string title = null);
        string GetFileForSave(string filter, string title = null);
    }

    public interface IMessageBoxService {
        void SetOwner(Window owner);
        MessageBoxResult ShowMessage(string text, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None);
    }

    public interface IDialogService {
        TViewModel CreateDialog<TViewModel, TWindow>()
            where TViewModel : DialogViewModelBase
            where TWindow : Window, new();
    }

    public interface IUIServices {
        IFileDialogService FileDialogService { get; }
        IMessageBoxService MessageBoxService { get; }
        IDialogService DialogService { get; }
    }

    public sealed class UIServicesDefaults : IUIServices {
        public IFileDialogService FileDialogService { get; } = new FileDialogService();
        public IMessageBoxService MessageBoxService { get; } = new MessageBoxService();
        public IDialogService DialogService { get; } = new DialogService();
    }
}

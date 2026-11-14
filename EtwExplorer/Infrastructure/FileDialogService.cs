using Microsoft.Win32;

namespace EtwExplorer.Infrastructure {
	sealed class FileDialogService : IFileDialogService {
		public string? GetFileForOpen(string filter, string? title = null) {
			var dialog = new OpenFileDialog { Filter = filter, Title = title ?? string.Empty };
			return dialog.ShowDialog() == true ? dialog.FileName : null;
		}

		public string? GetFileForSave(string filter, string? title = null) {
			var dialog = new SaveFileDialog { Filter = filter, Title = title ?? string.Empty };
			return dialog.ShowDialog() == true ? dialog.FileName : null;
		}
	}
}

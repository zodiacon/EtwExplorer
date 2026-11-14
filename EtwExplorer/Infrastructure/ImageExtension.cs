using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace EtwExplorer.Infrastructure {
	/// <summary>
	/// <c>{Image /icons/open.ico, Width=16, Height=16}</c> — builds an <see cref="Image"/> for a pack-relative resource path.
	/// </summary>
	[MarkupExtensionReturnType(typeof(Image))]
	public sealed class ImageExtension : MarkupExtension {
		public ImageExtension() {
		}

		public ImageExtension(string source) {
			Source = source;
		}

		[ConstructorArgument("source")]
		public string? Source { get; set; }

		public double Width { get; set; } = double.NaN;
		public double Height { get; set; } = double.NaN;

		public override object ProvideValue(IServiceProvider serviceProvider) {
			var image = new Image { Width = Width, Height = Height, Stretch = System.Windows.Media.Stretch.Uniform };
			if (!string.IsNullOrEmpty(Source)) {
				var uri = Uri.TryCreate(Source, UriKind.Absolute, out var absolute)
					? absolute
					: new Uri($"pack://application:,,,{(Source.StartsWith('/') ? Source : "/" + Source)}", UriKind.Absolute);
				image.Source = new BitmapImage(uri);
			}
			return image;
		}
	}
}

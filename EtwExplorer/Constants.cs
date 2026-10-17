using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EtwExplorer {
	static class Constants {
		static readonly Assembly App = Assembly.GetExecutingAssembly();

		/// <summary>Product name, for message box captions.</summary>
		public static string AppTitle { get; } =
			App.GetCustomAttribute<AssemblyProductAttribute>().Product;

		/// <summary>
		/// The main window title. Every part of it comes from the assembly manifest, so
		/// a release edits AssemblyInfo.cs and nothing else.
		/// </summary>
		public static string WindowTitle { get; } = string.Join(" ",
			AppTitle,
			"v" + App.GetName().Version.ToString(2),
			App.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright);
	}
}

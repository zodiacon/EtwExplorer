using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EtwExplorer.ViewModels {
	abstract class TabViewModelBase : BindableBase {
		public abstract string Icon { get; }
		public abstract string Header { get; }
	}
}

using System;

namespace ConfuserNext.ViewModel {
	public interface IViewModel<TModel> {
		TModel Model { get; }
	}
}
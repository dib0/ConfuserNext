using System;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace ConfuserNext.Core {
	/// <summary>
	///     The listener of module writer event.
	/// </summary>
	public class ModuleWriterListener {
		/// <summary>
		///     Subscribes this listener to the writer events raised by the specified options.
		/// </summary>
		/// <param name="options">The module writer options to listen to.</param>
		public void RegisterTo(ModuleWriterOptionsBase options) {
			options.WriterEvent += OnModuleWriterEvent;
		}

		void OnModuleWriterEvent(object sender, ModuleWriterEventArgs e) {
			if (e.Event == ModuleWriterEvent.PESectionsCreated)
				NativeEraser.Erase(e.Writer as NativeModuleWriter, e.Writer.Module as ModuleDefMD);
			if (OnWriterEvent != null) {
				OnWriterEvent(e.Writer, new ModuleWriterListenerEventArgs(e.Event));
			}
		}

		/// <summary>
		///     Occurs when a module writer event is triggered.
		/// </summary>
		public event EventHandler<ModuleWriterListenerEventArgs> OnWriterEvent;
	}

	/// <summary>
	///     Indicates the triggered writer event.
	/// </summary>
	public class ModuleWriterListenerEventArgs : EventArgs {
		/// <summary>
		///     Initializes a new instance of the <see cref="ModuleWriterListenerEventArgs" /> class.
		/// </summary>
		/// <param name="evt">The triggered writer event.</param>
		public ModuleWriterListenerEventArgs(ModuleWriterEvent evt) {
			WriterEvent = evt;
		}

		/// <summary>
		///     Gets the triggered writer event.
		/// </summary>
		/// <value>The triggered writer event.</value>
		public ModuleWriterEvent WriterEvent { get; private set; }
	}
}

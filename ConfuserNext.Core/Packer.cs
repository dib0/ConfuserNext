using System;
using System.Collections.Generic;
using System.IO;
using ConfuserNext.Core.Project;
using dnlib.DotNet;

namespace ConfuserNext.Core {
	/// <summary>
	///     Base class of Confuser packers.
	/// </summary>
	/// <remarks>
	///     A parameterless constructor must exists in derived classes to enable plugin discovery.
	/// </remarks>
	public abstract class Packer : ConfuserComponent {
		/// <summary>
		///     Executes the packer.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="parameters">The parameters of packer.</param>
		protected internal abstract void Pack(ConfuserContext context, ProtectionParameters parameters);

		/// <summary>
		///     Protects the stub using original project settings replace the current output with the protected stub.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="fileName">The result file name.</param>
		/// <param name="module">The stub module.</param>
		/// <param name="snKey">The strong name key.</param>
		/// <param name="prot">The packer protection that applies to the stub.</param>
		protected void ProtectStub(ConfuserContext context, string fileName, byte[] module, StrongNameKey snKey, Protection prot = null) {
			string tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			string outDir = Path.Combine(tmpDir, Path.GetRandomFileName());
			Directory.CreateDirectory(tmpDir);

			for (int i = 0; i < context.OutputModules.Count; i++) {
				// context.OutputPaths[i] == fileName for the module this stub replaces (Compressor
				// always passes its own context.OutputPaths[ModuleIndex] as fileName) - writing its
				// stale pre-compression bytes here just to immediately overwrite them below is
				// wasted work, and on Windows the write-then-immediately-rewrite-then-read of the
				// same file can race against the on-write antivirus scan lock, intermittently
				// failing the dnlib reload a few lines down with a sharing-violation IOException.
				if (context.OutputPaths[i] == fileName)
					continue;
				string path = Path.GetFullPath(Path.Combine(tmpDir, context.OutputPaths[i]));
				var dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				WriteFileThenAwaitReadable(path, context.OutputModules[i]);
			}
			string stubPath = Path.GetFullPath(Path.Combine(tmpDir, fileName));
			string stubDir = Path.GetDirectoryName(stubPath);
			if (!Directory.Exists(stubDir))
				Directory.CreateDirectory(stubDir);
			WriteFileThenAwaitReadable(stubPath, module);

			var proj = new ConfuserProject();
			proj.Seed = context.Project.Seed;
			foreach (Rule rule in context.Project.Rules)
				proj.Rules.Add(rule);
			proj.Add(new ProjectModule {
				Path = fileName
			});
			proj.BaseDirectory = tmpDir;
			proj.OutputDirectory = outDir;
			foreach (var path in context.Project.ProbePaths)
				proj.ProbePaths.Add(path);
			proj.ProbePaths.Add(context.Project.BaseDirectory);

			PluginDiscovery discovery = null;
			if (prot != null) {
				var rule = new Rule {
					Preset = ProtectionPreset.None,
					Inherit = true,
					Pattern = "true"
				};
				rule.Add(new SettingItem<Protection> {
					Id = prot.Id,
					Action = SettingItemAction.Add
				});
				proj.Rules.Add(rule);
				discovery = new PackerDiscovery(prot);
			}

			try {
				ConfuserEngine.Run(new ConfuserParameters {
					Logger = new PackerLogger(context.Logger),
					PluginDiscovery = discovery,
					Marker = new PackerMarker(snKey),
					Project = proj,
					PackerInitiated = true
				}, context.token).Wait();
			}
			catch (AggregateException ex) {
				context.Logger.Error("Failed to protect packer stub.");
				throw new ConfuserException(ex);
			}

			context.OutputModules = new[] { File.ReadAllBytes(Path.Combine(outDir, fileName)) };
			context.OutputPaths = new[] { fileName };
		}

		/// <summary>
		///     Writes <paramref name="data" /> to <paramref name="path" />, then waits until the
		///     file can be opened for exclusive reading before returning. The caller
		///     (<see cref="ProtectStub" />) immediately hands this freshly-written file to a
		///     recursive <see cref="ConfuserEngine.Run" />, which memory-maps it for reading almost
		///     instantly; on Windows, antivirus real-time protection commonly grabs a brief
		///     exclusive lock on a newly created executable file for scanning, which can otherwise
		///     make that reload fail with a sharing-violation IOException. Waiting here first lets
		///     that scan happen (and release its lock) before the race-prone read.
		/// </summary>
		static void WriteFileThenAwaitReadable(string path, byte[] data) {
			File.WriteAllBytes(path, data);

			const int maxAttempts = 20;
			for (int attempt = 1; attempt <= maxAttempts; attempt++) {
				try {
					using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
						return;
					}
				}
				// A file briefly locked for scanning reports as a sharing-violation IOException in
				// most cases, but .NET surfaces the exact same underlying Win32 error as
				// UnauthorizedAccessException instead whenever the open also requests FileAccess.Read
				// and the holder's share flags don't permit it - both are the same transient
				// condition here, not a real permissions problem (we created this file ourselves).
				catch (Exception ex) when (attempt < maxAttempts && (ex is IOException || ex is UnauthorizedAccessException)) {
					System.Threading.Thread.Sleep(50);
				}
			}
		}
	}

	internal class PackerLogger : ILogger {
		readonly ILogger baseLogger;

		public PackerLogger(ILogger baseLogger) {
			this.baseLogger = baseLogger;
		}

		public void Debug(string msg) {
			baseLogger.Debug(msg);
		}

		public void DebugFormat(string format, params object[] args) {
			baseLogger.DebugFormat(format, args);
		}

		public void Info(string msg) {
			baseLogger.Info(msg);
		}

		public void InfoFormat(string format, params object[] args) {
			baseLogger.InfoFormat(format, args);
		}

		public void Warn(string msg) {
			baseLogger.Warn(msg);
		}

		public void WarnFormat(string format, params object[] args) {
			baseLogger.WarnFormat(format, args);
		}

		public void WarnException(string msg, Exception ex) {
			baseLogger.WarnException(msg, ex);
		}

		public void Error(string msg) {
			baseLogger.Error(msg);
		}

		public void ErrorFormat(string format, params object[] args) {
			baseLogger.ErrorFormat(format, args);
		}

		public void ErrorException(string msg, Exception ex) {
			baseLogger.ErrorException(msg, ex);
		}

		public void Progress(int progress, int overall) {
			baseLogger.Progress(progress, overall);
		}

		public void EndProgress() {
			baseLogger.EndProgress();
		}

		public void Finish(bool successful) {
			if (!successful)
				throw new ConfuserException(null);
			baseLogger.Info("Finish protecting packer stub.");
		}
	}

	internal class PackerMarker : Marker {
		readonly StrongNameKey snKey;

		public PackerMarker(StrongNameKey snKey) {
			this.snKey = snKey;
		}

		protected internal override MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context) {
			MarkerResult result = base.MarkProject(proj, context);
			foreach (ModuleDefMD module in result.Modules)
				context.Annotations.Set(module, SNKey, snKey);
			return result;
		}
	}

	internal class PackerDiscovery : PluginDiscovery {
		readonly Protection prot;

		public PackerDiscovery(Protection prot) {
			this.prot = prot;
		}

		protected override void GetPluginsInternal(ConfuserContext context, IList<Protection> protections, IList<Packer> packers, IList<ConfuserComponent> components) {
			base.GetPluginsInternal(context, protections, packers, components);
			protections.Add(prot);
		}
	}
}
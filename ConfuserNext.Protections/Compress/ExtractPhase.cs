using System;
using System.Collections.Generic;
using System.Text;
using ConfuserNext.Core;
using ConfuserNext.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;

namespace ConfuserNext.Protections.Compress {
	internal class ExtractPhase : ProtectionPhase {
		public ExtractPhase(Compressor parent) : base(parent) { }

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.Modules; }
		}

		public override string Name {
			get { return "Packer info extraction"; }
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			if (context.Packer == null)
				return;

			bool isExe = context.CurrentModule.Kind == ModuleKind.Windows ||
			             context.CurrentModule.Kind == ModuleKind.Console;

			if (context.Annotations.Get<CompressorContext>(context, Compressor.ContextKey) != null) {
				if (isExe) {
					context.Logger.Error("Too many executable modules!");
					throw new ConfuserException(null);
				}
				return;
			}

			if (isExe) {
				bool compatMode = parameters.GetParameter(context, null, "compat", false);

				// The non-compat technique embeds the protected module as a second, manifest-less
				// module in a multi-module assembly, loaded at startup via Assembly.LoadModule.
				// CoreCLR (every .NET (Core) release, 1.0 through 10+) never implemented multi-module
				// assemblies: LoadModule throws NotImplementedException unconditionally there, so
				// that technique can only ever work against a .NET Framework target. Compat mode's
				// Assembly.Load-based technique has no such limitation, so it is forced on here
				// regardless of the requested setting - there is no working non-compat option for a
				// CoreCLR target, so silently producing (non-functional) non-compat output would be
				// worse than ignoring the request.
				if (!compatMode && context.CurrentModule.TargetsCoreCLR()) {
					context.Logger.Warn(
						"Compressor: 'compat' was not requested, but the target module runs on " +
						"CoreCLR (.NET (Core) 1.0+), which never implemented Assembly.LoadModule - " +
						"the non-compat packer technique cannot work there. Forcing compat mode.");
					compatMode = true;
				}

				var ctx = new CompressorContext {
					ModuleIndex = context.CurrentModuleIndex,
					Assembly = context.CurrentModule.Assembly,
					CompatMode = compatMode
				};
				context.Annotations.Set(context, Compressor.ContextKey, ctx);

				ctx.ModuleName = context.CurrentModule.Name;
				ctx.EntryPoint = context.CurrentModule.EntryPoint;
				ctx.Kind = context.CurrentModule.Kind;

				if (!ctx.CompatMode) {
					RandomGenerator random = context.Registry.GetService<IRandomService>().GetRandomGenerator(Compressor._Id);
					ctx.EmbeddedModuleName = GenerateModuleName(random);
					context.CurrentModule.Name = ctx.EmbeddedModuleName;
					context.CurrentModule.EntryPoint = null;
					context.CurrentModule.Kind = ModuleKind.NetModule;
				}

				context.CurrentModuleWriterListener.OnWriterEvent += new ResourceRecorder(ctx, context.CurrentModule).OnWriterEvent;
			}
		}

		const string NameChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

		static string GenerateModuleName(RandomGenerator random) {
			int len = 5 + random.NextInt32(6); // 5-10 chars
			var sb = new StringBuilder(len);
			for (int i = 0; i < len; i++)
				sb.Append(NameChars[random.NextInt32(NameChars.Length)]);
			return sb.ToString();
		}

		class ResourceRecorder {
			readonly CompressorContext ctx;
			ModuleDef targetModule;

			public ResourceRecorder(CompressorContext ctx, ModuleDef module) {
				this.ctx = ctx;
				targetModule = module;
			}

			public void OnWriterEvent(object sender, ModuleWriterListenerEventArgs e) {
				if (e.WriterEvent == ModuleWriterEvent.MDEndAddResources) {
					var writer = (ModuleWriterBase)sender;
					ctx.ManifestResources = new List<Tuple<uint, uint, string>>();
					var manifestResourceTable = writer.Metadata.TablesHeap.ManifestResourceTable;
					// Read the resource name directly from the module's own Resources list rather
					// than re-decoding it from the #Strings heap: at this point (MDEndAddResources,
					// which fires mid-write) a freshly-added string's RawManifestResourceRow.Name
					// may still be dnlib's internal "offset id" (StringsHeap.Add's return value,
					// flagged with bit 31) rather than its final on-disk heap offset - that offset
					// is only assigned once the heap is optimized and frozen later in the write.
					// Looking up such an id directly in the heap's raw-data dictionary throws
					// KeyNotFoundException. dnlib writes ManifestResource rows in the exact order of
					// targetModule.Resources (row rid == Resources[rid - 1]), so indexing that list
					// gives the real name without touching heap offsets at all.
					for (uint rid = 1; rid <= (uint)manifestResourceTable.Rows; rid++) {
						RawManifestResourceRow resource = manifestResourceTable[rid];
						string name = targetModule.Resources[(int)(rid - 1)].Name;
						ctx.ManifestResources.Add(Tuple.Create(resource.Offset, resource.Flags, name));
					}
					ctx.EntryPointToken = writer.Metadata.GetToken(ctx.EntryPoint).Raw;
				}
			}
		}
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
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
				var ctx = new CompressorContext {
					ModuleIndex = context.CurrentModuleIndex,
					Assembly = context.CurrentModule.Assembly,
					CompatMode = parameters.GetParameter(context, null, "compat", false)
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
					Dictionary<uint, byte[]> stringDict = writer.Metadata.StringsHeap.GetAllRawData().ToDictionary(pair => pair.Key, pair => pair.Value);
					var manifestResourceTable = writer.Metadata.TablesHeap.ManifestResourceTable;
					for (uint rid = 1; rid <= (uint)manifestResourceTable.Rows; rid++) {
						RawManifestResourceRow resource = manifestResourceTable[rid];
						ctx.ManifestResources.Add(Tuple.Create(resource.Offset, resource.Flags, Encoding.UTF8.GetString(stringDict[resource.Name])));
					}
					ctx.EntryPointToken = writer.Metadata.GetToken(ctx.EntryPoint).Raw;
				}
			}
		}
	}
}
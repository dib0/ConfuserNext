using System;
using System.Diagnostics;
using System.Linq;
using ConfuserNext.Core;
using ConfuserNext.Core.Services;
using ConfuserNext.DynCipher;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;

namespace ConfuserNext.Protections.ControlFlow {
	internal class ControlFlowPhase : ProtectionPhase {
		static readonly JumpMangler Jump = new JumpMangler();
		static readonly SwitchMangler Switch = new SwitchMangler();

		public ControlFlowPhase(ControlFlowProtection parent)
			: base(parent) { }

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.Methods; }
		}

		public override string Name {
			get { return "Control flow mangling"; }
		}

		CFContext ParseParameters(MethodDef method, ConfuserContext context, ProtectionParameters parameters, RandomGenerator random, bool disableOpti) {
			var ret = new CFContext();
			ret.Type = parameters.GetParameter(context, method, "type", CFType.Switch);
			ret.Predicate = parameters.GetParameter(context, method, "predicate", PredicateType.Normal);

			int rawIntensity = parameters.GetParameter(context, method, "intensity", 60);
			ret.Intensity = rawIntensity / 100.0;
			ret.Depth = parameters.GetParameter(context, method, "depth", 4);

			ret.JunkCode = parameters.GetParameter(context, method, "junk", false) && !disableOpti;
			ret.DecoyEH = parameters.GetParameter(context, method, "decoyEH", true);

			ret.Protection = (ControlFlowProtection)Parent;
			ret.Random = random;
			ret.Method = method;
			ret.Context = context;
			ret.DynCipher = context.Registry.GetService<IDynCipherService>();

			if (ret.Predicate == PredicateType.x86) {
				if ((context.CurrentModule.Cor20HeaderFlags & ComImageFlags.ILOnly) != 0)
					context.CurrentModuleWriterOptions.Cor20HeaderOptions.Flags &= ~ComImageFlags.ILOnly;
			}

			return ret;
		}

		static bool DisabledOptimization(ModuleDef module) {
			bool disableOpti = false;
			CustomAttribute debugAttr = module.Assembly.CustomAttributes.Find("System.Diagnostics.DebuggableAttribute");
			if (debugAttr != null) {
				if (debugAttr.ConstructorArguments.Count == 1)
					disableOpti |= ((DebuggableAttribute.DebuggingModes)(int)debugAttr.ConstructorArguments[0].Value & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
				else
					disableOpti |= (bool)debugAttr.ConstructorArguments[1].Value;
			}
			debugAttr = module.CustomAttributes.Find("System.Diagnostics.DebuggableAttribute");
			if (debugAttr != null) {
				if (debugAttr.ConstructorArguments.Count == 1)
					disableOpti |= ((DebuggableAttribute.DebuggingModes)(int)debugAttr.ConstructorArguments[0].Value & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
				else
					disableOpti |= (bool)debugAttr.ConstructorArguments[1].Value;
			}
			return disableOpti;
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			bool disabledOpti = DisabledOptimization(context.CurrentModule);
			RandomGenerator random = context.Registry.GetService<IRandomService>().GetRandomGenerator(ControlFlowProtection._FullId);

			foreach (MethodDef method in parameters.Targets.OfType<MethodDef>().WithProgress(context.Logger))
				if (method.HasBody && method.Body.Instructions.Count > 0) {
					ProcessMethod(method.Body, ParseParameters(method, context, parameters, random, disabledOpti));
					context.CheckCancellation();
				}
		}

		static ManglerBase GetMangler(CFType type) {
			if (type == CFType.Switch)
				return Switch;
			return Jump;
		}

		/// <summary>
		///     Prepends a self-contained try/catch(Exception) around a couple of junk instructions,
		///     entirely new code that runs before (and falls through unconditionally into) the
		///     method's real first instruction. It never encloses any of the method's own logic, so
		///     it is always safe to add regardless of what the method actually does; its only purpose
		///     is to clutter decompiler output with exception-handling noise that has no bearing on
		///     the method's real behaviour, making the real control flow harder to pick out at a
		///     glance (ILSpy/dnSpy render every try/catch region whether or not it is ever entered).
		/// </summary>
		static void AddDecoyExceptionHandler(CilBody body, CFContext ctx) {
			if (body.Instructions.Count == 0)
				return;

			Instruction originalFirst = body.Instructions[0];
			TypeRef exceptionType = ctx.Method.Module.CorLibTypes.GetTypeRef("System", "Exception");

			Instruction tryStart = Instruction.CreateLdcI4(ctx.Random.NextInt32());
			Instruction pop = Instruction.Create(OpCodes.Pop);
			Instruction leaveTry = Instruction.Create(OpCodes.Leave, originalFirst);
			Instruction catchStart = Instruction.Create(OpCodes.Pop); // discards the caught exception object
			Instruction leaveHandler = Instruction.Create(OpCodes.Leave, originalFirst);

			var prefix = new[] { tryStart, pop, leaveTry, catchStart, leaveHandler };
			for (int i = prefix.Length - 1; i >= 0; i--)
				body.Instructions.Insert(0, prefix[i]);

			body.ExceptionHandlers.Insert(0, new ExceptionHandler(ExceptionHandlerType.Catch) {
				TryStart = tryStart,
				TryEnd = catchStart,
				HandlerStart = catchStart,
				HandlerEnd = originalFirst,
				CatchType = exceptionType
			});
		}

		void ProcessMethod(CilBody body, CFContext ctx) {
			if (ctx.DecoyEH && ctx.Intensity > ctx.Random.NextDouble())
				AddDecoyExceptionHandler(body, ctx);

            if (!MaxStackCalculator.GetMaxStack(body.Instructions, body.ExceptionHandlers, out uint maxStack))
            {
                ctx.Context.Logger.Error("Failed to calcuate maxstack.");
                throw new ConfuserException(null);
            }
            body.MaxStack = (ushort)maxStack;
			ScopeBlock root = BlockParser.ParseBody(body);

			GetMangler(ctx.Type).Mangle(body, root, ctx);

			body.Instructions.Clear();
			root.ToBody(body);
			foreach (ExceptionHandler eh in body.ExceptionHandlers) {
				var index = body.Instructions.IndexOf(eh.TryEnd) + 1;
				eh.TryEnd = index < body.Instructions.Count ? body.Instructions[index] : null;
				index = body.Instructions.IndexOf(eh.HandlerEnd) + 1;
				eh.HandlerEnd = index < body.Instructions.Count ? body.Instructions[index] : null;
			}
			body.KeepOldMaxStack = true;
		}
	}
}
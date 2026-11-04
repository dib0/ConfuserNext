using System;
using System.Collections.Generic;
using ConfuserNext.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ConfuserNext.Protections.ReferenceProxy {
	internal class MildMode : RPMode {
		// proxy method, { opCode, calling type, target method}
		readonly Dictionary<Tuple<Code, TypeDef, IMethod>, MethodDef> proxies = new Dictionary<Tuple<Code, TypeDef, IMethod>, MethodDef>();

		public override void ProcessCall(RPContext ctx, int instrIndex) {
			Instruction invoke = ctx.Body.Instructions[instrIndex];
			var target = (IMethod)invoke.Operand;

			// Value type proxy is not supported in mild mode.
			if (target.DeclaringType.ResolveTypeDefThrow().IsValueType)
				return;
			// Skipping visibility is not supported in mild mode.
			if (!target.ResolveThrow().IsPublic && !target.ResolveThrow().IsAssembly)
				return;

			Tuple<Code, TypeDef, IMethod> key = Tuple.Create(invoke.OpCode.Code, ctx.Method.DeclaringType, target);
            if (!proxies.TryGetValue(key, out MethodDef proxy))
            {
                MethodSig sig = CreateProxySignature(ctx, target, invoke.OpCode.Code == Code.Newobj);

                proxy = new MethodDefUser(ctx.Name.RandomName(), sig);
                proxy.Attributes = MethodAttributes.PrivateScope | MethodAttributes.Static;
                proxy.ImplAttributes = MethodImplAttributes.Managed | MethodImplAttributes.IL;
                ctx.Method.DeclaringType.Methods.Add(proxy);

                // Fix peverify --- Non-virtual call to virtual methods must be done on this pointer
                if (invoke.OpCode.Code == Code.Call && target.ResolveThrow().IsVirtual)
                {
                    proxy.IsStatic = false;
                    sig.HasThis = true;
                    sig.Params.RemoveAt(0);
                }

                ctx.Marker.Mark(proxy, ctx.Protection);
                ctx.Name.Analyze(proxy);
                ctx.Name.SetCanRename(proxy, false);

                proxy.Body = new CilBody();
                for (int i = 0; i < proxy.Parameters.Count; i++)
                    proxy.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, proxy.Parameters[i]));
                // Pad with a random number of semantically-inert nops. Several third-party
                // ConfuserEx unpackers (e.g. NoFuserEx's ProxyDeobfuscator) identify these proxy
                // stubs by an exact "body.Count == paramCount + 2" check; varying the length per
                // proxy defeats that fixed-shape match without changing behavior.
                int junk = 1 + ctx.Random.NextInt32(4);
                for (int i = 0; i < junk; i++)
                    proxy.Body.Instructions.Add(Instruction.Create(OpCodes.Nop));
                proxy.Body.Instructions.Add(Instruction.Create(invoke.OpCode, target));
                proxy.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

                proxies[key] = proxy;
            }

            invoke.OpCode = OpCodes.Call;
			if (ctx.Method.DeclaringType.HasGenericParameters) {
				var genArgs = new GenericVar[ctx.Method.DeclaringType.GenericParameters.Count];
				for (int i = 0; i < genArgs.Length; i++)
					genArgs[i] = new GenericVar(i);

				invoke.Operand = new MemberRefUser(
					ctx.Module,
					proxy.Name,
					proxy.MethodSig,
					new GenericInstSig((ClassOrValueTypeSig)ctx.Method.DeclaringType.ToTypeSig(), genArgs).ToTypeDefOrRef());
			}
			else
				invoke.Operand = proxy;

			var targetDef = target.ResolveMethodDef();
			if (targetDef != null)
				ctx.Context.Annotations.Set(targetDef, ReferenceProxyProtection.Targeted, ReferenceProxyProtection.Targeted);
		}

		public override void Finalize(RPContext ctx) { }
	}
}
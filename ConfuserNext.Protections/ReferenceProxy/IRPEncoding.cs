using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ConfuserNext.Protections.ReferenceProxy {
	internal interface IRPEncoding {
		Instruction[] EmitDecode(MethodDef init, RPContext ctx, Instruction[] arg);
		int Encode(MethodDef init, RPContext ctx, int value);
	}
}
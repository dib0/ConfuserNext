using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ConfuserNext.Protections.Resources {
	internal interface IEncodeMode {
		IEnumerable<Instruction> EmitDecrypt(MethodDef init, REContext ctx, Local block, Local key);
		uint[] Encrypt(uint[] data, int offset, uint[] key);
	}
}
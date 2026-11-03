using System;
using System.Collections.Generic;
using ConfuserNext.Core;
using ConfuserNext.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ConfuserNext.Protections.Compress {
	internal enum Mode {
		Normal,
		Dynamic
	}

	internal interface IKeyDeriver {
		void Init(ConfuserContext ctx, RandomGenerator random);
		uint[] DeriveKey(uint[] a, uint[] b);
		IEnumerable<Instruction> EmitDerivation(MethodDef method, ConfuserContext ctx, Local dst, Local src);
	}
}
using System;
using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace ConfuserNext.Protections.ControlFlow {
	internal interface IPredicate {
		void Init(CilBody body);
		void EmitSwitchLoad(IList<Instruction> instrs);
		int GetSwitchKey(int key);
	}
}
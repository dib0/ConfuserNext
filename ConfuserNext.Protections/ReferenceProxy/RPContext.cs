using System;
using System.Collections.Generic;
using ConfuserNext.Core;
using ConfuserNext.Core.Services;
using ConfuserNext.DynCipher;
using ConfuserNext.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ConfuserNext.Protections.ReferenceProxy {
	internal enum Mode {
		Mild,
		Strong,
		Ftn
	}

	internal enum EncodingType {
		Normal,
		Expression,
		x86
	}

	internal class RPContext {
		public ReferenceProxyProtection Protection;
		public CilBody Body;
		public HashSet<Instruction> BranchTargets;
		public ConfuserContext Context;
		public Dictionary<MethodSig, TypeDef> Delegates;
		public int Depth;
		public IDynCipherService DynCipher;
		public EncodingType Encoding;
		public IRPEncoding EncodingHandler;
		public int InitCount;
		public bool InternalAlso;
		public IMarkerService Marker;
		public MethodDef Method;
		public Mode Mode;

		public RPMode ModeHandler;
		public ModuleDef Module;
		public INameService Name;
		public RandomGenerator Random;
		public bool TypeErasure;
	}
}
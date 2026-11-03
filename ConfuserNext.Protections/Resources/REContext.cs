using System;
using ConfuserNext.Core;
using ConfuserNext.Core.Services;
using ConfuserNext.DynCipher;
using ConfuserNext.Renamer;
using dnlib.DotNet;

namespace ConfuserNext.Protections.Resources {
	internal class REContext {
		public ConfuserContext Context;

		public FieldDef DataField;
		public TypeDef DataType;
		public IDynCipherService DynCipher;
		public MethodDef InitMethod;
		public IMarkerService Marker;

		public Mode Mode;

		public IEncodeMode ModeHandler;
		public ModuleDef Module;
		public INameService Name;
		public RandomGenerator Random;
	}
}
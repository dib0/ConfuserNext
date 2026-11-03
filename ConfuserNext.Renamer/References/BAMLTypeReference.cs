using System;
using ConfuserNext.Core;
using ConfuserNext.Renamer.BAML;
using dnlib.DotNet;

namespace ConfuserNext.Renamer.References {
	internal class BAMLTypeReference : INameReference<TypeDef> {
		readonly TypeInfoRecord rec;
		readonly TypeSig sig;

		public BAMLTypeReference(TypeSig sig, TypeInfoRecord rec) {
			this.sig = sig;
			this.rec = rec;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			rec.TypeFullName = sig.ReflectionFullName;
			return true;
		}

		public bool ShouldCancelRename() {
			return false;
		}
	}
}
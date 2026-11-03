using System;
using ConfuserNext.Core;
using dnlib.DotNet;

namespace ConfuserNext.Renamer.References {
	public class TypeRefReference : INameReference<TypeDef> {
		readonly TypeDef typeDef;
		readonly TypeRef typeRef;

		public TypeRefReference(TypeRef typeRef, TypeDef typeDef) {
			this.typeRef = typeRef;
			this.typeDef = typeDef;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			typeRef.Namespace = typeDef.Namespace;
			typeRef.Name = typeDef.Name;
			return true;
		}

		public bool ShouldCancelRename() {
			return false;
		}
	}
}
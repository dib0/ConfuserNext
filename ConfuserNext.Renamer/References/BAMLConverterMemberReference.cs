using System;
using ConfuserNext.Core;
using ConfuserNext.Renamer.BAML;
using dnlib.DotNet;

namespace ConfuserNext.Renamer.References {
	internal class BAMLConverterMemberReference : INameReference<IDnlibDef> {
		readonly IDnlibDef member;
		readonly PropertyRecord rec;
		readonly TypeSig sig;
		readonly BAMLAnalyzer.XmlNsContext xmlnsCtx;

		public BAMLConverterMemberReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, IDnlibDef member, PropertyRecord rec) {
			this.xmlnsCtx = xmlnsCtx;
			this.sig = sig;
			this.member = member;
			this.rec = rec;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			string typeName = sig.ReflectionName;
			string prefix = xmlnsCtx.GetPrefix(sig.ReflectionNamespace, sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module.Assembly);
			if (!string.IsNullOrEmpty(prefix))
				typeName = prefix + ":" + typeName;
			rec.Value = typeName + "." + member.Name;
			return true;
		}

		public bool ShouldCancelRename() {
			return false;
		}
	}
}
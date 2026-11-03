using System;
using ConfuserNext.Core;

namespace ConfuserNext.Renamer {
	public interface INameReference {
		bool UpdateNameReference(ConfuserContext context, INameService service);

		bool ShouldCancelRename();
	}

	public interface INameReference<out T> : INameReference { }
}
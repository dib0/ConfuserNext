using System;
using ConfuserNext.Core;

namespace ConfuserNext.Protections.AntiTamper {
	internal interface IModeHandler {
		void HandleInject(AntiTamperProtection parent, ConfuserContext context, ProtectionParameters parameters);
		void HandleMD(AntiTamperProtection parent, ConfuserContext context, ProtectionParameters parameters);
	}
}
﻿using NDesk.Options;
using OTAPI.Patcher.Engine.Extensions;
using OTAPI.Patcher.Engine.Modification;

using System;

namespace OTAPI.Patcher.Engine.Modifications.Hooks.Net
{
	public class SendData : ModificationBase
	{
		public override string Description => "Hooking NetMessage.SendData...";

		public override void Run()
		{
			var vanilla = SourceDefinition.Type("Terraria.NetPlay").Method("SendData");
			var callback = ModificationDefinition.Type("OTAPI.Core.Callbacks.Terraria").Method("SendData");

			//Few stack issues arose trying to inject a callack before for lock, so i'll resort to 
			//wrapping the method;

			vanilla.Wrap(callback, null, true);

			Console.WriteLine("Done");
		}
	}
}
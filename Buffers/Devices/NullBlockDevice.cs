﻿using System;

namespace Buffers.Devices
{
	public sealed class NullBlockDevice : BlockDeviceBase
	{
		public override string Name { get { return "NullBlock"; } }
		public override uint PageSize { get { return 0; } protected set { } }
		protected override void DoRead(uint pageid, byte[] result) { }
		protected override void DoWrite(uint pageid, byte[] data) { }
	}
}
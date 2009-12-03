﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ParseStrace
{
	abstract class IOItemStorage
	{
		public abstract void PhaseOne(IOItem item);
		public abstract void PhaseBetween();
		public abstract void PhaseTwo(IOItem item);

		protected readonly TextWriter writer;

		public IOItemStorage(TextWriter writer)
		{
			this.writer = writer;
		}
	}


	sealed class IOItemDirectlyToWriter : IOItemStorage
	{
		public IOItemDirectlyToWriter(TextWriter writer)
			: base(writer) { }

		public override void PhaseOne(IOItem item) { }
		public override void PhaseBetween() { }

		public override void PhaseTwo(IOItem item)
		{
			Output(writer, item);
		}

		public static void Output(TextWriter writer, IOItem item)
		{
			string typestring;

			if (item.FDType== FDType.File)
				typestring = "";
			else
				typestring = "<" + item.TypeString + ">";

			writer.WriteLine("{6} {5,-6} at {4,-9} of Pid={1} FD={2}{3}: {0}",
				item.Filename, item.Pid, item.FDNum, typestring,
				item.Position, item.Length,
				item.IsWrite ?"Write":"Read ");
		}
	}

	
	sealed class IOItemStorageVerbose : IOItemStorage
	{
		private const int kPageSize = 4096;
		private readonly IDictionary<string, int> filePages = new Dictionary<string, int>();
		private IDictionary<string, int> fileStarts = null;

		public IOItemStorageVerbose(TextWriter writer)
			: base(writer) { }

		public void CalcPagePosition(IOItem item, out long pos, out long len)
		{
			pos = item.Position / kPageSize;
			len = (item.Position + item.Length + (kPageSize - 1)) / kPageSize - pos;

			if (len == 0)
				len = 1;
		}

		public override void PhaseOne(IOItem item)
		{
			long pos, len;
			CalcPagePosition(item, out pos, out len);

			int maxpage = 0, npages = (int)(pos + len);
			filePages.TryGetValue(item.Filename, out maxpage);

			if (npages > maxpage)
				filePages[item.Filename] = npages;
		}

		public override void PhaseBetween()
		{
			fileStarts = new Dictionary<string, int>();
			int start = 0;

			foreach (var item in filePages)
			{
				fileStarts[item.Key] = start;
				start += item.Value;
			}
		}

		public override void PhaseTwo(IOItem item)
		{
			if (item.FDType == FDType.File)
			{
				long pos, len;
				CalcPagePosition(item, out pos, out len);

				writer.Write("{0}\t{1}\t{2}\t# ",
					pos + fileStarts[item.Filename],
					len, item.IsWrite ? 1 : 0);
			}
			else
			{
				writer.Write("\t\t\t# ");
			}

			IOItemDirectlyToWriter.Output(writer, item);
		}
	}
	
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Buffers.Queues;
using Buffers.Memory;
using System.Diagnostics;

//Blower. Determine victim by two separate queues. each queue stores read and write operation separately. 
namespace Buffers.Managers
{
	class BlowerByLyf : BufferManagerBase
	{
		//the first version eliminate single queue.
		//LRUQueue single;        //Store the pages that was only referenced once. limited by a threshold. From 2Q
		protected Pool pool;

		//
		//An auxiliary data structure, only page id is used. store read and write operation separately.
		List<uint> readQueue = new List<uint>();
		List<uint> writeQueue = new List<uint>();

		uint windowSize;

		int quota = 0;			//为非负就是在read窗口里，否则就是在write窗口里。
	
		//Real data are all stored in map.
		public Dictionary<uint, Frame> map = new Dictionary<uint, Frame>();

		public BlowerByLyf(uint npages)
			: this(null, npages)
		{			
		}

		public BlowerByLyf(IBlockDevice dev, uint npages)
			: base(dev)
		{
			pool = new Pool(npages, this.dev.PageSize, OnPoolFull);
			windowSize = npages / 2;
		}

		public override string Name { get { return "Blow by lyf"; } }


		int LastIndexOfResident(IList<uint> list)
		{
			for (int i = list.Count - 1; i >= 0; i--)
			{
				if (map[list[i]].Resident)
					return i;
			}
			return -1;
		}


		bool IsInReadWindow(uint frameId)
		{
			int endResidentIndex = LastIndexOfResident(readQueue) + 1;
			int begin;
			
			if (endResidentIndex == 0)
				return false;

			if (quota < 0)
				begin = endResidentIndex;
			else
				begin = Math.Max(0, endResidentIndex - quota);

			int index = readQueue.IndexOf(frameId, begin,
				Math.Min((int)windowSize, readQueue.Count - begin));

			return index >= 0;
		}

		bool IsInWriteWindow(uint frameId)
		{
			int endResidentIndex = LastIndexOfResident(writeQueue) + 1;
			int begin;

			if (endResidentIndex == 0)
				return false;

			if (-quota < 0)
				begin = endResidentIndex;
			else
				begin = Math.Max(0, endResidentIndex - (-quota));

			int index = writeQueue.IndexOf(frameId, begin,
				Math.Min((int)windowSize, writeQueue.Count - begin));

			return index >= 0;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="pageid"></param>
		/// <param name="result"></param>
		protected sealed override void DoRead(uint pageid, byte[] result)
		{
			Frame Frame;

			//if not in hash map
			if (!map.TryGetValue(pageid, out Frame))
			{
				//add to hash map
				Frame = new Frame(pageid, pool.AllocSlot());
				map[pageid] = Frame;

				//load the page
				dev.Read(pageid, pool[Frame.DataSlotId]);
				pool[Frame.DataSlotId].CopyTo(result, 0);

				//add to queue;
				readQueue.Insert(0, pageid);

				//(to be added) if the queue exceed a certain threshold, one frame should be kicked off.
			}
			else//in hash map
			{
				Frame = map[pageid];

				if (!Frame.Resident)     //miss non resident
				{
					Frame.DataSlotId = pool.AllocSlot();
					dev.Read(pageid, pool[Frame.DataSlotId]);
					pool[Frame.DataSlotId].CopyTo(result, 0);
				}

				//update 
				if (IsInReadWindow(pageid))
				{
					//quota += -1;
				}

				readQueue.Remove(pageid);
				readQueue.Insert(0, pageid);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="pageid"></param>
		/// <param name="data"></param>
		protected sealed override void DoWrite(uint pageid, byte[] data)
		{
			Frame Frame;

			//if not in hash map
			if (!map.TryGetValue(pageid, out Frame))
			{
				//add to hash map
				Frame = new Frame(pageid, pool.AllocSlot());
				map[pageid] = Frame;

				//add to wirte queue
				writeQueue.Insert(0,pageid);

				//(to be added) if the queue exceed a certain threshold, one frame should be kicked off.
			}
			else//in hash map
			{
				Frame = map[pageid];

				if (!Frame.Resident)     //miss non resident allocate a slot
				{
					Frame.DataSlotId = pool.AllocSlot();
				}

				//update
				if (IsInWriteWindow(pageid))
				{
					//quota += 3;//TODO
				}

				writeQueue.Remove(pageid);
				writeQueue.Insert(0, pageid);
			}

			Frame.Dirty = true;
			data.CopyTo(pool[Frame.DataSlotId], 0);
		}


		static bool IsInQueue(LinkedList<uint> queue, uint targetFrame, uint lastFrame)
		{
			foreach (uint item in queue)
			{
				if(item==targetFrame)
					return true;
				if (item == lastFrame)
					return false;
			}
			return false;//ifnull
		}

		void OnPoolFull()
		{
			int checkReadIndex = LastIndexOfResident(readQueue);
			int checkWriteIndex = LastIndexOfResident(writeQueue);
			uint victim = uint.MaxValue;

			while (true)
			{
				if (quota >= 0)
				{
					quota--;

					if (checkReadIndex == -1)
						continue;

					uint pageid = readQueue[checkReadIndex];
					checkReadIndex--;

					if (!map[pageid].Resident)
						continue;

					if (writeQueue.IndexOf(pageid, 0, checkWriteIndex + 1) >= 0)
						continue;

					victim = pageid;
					break;
				}
				else
				{
					quota+=3;

					if (checkWriteIndex == -1)
						continue;

					uint pageid = writeQueue[checkWriteIndex];
					checkWriteIndex--;

					if (!map[pageid].Resident)
						continue;

					if (readQueue.IndexOf(pageid, 0, checkReadIndex + 1) >= 0)
						continue;

					victim = pageid;
					break;
				}
			}


			//释放页面
			WriteIfDirty(map[victim]);
			pool.FreeSlot(map[victim].DataSlotId);
			map[victim].DataSlotId = -1;


		}


		protected override void DoFlush()
		{
			foreach (var entry in map)
			{
				if (entry.Value.Resident)
				{
					WriteIfDirty(entry.Value);
				}
			}
		}

		protected void WriteIfDirty(IFrame frame)
		{
			if (frame.Dirty)
			{
				dev.Write(frame.Id, pool[frame.DataSlotId]);
				frame.Dirty = false;
			}
		}
	}
}
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Buffers.Memory;
using Buffers.Queues;
using Buffers.Lists;

namespace Buffers.Managers
{
	public sealed class FLIRSbyLyf : BufferManagerBase
	{
		//private readonly float ratio;
		//private readonly MultiList<RWQuery> rwlist;
        private readonly FLIRSQueue readList;
        private readonly FLIRSQueue writeList;
        private readonly HIRQueue hirQueue;
		private readonly IDictionary<uint, RWFrame> map = new Dictionary<uint, RWFrame>();
        private readonly uint maxHIRQueueLength;
        private readonly uint cacheSize;

		public FLIRSbyLyf(uint npages)
			: base(null, npages)
		{
			//this.ratio = ratio;
            cacheSize = npages;
            readList = new FLIRSQueue(false,map);
            writeList = new FLIRSQueue(true,map);
            hirQueue = new HIRQueue();
            maxHIRQueueLength = Math.Max(npages / 10, 1);
		}


		protected override void OnPoolFull()
		{
            RWFrame frame = map[hirQueue.getLast()];
            hirQueue.Dequeue(frame);

            WriteIfDirty(frame);
            pool.FreeSlot(frame.DataSlotId);
            frame.DataSlotId = -1;
		}

		protected sealed override void DoRead(uint pageid, byte[] result)
		{
			RWFrame frame = null;
            //////////////////////////////////by cat
			if (!map.TryGetValue(pageid, out frame))
			{
				frame = new RWFrame(pageid);
				map[pageid] = frame;
                //initialize
                if (map.Count <= cacheSize - maxHIRQueueLength)
                {
                    frame.ReadLowIR = true;
                    frame.WriteLowIR = true;
                }
			}

			//bool isLowIRAfter = (frame.NodeOfRead != null);
            bool isResidentOld = frame.Resident;

			if (!frame.Resident)
			{
				frame.DataSlotId = pool.AllocSlot();
				dev.Read(pageid, pool[frame.DataSlotId]);
				pool[frame.DataSlotId].CopyTo(result, 0);
			}
            //////////////////////////////////////////////////////
            bool oldStatus = frame.ShouldInHirQueue();

            readList.updateFrame(frame, false);
            writeList.updateFrame(frame, false);
            hirQueue.updateFrame(frame);

            bool newStatus = frame.ShouldInHirQueue();

            if (oldStatus && !newStatus)
            {
                if(isResidentOld)
                    hirQueue.Dequeue(frame);
                Tidy();
            }
		}

		private void Tidy()
		{
            RWFrame frame;
			while (true)
			{
                if (writeList.Count() * Config.ReadCost > readList.Count() * Config.WriteCost)
                {
                    frame = writeList.Prune();
                }
                else
                {
                    frame = readList.Prune();
                }
                if (frame==null)
                {
                    continue;
                }

                if (frame.ShouldInHirQueue() && frame.Resident)
                {
                    break;
                }
			}

            hirQueue.Enqueue(frame);

		}

		protected sealed override void DoWrite(uint pageid, byte[] data)
		{
            RWFrame frame=null;

            if (!map.TryGetValue(pageid, out frame))
            {
                frame = new RWFrame(pageid);
                map[pageid] = frame;

                if (map.Count <= cacheSize - maxHIRQueueLength)
                {
                    frame.ReadLowIR = true;
                    frame.WriteLowIR = true;
                }
            }
            if (!frame.Resident)
            {
                frame.DataSlotId = pool.AllocSlot();
            }

            data.CopyTo(pool[frame.DataSlotId], 0);
            frame.Dirty = true;

            //////////////////////////////////////////////////////
            bool oldStatus = frame.ShouldInHirQueue();

            readList.updateFrame(frame, true);
            writeList.updateFrame(frame, true);
            hirQueue.updateFrame(frame);

            bool newStatus = frame.ShouldInHirQueue();

            if (oldStatus && !newStatus)
            {
                hirQueue.Dequeue(frame);
                Tidy();
            }
		}

		protected override void DoFlush()
		{
            foreach (var entry in map)
            {
                WriteIfDirty(entry.Value);
                entry.Value.Dirty = false;
            }
		}



		public class RWFrame : Frame
		{
			public RWFrame(uint id) : base(id) { Init(); }
			public RWFrame(uint id, int slotid) : base(id, slotid) { Init(); }

			private void Init()
			{
				ReadLowIR = false;
				WriteLowIR = false;
				NodeOfReadInWriteQueue = null;
				NodeOfWriteInReadQueue = null;
				NodeOfReadInReadQueue = null;
				NodeOfWriteInWriteQueue = null;
			}

            public override string ToString()
            {
                return string.Format("Frame{{Id={0},Dirty={1},SlotId={2},ReadLowIR={3},WriteLowIR={4}}}",
                    Id, Dirty, DataSlotId,ReadLowIR, WriteLowIR);
            }
            /*public override string ToString()
            {
                return string.Format("Frame{{Id={0},Dirty={1},SlotId={2}}}",
                    id, dirty, slotid);
            }*/

            public bool ShouldInHirQueue()
            {
                return (!Dirty && !ReadLowIR) || (Dirty && !ReadLowIR && !WriteLowIR);
            }


			public bool ReadLowIR { get; set; }
			public bool WriteLowIR { get; set; }

            public LinkedListNode<RWQuery> NodeOfReadInReadQueue { get; set; }
            public LinkedListNode<RWQuery> NodeOfWriteInWriteQueue { get; set; }
            public LinkedListNode<RWQuery> NodeOfReadInWriteQueue { get; set; }
            public LinkedListNode<RWQuery> NodeOfWriteInReadQueue { get; set; }
            public LinkedListNode<uint> NodeOfHIRQueue { get; set; }

		}

        private class HIRQueue
        {
            LinkedList<uint> queue = new LinkedList<uint>();

            public LinkedListNode<uint> updateFrame(RWFrame frame)
            {
                if (frame.NodeOfHIRQueue == null)
                {
                    if (frame.ShouldInHirQueue())
                    {
                        return Enqueue(frame);
                    }
                    return null;
                }
                else
                {
                    return AccessFrame(frame);
                }
            }

            public LinkedListNode<uint> AccessFrame(RWFrame frame)
            {
                LinkedListNode<uint> node = frame.NodeOfHIRQueue;
                uint pageid = node.Value;
                queue.Remove(node);
                frame.NodeOfHIRQueue = queue.AddFirst(pageid);
                return frame.NodeOfHIRQueue;
            }

            public void Dequeue(RWFrame frame)
            {
                queue.Remove(frame.NodeOfHIRQueue);
                frame.NodeOfHIRQueue = null;
            }

            public uint getLast()
            {
                return queue.Last.Value;
            }

            public LinkedListNode<uint> Enqueue(RWFrame frame)
            {
                frame.NodeOfHIRQueue = queue.AddFirst(frame.Id);
                return frame.NodeOfHIRQueue;
            }
        }

        private class FLIRSQueue
        {
            LinkedList<RWQuery> queue = new LinkedList<RWQuery>();      //queue for RWQuery
            bool isWriteQueue;       //this is a write queue?
            IDictionary<uint, RWFrame> map;

            public FLIRSQueue(bool iisWriteQueue, IDictionary<uint, RWFrame> imap)
            {
                isWriteQueue = iisWriteQueue;
                map = imap;
            }

            public LinkedListNode<RWQuery> updateFrame(RWFrame frame, bool isWrite)
            {
                if (getNode(frame, isWriteQueue, isWrite) == null)
                {
                    return Enqueue(frame, isWrite);
                }
                else
                {
                    return AccessFrame(frame, isWrite);
                }
            }

            public LinkedListNode<RWQuery> AccessFrame(RWFrame frame, bool isWrite)
		    {

                LinkedListNode<RWQuery> node = getNode(frame, isWriteQueue, isWrite);

                if (isWriteQueue)
                {
                    frame.WriteLowIR = true;
                }
                else
                {
                    frame.ReadLowIR = true;
                }
			    queue.Remove(node);
			    LinkedListNode<RWQuery> newNode = queue.AddFirst(new RWQuery(frame.Id, isWrite));
                updateFrameNode(frame, isWriteQueue, node.Value.Type== AccessType.Write,newNode);
                
                pruneHIR();
                return newNode;
		    }


            public LinkedListNode<RWQuery> Enqueue(RWFrame frame, bool isWrite)
            {
                LinkedListNode<RWQuery> newNode = queue.AddFirst(new RWQuery(frame.Id, isWrite));
                updateFrameNode(frame, isWriteQueue, isWrite, newNode);
                return newNode;
            }

            public int Count()
            {
                return queue.Count;
            }

            //return the lastNode(is LIR) and prune HIR node to insure a new LIR node is the currentNode.
            public RWFrame Prune()
            {
                //update the info of lastNode.
                LinkedListNode<RWQuery> queryNodeReturn = queue.Last;
                RWQuery query = queryNodeReturn.Value;
                RWFrame frameReturn = map[query.PageId];
                if (queryNodeReturn != null)
                {
                    if (isWriteQueue)
                    {
                        Debug.Assert(frameReturn.WriteLowIR);
                        frameReturn.WriteLowIR = false;
                    }
                    else
                    {
                        Debug.Assert(frameReturn.ReadLowIR);
                        frameReturn.ReadLowIR = false;
                    }
                }


                pruneHIR();

                return frameReturn;
            }

            private void pruneHIR()
            {
                //find the last LIR frame and prune HIR
                LinkedListNode<RWQuery> queryNode = queue.Last;
                while (queryNode != null)
                {
                    RWQuery query = queryNode.Value;
                    RWFrame frame = map[query.PageId];

                    if (isWriteQueue)
                    {
                        if (query.Type== AccessType.Write && frame.WriteLowIR)
                            break;
                    }
                    else
                    {
						if (query.Type == AccessType.Read && frame.ReadLowIR)
                            break;
                    }

                    LinkedListNode<RWQuery> delNode = queryNode;
                    updateFrameNode(frame, isWriteQueue, delNode.Value.Type== AccessType.Write, null);
                    queryNode = queryNode.Previous;
                    queue.Remove(delNode);
                }
            }

            private LinkedListNode<RWQuery> getNode(RWFrame frame, bool isWriteQueue, bool isWriteOperation)
            {
                if (isWriteQueue)
                {
                    if (isWriteOperation)
                    {
                        return frame.NodeOfWriteInWriteQueue;
                    }
                    else
                    {
                        return frame.NodeOfWriteInReadQueue;
                    }
                }
                else
                {
                    if (isWriteOperation)
                    {
                        return frame.NodeOfReadInWriteQueue;
                    }
                    else
                    {
                        return frame.NodeOfReadInReadQueue;
                    }
                }
            }

            private void updateFrameNode(RWFrame frame, bool isWriteQueue, bool isWriteOperation, LinkedListNode<RWQuery> node)
            {
                if (isWriteQueue)
                {
                    if (isWriteOperation)
                    {
                        frame.NodeOfWriteInWriteQueue = node;
                    }
                    else
                    {
                        frame.NodeOfWriteInReadQueue = node;
                    }
                }
                else
                {
                    if (isWriteOperation)
                    {
                        frame.NodeOfReadInWriteQueue = node;
                    }
                    else
                    {
                        frame.NodeOfReadInReadQueue = node;
                    }
                }
            }
        }
	}
}
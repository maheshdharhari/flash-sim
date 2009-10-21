﻿using System.Collections.Generic;
using System.Diagnostics;
using Buffers.Memory;

namespace Buffers.Queues
{
	public abstract class QueueGroup : QueueBase
	{
		/// <summary>
		/// QueueIndex -> IQueue
		/// </summary>
		protected IQueue[] queues = null;
		/// <summary>
		/// QueueIndex -> RouteIndex
		/// </summary>
		protected uint[] offsets = null;
		/// <summary>
		/// RouteIndex -> (QueueIndex, InnerRootIndex)
		/// </summary>
		protected Route[] routes = null;


		protected void BuildRoutes()
		{
			List<uint> offsets = new List<uint>();
			List<Route> routes = new List<Route>();

			for (int i = 0; i < queues.Length; i++)
			{
				offsets.Add((uint)routes.Count);
				for (int j = 0; j < queues[i].BasicQueueCount; j++)
					routes.Add(new Route((uint)i, (uint)j));
			}

			this.offsets = offsets.ToArray();
			this.routes = routes.ToArray();
		}

		protected RoutingNode NATInwards(QueueNode outerNode)
		{
			Route route = routes[(int)outerNode.Index];
			return new RoutingNode(route.QueueIndex, route.InnerRouteIndex, outerNode.ListNode);
		}
		protected QueueNode NATOutwards(uint queueIndex, QueueNode innerNode)
		{
			uint routeIndex = innerNode.Index + offsets[(int)queueIndex];
			return new QueueNode(routeIndex, innerNode.ListNode);
		}


		public override IEnumerator<IFrame> GetEnumerator()
		{
			foreach (var queue in queues)
				foreach (var frame in queue)
					yield return frame;
		}

		public override uint Size
		{
			get
			{
				uint sum = 0;
				foreach (var queue in queues)
					sum += queue.Size;
				return sum;
			}
		}

		public override uint BasicQueueCount
		{
			get
			{
				uint sum = 0;
				foreach (var queue in queues)
					sum += queue.BasicQueueCount;
				return sum;
			}
		}

	
		public override QueueNode AccessFrame(QueueNode node)
		{
			RoutingNode routing = NATInwards(node);
			QueueNode qn = queues[(int)routing.QueueIndex].AccessFrame(routing.InnerNode);
			return NATOutwards(routing.QueueIndex, qn);
		}

		public override IFrame Dequeue(QueueNode node)
		{
			RoutingNode routing = NATInwards(node);
			return queues[(int)routing.QueueIndex].Dequeue(routing.InnerNode);
		}



		protected struct Route
		{
			public readonly uint QueueIndex;
			public readonly uint InnerRouteIndex;

			public Route(uint queueIndex, uint innerRouteIndex)
			{
				QueueIndex = queueIndex;
				InnerRouteIndex = innerRouteIndex;
			}

			public override bool Equals(object obj)
			{
				if (obj == null)
					return false;
				if (this.GetType() != obj.GetType())
					return false;

				return this == (Route)obj;
			}

			public static bool operator ==(Route left, Route right)
			{
				return left.QueueIndex == right.QueueIndex &&
					left.InnerRouteIndex == right.InnerRouteIndex;
			}

			public static bool operator !=(Route left, Route right)
			{
				return !(left == right);
			}

			public override int GetHashCode()
			{
				return QueueIndex.GetHashCode() ^ InnerRouteIndex.GetHashCode();
			}
		}

		protected struct RoutingNode
		{
			public readonly uint QueueIndex;
			public readonly QueueNode InnerNode;

			public RoutingNode(uint major, uint innerindex, LinkedListNode<IFrame> innernode)
				: this(major, new QueueNode(innerindex, innernode)) { }

			public RoutingNode(uint major, QueueNode inner)
			{
				QueueIndex = major;
				InnerNode = inner;
			}

			public override bool Equals(object obj)
			{
				if (obj == null)
					return false;
				if (this.GetType() != obj.GetType())
					return false;

				return this == (RoutingNode)obj;
			}

			public static bool operator ==(RoutingNode left, RoutingNode right)
			{
				return left.QueueIndex == right.QueueIndex &&
					left.InnerNode == right.InnerNode;
			}

			public static bool operator !=(RoutingNode left, RoutingNode right)
			{
				return !(left == right);
			}

			public override int GetHashCode()
			{
				return QueueIndex.GetHashCode() ^ InnerNode.GetHashCode();
			}
		}

	}
}
﻿using System;
using System.Collections.Generic;
using Buffers.Devices;
using Buffers.Managers;
using System.Reflection;

namespace Buffers
{
	public static class Config
	{
		public static decimal ReadCost { get; private set; }
		public static decimal WriteCost { get; private set; }
		public static float Ratio { get { return (float)(WriteCost / ReadCost); } }
		public static uint RoundedRatio { get { return (uint)Ratio; } }


		private delegate IBufferManager ManagerCreator(uint npages, string[] args, bool verify);
		private static readonly IDictionary<string, MethodInfo> creators = new Dictionary<string, MethodInfo>();


		static Config()
		{
			SetConfig(1, 1);

			foreach (MethodInfo method in typeof(Config).GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
			{
				foreach (object objattr in method.GetCustomAttributes(typeof(ManagerFactoryAttribute), false))
				{
					ManagerFactoryAttribute attr = objattr as ManagerFactoryAttribute;
					creators[attr.CmdLineName.ToLower()] = method;
				}
			}
		}
		public static void SetConfig(decimal readcost, decimal writecost)
		{
			ReadCost = readcost;
			WriteCost = writecost;
		}


		public static IBlockDevice CreateDevice(bool verify)
		{
			return verify ? new MemorySimulatedDevice(1) : null;
		}
		public static IBufferManager CreateManager(string name, uint npages, string[] args, bool verify)
		{
			return (IBufferManager)creators[name.ToLower()].Invoke(
				null, new object[] { npages, args, verify });
		}

           
#pragma warning disable 0169 // "Never used"
#pragma warning disable 0618 // "Obsolete"
		[ManagerFactory("Trival")]
		static IBufferManager CreateTrival(uint npages, string[] args, bool verify)
		{
			return new TrivalManager(CreateDevice(verify));
		}
		[ManagerFactory("CFLRU")]
		static IBufferManager CreateCFLRU(uint npages, string[] args, bool verify)
		{
			return new CFLRU(CreateDevice(verify), npages, float.Parse(args[0]));
		}
		[ManagerFactory("LRUWSR")]
		static IBufferManager CreateLRUWSR(uint npages, string[] args, bool verify)
		{
			return new LRUWSR(CreateDevice(verify), npages);
		}
        [ManagerFactory("CCFLRU")]
        static IBufferManager CreateCCFLRU(uint npages, string[] args, bool verify)
        {
            return new CCFLRU(CreateDevice(verify), npages);
        } 
		[ManagerFactory("CMFT")]
		static IBufferManager CreateCMFT(uint npages, string[] args, bool verify)
		{
			return new CMFTByCat(CreateDevice(verify), npages);
		}
		[ManagerFactory("LRU")]
		static IBufferManager CreateLRU(uint npages, string[] args, bool verify)
		{
			return new LRU(CreateDevice(verify), npages);
		}
        [ManagerFactory("LIRS")]
        static IBufferManager CreateLIRS(uint npages, string[] args, bool verify)
        {
            return new LIRS_ByWu(CreateDevice(verify), npages, float.Parse(args[0]));
        }
        [ManagerFactory("FLRUByLyf")]
        static IBufferManager CreateFLRUByLyf(uint npages, string[] args, bool verify)
        {
            return new FLRU(CreateDevice(verify), npages);
        }
		[ManagerFactory("FLIRSByCat")]
		static IBufferManager CreateFLIRSByCat(uint npages, string[] args, bool verify)
		{
			return new FLIRSByCat(CreateDevice(verify), npages, Ratio, float.Parse(args[0]));
		}
		[ManagerFactory("FLIRSByLyf")]
		static IBufferManager CreateFLIRSByLyf(uint npages, string[] args, bool verify)
		{
			return new FLIRSbyLyf2(CreateDevice(verify), npages, double.Parse(args[0]));
		}
		[ManagerFactory("Tn")]
		[ManagerFactory("ACAR")]
		static IBufferManager CreateTn(uint npages, string[] args, bool verify)
		{
			if (args.Length == 0)
				return new Tn(CreateDevice(verify), npages, Ratio);
			else
				return new Tn(CreateDevice(verify), npages, Ratio, new TnConfig(
					int.Parse(args[0]) != 0,
					int.Parse(args[1]) != 0,
					int.Parse(args[2]) != 0,
					float.Parse(args[3]),
					float.Parse(args[4]),
					float.Parse(args[5]),
					float.Parse(args[6])
					));
		}
		[ManagerFactory("OldTn")]
		static IBufferManager CreateOldTn(uint npages, string[] args, bool verify)
		{
			if (args.Length == 0)
				return new OldTn(CreateDevice(verify), npages, Ratio);
			else
				return new OldTn(CreateDevice(verify), npages, Ratio, new TnConfig(
					int.Parse(args[0]) != 0,
					int.Parse(args[1]) != 0,
					int.Parse(args[2]) != 0,
					float.Parse(args[3]),
					float.Parse(args[4]),
					float.Parse(args[5]),
					float.Parse(args[6])
					));
		}
		[ManagerFactory("OldOldTn")]
		static IBufferManager CreateOldOldTn(uint npages, string[] args, bool verify)
		{
			if (args.Length == 0)
				return new OldOldTn(CreateDevice(verify), npages, Ratio);
			else
				return new OldOldTn(CreateDevice(verify), npages, Ratio, new OldOldTnConfig(
					int.Parse(args[0]) != 0,
					int.Parse(args[1]) != 0,
					uint.Parse(args[5]),
					uint.Parse(args[6]),
					int.Parse(args[2]) != 0
					));
		}
#pragma warning restore 0618
#pragma warning restore 0169

	}
}

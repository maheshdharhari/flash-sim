﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;


namespace ParseStrace
{
	class ProcFDTable
	{
		private static readonly Regex regexOpen = new Regex(@"^\""(.+)\"",");
		private static readonly Regex regexPipe = new Regex(@"\[(\d+),\s+(\d+)\]");
		private static readonly Regex regexSelfDir = new Regex(@"/\./");
		private static readonly Regex regexParentDir = new Regex(@"/(([^./])|(\.[^./])|(\.\.[^/]))[^/]*/../");
		private static readonly string[] argumentSplitter = { ", " };

		private int pid;
		private string halfLine = null;
		private IOItemFormatter formatter;
		private Dictionary<int, FileState> curFiles = new Dictionary<int, FileState>();


		public ProcFDTable(int pid, IOItemFormatter formatter)
		{
			this.pid = pid;
			this.formatter = formatter;
			curFiles.Add(0, new FileState("/dev/stdin", FDType.Terminal));
			curFiles.Add(1, new FileState("/dev/stdout", FDType.Terminal));
			curFiles.Add(2, new FileState("/dev/stderr", FDType.Terminal));

		}

		public ProcFDTable Fork(int newpid)
		{
			ProcFDTable other = new ProcFDTable(newpid, formatter);

			foreach (var item in curFiles)
				other.curFiles[item.Key] = item.Value;

			return other;
		}


		private static int GetFirstNumeric(string args)
		{
			return int.Parse(args.Split(argumentSplitter, 2, StringSplitOptions.None)[0]);
		}

		private string NormalizeFilename(string filename)
		{
			string oldfilename = null;
			while (oldfilename != filename)
			{
				oldfilename = filename;
				filename = regexSelfDir.Replace(oldfilename, "/");
			}

			oldfilename = null;
			while (oldfilename != filename)
			{
				oldfilename = filename;
				filename = regexParentDir.Replace(oldfilename, "/");
			}

			return filename;
		}


		public void OnAccept(string args, long ret)
		{
			int accepting = GetFirstNumeric(args);
			curFiles[(int)ret] = new FileState("/SocketTo/" + accepting, FDType.Socket);
		}

		public void OnClose(string args)
		{
			int fd = int.Parse(args);
			curFiles.Remove(fd);
		}

		public void OnDup(string args, long ret)
		{
			int oldfd = GetFirstNumeric(args);
			int newfd = (int)ret;
			curFiles[newfd] = curFiles[oldfd];
		}

		public void OnFcntl(string args, long ret)
		{
			if (args.Contains("F_DUPFD"))
			{
				int oldfd = GetFirstNumeric(args);
				int newfd = (int)ret;
				curFiles[newfd] = curFiles[oldfd];
			}
		}

		public void OnLSeek(string args, long ret)
		{
			int fd = GetFirstNumeric(args);
			Debug.Assert(curFiles.ContainsKey(fd));

			FileState fs = curFiles[fd];
			fs.Position = ret;
		}

		public void OnMMap(int offsetMultiple, string argsline, int phase)
		{
			if (argsline.Contains("MAP_ANON"))
				return;

			string[] args = argsline.Split(argumentSplitter, StringSplitOptions.None);
			int fd = int.Parse(args[4]);
			long length = long.Parse(args[1]);
			long offset = Utils.ParseHexLong(args[5]);

			FileState fs;
			if (!curFiles.TryGetValue(fd, out fs))
			{
				fs = new FileState("/Unknown-FD/" + fd.ToString(), FDType.Unknown);
				curFiles[fd] = fs;
			}

			IOItem item = new IOItem(pid, fs.Filename, (short)fd,
				false, offset, length, fs.FDType);

			if (phase == 1)
				formatter.PhaseOne(item);
			else
				formatter.PhaseTwo(item);
		}

		public void OnOpen(string args, long ret)
		{
			int fd = (int)ret;
			string filename = regexOpen.Match(args).Groups[1].Value;
			curFiles[fd] = new FileState(NormalizeFilename(filename));
		}

		public void OnPipe(string args)
		{
			Match m = regexPipe.Match(args);
			int reading = int.Parse(m.Groups[1].Value);
			int writing = int.Parse(m.Groups[2].Value);

			curFiles[reading] = new FileState("/Pipe/" + reading, FDType.Pipe);
			curFiles[writing] = new FileState("/Pipe/" + writing, FDType.Pipe);
		}

		public void OnUnfinished(string halfline)
		{
			halfLine = halfline;
		}

		public void OnReadWrite(bool isWrite, string args, long ret, int phase)
		{
			int fd = GetFirstNumeric(args);

			FileState fs;
			if (!curFiles.TryGetValue(fd, out fs))
			{
				fs = new FileState("/Unknown-FD/" + fd.ToString(), FDType.Unknown);
				curFiles[fd] = fs;
			}

			IOItem item = new IOItem(pid, fs.Filename, (short)fd,
				isWrite, fs.Position, ret, fs.FDType);

			fs.Position += ret;

			if (phase == 1)
				formatter.PhaseOne(item);
			else
				formatter.PhaseTwo(item);
		}

		public string OnResumed()
		{
			string str = halfLine;
			halfLine = null;
			return str;
		}


		private class FileState
		{
			public readonly string Filename;
			public readonly FDType FDType;
			public long Position = 0;

			public FileState(string filename)
				: this(filename, FDType.File) { }

			public FileState(string filename, FDType type)
			{
				this.Filename = filename;
				this.FDType = type;
			}

			public override string ToString()
			{
				return string.Format("[{0} of Type={1} at Pos={2}]",
					Filename, FDType, Position);
			}
		}


	}
}

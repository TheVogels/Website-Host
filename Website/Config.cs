using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Website
{
	public static class Config
	{
		public static ushort Port = 52792;
		public const uint MaxWaitTime = 80*1000;

		public static string SourcePath = "C:\\Users\\Wyatt\\source\\repos\\Website\\Website\\WebsiteData";
		public static Encoding encoding = Encoding.UTF8;

		public static ConsoleColor 
			DefaultC = ConsoleColor.Gray,
			ReceiveC = ConsoleColor.Yellow,
			SendC = ConsoleColor.Magenta,

			ErrorC = ConsoleColor.Red,
			WarnC = ConsoleColor.DarkYellow,
			MiscDefaultC = ConsoleColor.DarkCyan;

		public static char Sep = Path.DirectorySeparatorChar;

		public static bool 
			ForceInstantCloseConnection = true,

			EnableCompression = true,
			CompressVeryLargeFiles = false,

			EnableExternalFileRedirects = false;

		public static IEnumerable<string>? BlockedUserAgents = ["modat.io", "censys.io", "infrawat.ch"];

		public static System.IO.Compression.CompressionLevel Compression = (System.IO.Compression.CompressionLevel)255;

		public static int BufferLimit = 620_000_000;

		public static int File_MaxFileSizeForCompression = 715_827_863,
			VideoFile_MaxFileSizeForCompression = 95_827_863;

		#if LargeFileSupport
		// Performance and RAM usage issues were happening before this was implemented.
		// Turned out loading a file about the size of 2GB into RAM directly from the disk is slow.
		public static int MaxBufferedFileSize = 586_346_556;
		#endif

		public static void ParseConfig(IEnumerable<string> Config, bool ShowSummary = true, bool DynamicReloadWarnings = false)
		{
			if(DynamicReloadWarnings)
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("If an argument is removed after the previous config change, it will not be reset to the default value until a server restart.\n");
				Console.ForegroundColor = MiscDefaultC;
			}

			List<string> CustomBlockedUserAgents = new List<string>(10);

			foreach(var i in Config)
			{
				if(string.IsNullOrWhiteSpace(i) ||  i.StartsWith("//"))
					continue;

				var opI = i.IndexOf(' ');
				if(opI == -1)
				{
					Console.ForegroundColor = ErrorC;
					Console.WriteLine("Setting has no arguments: {0}", i);
					Console.ForegroundColor = MiscDefaultC;
					continue;
				}

				var atr = i.Substring(opI+1);
				var op = i[..opI].ToLowerInvariant();

				switch(op)
				{
					case "sourcepath:":
					SourcePath = atr;
					break;
					case "port:":
					if(DynamicReloadWarnings && Port != ushort.Parse(atr))
					{
						Console.ForegroundColor = ErrorC;
						Console.WriteLine("Restart the server to change the port, the port cannot be changed live.");
						Console.ForegroundColor = MiscDefaultC;
					}
					Port = ushort.Parse(atr);
					break;
					case "encoding:":
					encoding = Encoding.GetEncoding(atr);
					break;
					case "forceinstantcloseconnection:":
					ForceInstantCloseConnection = bool.Parse(atr);
					break;
					case "defaultc:":
					DefaultC = (ConsoleColor)byte.Parse(atr);
					break;
					case "sendc:":
					SendC = (ConsoleColor)byte.Parse(atr);
					break;
					case "receivec:":
					ReceiveC = (ConsoleColor)byte.Parse(atr);
					break;
					case "errorc:":
					ErrorC = (ConsoleColor)byte.Parse(atr);
					break;
					case "addblockedagent:":
					CustomBlockedUserAgents.Add(atr);
					BlockedUserAgents = CustomBlockedUserAgents;
					break;
					case "enablecompression:":
					EnableCompression = bool.Parse(atr);
					break;
					case "miscdefaultc:":
					MiscDefaultC = (ConsoleColor)byte.Parse(atr);
					break;
					case "warningc:":
					WarnC = (ConsoleColor)byte.Parse(atr);
					break;
					case "compressionlevel:":
					Compression = (System.IO.Compression.CompressionLevel)byte.Parse(atr);
					break;
					case "compresslargefiles:":
					CompressVeryLargeFiles = bool.Parse(atr);
					break;
					case "bufferlimit:":
					BufferLimit = int.Parse(atr);
					break;
					case "maxfilesizeforcompression:":
					File_MaxFileSizeForCompression = int.Parse(atr);
					break;
					case "maxvideofilesizeforcompression:":
					VideoFile_MaxFileSizeForCompression = int.Parse(atr);
					break;
					case "enableexternalfileredirects:":
					if(EnableExternalFileRedirects = bool.Parse(atr))
					{
						Console.ForegroundColor = WarnC;
						Console.WriteLine("The redirection files may be exploited to gain access to any file on your system.\nPlease make sure that what has access to the WebsiteData folder is trustworthy.");
						Console.ForegroundColor = DefaultC;
					}
					break;
					case "maxbufferedfilesize:":
					MaxBufferedFileSize = int.Parse(atr);
					break;
					default:
					Console.ForegroundColor = ErrorC;
					Console.WriteLine("Unrecognized setting: {0}", op);
					Console.ForegroundColor = DefaultC;
					break;
				}
			}

			CustomBlockedUserAgents.TrimExcess();

			if(ShowSummary)
			{
				Console.ForegroundColor = MiscDefaultC;

				Console.WriteLine
				(
					"-- Config Summary --\n\nSourcePath: {0}\nPort: {1}\nEncoding: {2}\nForceInstantCloseConnection: {3}\nEnableCompression: {4}\nCompression Level: {5} ({6})\nCompressLargeFiles: {7}\nBufferLimit: {8}\nMaxFileSizeForCompression: {9}\nMaxVideoFileSizeForCompression: {10}\nEnableExternalFileRedirects: {11}\nMaxBufferedFileSize: {12}\n", 
					SourcePath, 
					Port, 
					encoding.EncodingName, 
					ForceInstantCloseConnection, 
					EnableCompression, 
					Compression == (System.IO.Compression.CompressionLevel)255 ? "C# Default" : Compression, (int)Compression,
					CompressVeryLargeFiles,
					BufferLimit,
					File_MaxFileSizeForCompression,
					VideoFile_MaxFileSizeForCompression,
					EnableExternalFileRedirects,
					MaxBufferedFileSize
				);
				
				Console.ForegroundColor = DefaultC;
				Console.WriteLine("█▌ Default Color (DefaultC: {0})", (int)DefaultC);
				Console.ForegroundColor = ReceiveC;
				Console.WriteLine("█▌ Data Receive Color (ReceiveC: {0})", (int)ReceiveC);
				Console.ForegroundColor = SendC;
				Console.WriteLine("█▌ Data Send Color (SendC: {0})", (int)SendC);
				Console.ForegroundColor = ErrorC;
				Console.WriteLine("█▌ Error Color (ErrorC: {0})", (int)ErrorC);
				Console.ForegroundColor = MiscDefaultC;
				Console.WriteLine("█▌ Miscellaneous Color (MiscDefaultC: {0})", (int)MiscDefaultC);
				Console.ForegroundColor = WarnC;
				Console.WriteLine("█▌ Warning Color (WarnC: {0})", (int)WarnC);

				Console.ForegroundColor = MiscDefaultC;

				Console.WriteLine("\n- Blocked User Agents (AddBlockedAgent: ...) -\n{0}\n", BlockedUserAgents == null ? "DEFAULT" : string.Join(",\n", BlockedUserAgents));

				Console.WriteLine("-- Config Summary End --\n");

				Console.ForegroundColor = DefaultC;
			}
		}
	}
}
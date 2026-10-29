using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web;
using System.IO.Compression;
using static Website.WebUtil;
using static Website.Util;
using static Website.Config;
using StringWriter = Website.WebUtil.StringWriter;
using System.Runtime.CompilerServices;
#pragma warning disable CS8600, CS8622, CS8602, IDE0028

namespace Website
{
	#if LargeFileSupport
	using fLong = long;
	#else
	using fLong = int;
	#endif

	public static class Program
	{
		public static void Main()
		{
			if(File.Exists($"{Environment.CurrentDirectory}{Sep}Config.cfg"))
			{
				ParseConfig(File.ReadLines($"{Environment.CurrentDirectory}{Sep}Config.cfg"), true);
			}
			else
			{
				Console.ForegroundColor = WarnC;
				Console.WriteLine("We could not find a config file at \"{0}{1}Config.cfg\".\nPlease add one for automatic configuration.", Environment.CurrentDirectory, Sep);
				Console.ForegroundColor = DefaultC;
			}

			Console.WriteLine("Port: {0}\nHost path: {1}\n", Port, SourcePath);

			if(!Directory.Exists(SourcePath))
			{
				Console.ForegroundColor = ErrorC;
				Console.Write("The directory \"{0}\" does not exist.\nPlease provide the one to use: ", SourcePath);
				Console.ForegroundColor = DefaultC;
				SourcePath = Console.ReadLine();
				if(!Directory.Exists(SourcePath))
				{
					Console.ForegroundColor = ErrorC;
					Console.WriteLine("The directory still does not exist. Exiting.");
					Console.ForegroundColor = DefaultC;
					return;
				}
			}
			
			Socket data = new Socket(SocketType.Stream, ProtocolType.Tcp);

			new Thread(DynamicLoadingListener)
			{
				IsBackground = true,
				Name = "Keyboard Thread",
				Priority = ThreadPriority.BelowNormal,
			}.Start();

			data.Bind(new IPEndPoint(IPAddress.Any, Port));

			if(CompressVeryLargeFiles)
			{
				System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
				System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.Interactive;
			}

			while(true)
			{
				Console.WriteLine("Listening for new incoming connections.");
				data.Listen();

				new Thread((object d) => HandleUser((Socket)d))
				{
					Name = "Web responder Thread",
				}.Start(data.Accept());
			}
		}

		public static void DynamicLoadingListener()
		{
			while(true)
			{
				switch(Console.ReadKey(true).Key)
				{
					case ConsoleKey.R:
					Console.ForegroundColor = ReceiveC;
					Console.WriteLine("\nReload config file.");
					Console.ForegroundColor = DefaultC;
					if(File.Exists($"{Environment.CurrentDirectory}{Sep}Config.cfg"))
					{
						ParseConfig(File.ReadLines($"{Environment.CurrentDirectory}{Sep}Config.cfg"), DynamicReloadWarnings: true);
					}
					else
					{
						Console.ForegroundColor = ErrorC;
						Console.WriteLine("No config file exists in \"{0}{1}Config.cfg\", dummy.", Environment.CurrentDirectory, Sep);
						Console.ForegroundColor = DefaultC;
					}
					break;
					case ConsoleKey.G:
					Console.ForegroundColor = ReceiveC;
					Console.WriteLine("Collect garbage.");
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
					Console.WriteLine("Collected garbage.");
					Console.ForegroundColor = DefaultC;
					break;
				}
			}
		}

		public struct HeaderData()
		{
			public bool Parse(ReadOnlySpan<byte> data) => Parse(encoding.GetString(data));

			[MethodImpl(MethodImplOptions.AggressiveOptimization)]
			public bool Parse(string data)
			{
				RequestData??= new();
				ArgumentsP??= new();

				var index = data.IndexOf('\n');

				if(index < 0)
					index = 0;

				string[] Split = data[..index].Split(' ');

				if(Split.Length < 2)
				{
					Console.WriteLine("Unknown data: \"{0}\" (Length: {1})", data, data.Length);

					Path = string.Empty;
					Method = string.Empty;
					Version = string.Empty;
					Arguments = string.Empty;
					RequestData.Clear();
					ArgumentsP.Clear();
					return false;
				}

				Method = Split[0];
				Path = UrlDecode(Split[1]);
				var ArgI = Path.LastIndexOf('?');
				if(ArgI > 0)
				{
					Arguments = Path.Substring(ArgI+1);
					Path = Path[..ArgI];
				}
				else
					Arguments = string.Empty;
				Version = Split[2];

				foreach(var i in data.Substring(index+1).Split('\r'))
				{
					if(string.IsNullOrWhiteSpace(i))
						continue;

					ArgI = i.IndexOf(':');
					if(ArgI == -1)
						continue;

					RequestData[i[1..ArgI].ToLowerInvariant()] = i.Substring(ArgI+2);
				}

				foreach(var i in Arguments.Split('&'))
				{
					Console.WriteLine(i);

					ArgI = i.IndexOf('=');
					if(ArgI == -1)
						continue;

					ArgumentsP[i[..ArgI].ToLowerInvariant()] = i.Substring(ArgI+1);
				}
				return true;
			}

			public string Path = string.Empty;
			public string Arguments = string.Empty;
			public Dictionary<string, string> ArgumentsP = new();
			public string Method = string.Empty;
			public string Version = string.Empty;
			public Dictionary<string, string> RequestData = new();

			public readonly string GetConnectionMode() => RequestData.TryGetValue("Connection", out string Connection) ? Connection : "keep-alive";
		}

		struct WebResulterResult
		{
			[Flags]
			public enum WebResulterResultFlags : byte
			{
				DisposeOnDone = 1 << 0,
			}

			public byte[]? ContentB;
			public Stream? ContentS;
			public WebResulterResultFlags flags;

			public readonly int Length
			{
				get
				{
					if(ContentB != null)
						return ContentB.Length;
					else if(ContentS != null)
						return (int)ContentS.Length;
					else
						throw new NullReferenceException();
				}
			}
			#if LargeFileSupport
			public readonly fLong LongLength
			{
				get
				{
					if(ContentB != null)
						return ContentB.LongLength;
					else if(ContentS != null)
						return ContentS.Length;
					else
						throw new NullReferenceException();
				}
			}
			#else
			public readonly fLong LongLength
			{
				get => Length;
			}
			#endif

			public static implicit operator WebResulterResult(byte[] Data) => new WebResulterResult{ContentB = Data};
			public static implicit operator WebResulterResult(Stream Data) => new WebResulterResult{ContentS = Data};

			public static explicit operator byte[]?(WebResulterResult source) => source.ContentB;
			public static explicit operator Stream?(WebResulterResult source) => source.ContentS;

			public static bool operator ==(WebResulterResult a, WebResulterResult b) => a.ContentB == b.ContentB || a.ContentS == b.ContentS;
			public static bool operator !=(WebResulterResult a, WebResulterResult b) => a.ContentB != b.ContentB && a.ContentS != b.ContentS;

			public readonly override bool Equals(object? obj)
			{
				if(obj is WebResulterResult result)
					return this == result;
				else if(obj is Stream stream)
					return ContentS == stream;
				else if(obj is byte[] bytes)
					return ContentB == bytes;

				return false;
			}

			public readonly override int GetHashCode()
			{
				if(ContentB == null && ContentS == null)
					return base.GetHashCode();
				else if(ContentB == null && ContentS != null)
					return ContentS.GetHashCode();
				else if(ContentB != null && ContentS == null)
					return ContentB.GetHashCode();
				else
					return HashCode.Combine(ContentB.GetHashCode());
			}
		}

		interface WebResulter
		{
			public abstract WebResulterResult GetResult(HeaderData header);
			public abstract string GetHeader(HeaderData header, WebResulterResult Result = default);
		}

		struct FileWebResulter : WebResulter
		{
			public static bool Exists(string path)
			{
				return File.Exists(path) || (EnableExternalFileRedirects && File.Exists(path+".webextredir"));
			}

			public WebResulterResult GetResult(HeaderData header)
			{
				string Path = SourcePath+header.Path;
				Console.WriteLine(Path);

				if(EnableExternalFileRedirects && File.Exists(Path+".webextredir"))
				{
					var NewPath = File.ReadAllText(Path+".webextredir");
					if(File.Exists(NewPath))
					{
						Path = NewPath;
						Console.WriteLine("File redirect file was found, redirecting the path to {0}.", NewPath);
					}
					else
					{
						Console.ForegroundColor = ErrorC;
						Console.WriteLine("File redirect file was found, but {0} does not exist.", NewPath);
						Console.ForegroundColor = DefaultC;
					}
				}

				if(!File.Exists(Path))
					return new ErrorWebResulter(404).GetResult(header);

				string type = GetFileType(Path);

				bool LiveHTML = Path.EndsWith(".htmle", StringComparison.InvariantCultureIgnoreCase);
				if(LiveHTML)
				{
					string fileData = File.ReadAllText(Path, encoding);

					int i, ie;
					while(true)
					{
						i = fileData.IndexOf("<!-- /!");
						ie = fileData.IndexOf(" -->", i+7);
						if(ie == -1 || i == -1)
							break;
						var str = fileData[(i+7)..ie];
						fileData = string.Concat(fileData.AsSpan(0, i), fileData.AsSpan(ie+5));

						Console.WriteLine("str: \"{0}\"\ni: {1}, ie: {2}", str, i, ie);

						var arg = str.Substring(str.IndexOf(' ')+1);

						if(str.StartsWith("embed ", StringComparison.InvariantCultureIgnoreCase))
						{
							fileData = fileData.Insert(i, File.ReadAllText($"{SourcePath}{Sep}{arg.Replace('\\', Sep)}", encoding));
						}
						else if(str.StartsWith("gallery ", StringComparison.InvariantCultureIgnoreCase))
						{
							string Gal = null;
							var files = new DirectoryInfo($"{SourcePath}{Sep}{arg}").GetFiles();
							Array.Sort(files, (a,b) => Math.Sign(a.LastWriteTimeUtc.Ticks-b.LastWriteTimeUtc.Ticks));
							foreach(var f in files)
							{
								if((f.Attributes & (FileAttributes.System|FileAttributes.Hidden)) == 0)
									Gal+= $"<{(GetFileType(f.Name).StartsWith("video", StringComparison.InvariantCultureIgnoreCase) ? "video controls" : "img")} src=\"/{arg.Replace(Sep,'/')}/{f.Name}\"></video>";
							}
							fileData = fileData.Insert(i, Gal);
						}
						i = 0;
					}

					var file = encoding.GetBytes(fileData);

					return file;
				}
				else
				{
					FileStream file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, new FileInfo(Path).Length > BufferLimit ? FileOptions.None : FileOptions.SequentialScan);

					long length = file.Length;

					#if LargeFileSupport
					bool a;
					if((a = length > Array.MaxLength) || (MaxBufferedFileSize != -1 && length > MaxBufferedFileSize))
					{
						Console.ForegroundColor = MiscDefaultC;
						Console.WriteLine(a ? "{0} elements is too large for the runtime to allocate, using a stream." : "File size {0} is larger than {1} bytes, using a stream.", length, MaxBufferedFileSize);
						Console.ForegroundColor = DefaultC;

						return new WebResulterResult{ContentS = file, flags = WebResulterResult.WebResulterResultFlags.DisposeOnDone};
					}
					else
					{
					#endif
						var data = new byte[length];

						file.ReadExactly(data);
						file.Dispose();

						return data;
					#if LargeFileSupport
					}
					#endif
				}
			}

			public string GetHeader(HeaderData header, WebResulterResult Result = default)
			{
				string Path = SourcePath+header.Path;
				Console.WriteLine(Path);

				if(EnableExternalFileRedirects && File.Exists(Path+".webextredir"))
				{
					var NewPath = File.ReadAllText(Path+".webextredir");
					if(File.Exists(NewPath))
						Path = NewPath;
				}

				if(!File.Exists(Path))
					return new ErrorWebResulter(404).GetHeader(header, Result);

				string type = GetFileType(Path);

				if(Result != default)
					return BuildHeader( HttpStatusCode.OK, type, Length: Result.LongLength.ToString(), LastModified: File.GetLastWriteTimeUtc(Path) );
				bool LiveHTML = Path.EndsWith(".htmle", StringComparison.InvariantCultureIgnoreCase);

				if(LiveHTML)
				{
					string fileData = File.ReadAllText(Path, encoding);

					int i, ie;
					while(true)
					{
						i = fileData.IndexOf("<!-- /!");
						ie = fileData.IndexOf(" -->", i+7);
						if(ie == -1 || i == -1)
							break;
						var str = fileData[(i+7)..ie];
						fileData = string.Concat(fileData.AsSpan(0, i), fileData.AsSpan(ie+5));

						Console.WriteLine("str: \"{0}\"\ni: {1}, ie: {2}", str, i, ie);

						var arg = str.Substring(str.IndexOf(' ')+1);

						if(str.StartsWith("embed ", StringComparison.InvariantCultureIgnoreCase))
						{
							fileData = fileData.Insert(i, File.ReadAllText($"{SourcePath}{Sep}{arg}", encoding));
						}
						else if(str.StartsWith("gallery ", StringComparison.InvariantCultureIgnoreCase))
						{
							string Gal = null;
							var files = new DirectoryInfo($"{SourcePath}{Sep}{arg}").GetFiles();
							Array.Sort(files, (a,b) => Math.Sign(a.LastWriteTimeUtc.Ticks-b.LastWriteTimeUtc.Ticks));
							foreach(var f in files)
							{
								if((f.Attributes & (FileAttributes.System|FileAttributes.Hidden)) == 0)
									Gal+= $"<{(GetFileType(f.Name).StartsWith("video", StringComparison.InvariantCultureIgnoreCase) ? "video controls" : "img")} src=\"/{arg.Replace(Sep,'/')}/{f.Name}\" />";
							}
							fileData = fileData.Insert(i, Gal);
						}
						i = 0;
					}

					var file = encoding.GetBytes(fileData);

					int length = file.Length;
					Console.WriteLine("File is {0} bytes long.", length);

					return BuildHeader( HttpStatusCode.OK, type, Length: length.ToString(), LastModified: File.GetLastWriteTimeUtc(Path) );
				}
				else
				{
					var length = new FileInfo(Path).Length;
					Console.WriteLine("File is {0} bytes long.", length);

					return BuildHeader( HttpStatusCode.OK, type, Length: length.ToString(), LastModified: File.GetLastWriteTimeUtc(Path) );
				}
			}
		}

		struct FileBrowserWebResulter : WebResulter
		{
			public readonly WebResulterResult GetResult(HeaderData header)
			{
				bool LookingForHTML = header.Path == $"{Sep}BrowseHTML";

				bool IgnoreMax = false;
				if(header.ArgumentsP.TryGetValue("ignoremax", out string? _IgnoreMax))
					IgnoreMax = IsYes(_IgnoreMax);
				
				IStringWriter HTML = new StringWriterEncoded(encoding, "<!DOCTYPE html>\r\n\r\n<html lang=\"en-US\">\r\n<head>\r\n<meta charset=\""u8);
				HTML+= encoding.WebName;
				HTML+= "\">\r\n<meta name=\"viewport\" content=\"width=550\" />\r\n<title>Browse Files</title><link rel=\"stylesheet\" href=\"filebrowse.css\">\r\n<link rel=\"icon\" type=\"image/gif\" href=\"/Media/Ico/Folder_Icon.gif\"></head>\r\n\r\n<body class=\"BrowserBackground\">"u8;

				var SubPath = string.Empty;
				if(header.ArgumentsP.TryGetValue("path", out SubPath) && !string.IsNullOrEmpty(SubPath))
				{
					SubPath = Sep+SubPath.Replace('/',Sep);

					var LastI = SubPath.LastIndexOf(Sep);
					if(LastI > 0)
						HTML+= $"<a class=\"directory\" href=\"/BrowseAll?path={HTMLURLEncode(SubPath[1..LastI])}\">..</a><br /><br />";
					else
						HTML+= "<a class=\"directory\" href=\"/BrowseAll?path=\">..</a><br /><br />"u8;
				}
				else
					HTML+= "<a class=\"directory\" href=\"/\">Home</a><br /><br />"u8;
				
				var directory = new DirectoryInfo(SourcePath+SubPath);
				if(!LookingForHTML)
				{
					foreach(var i in directory.EnumerateDirectories("*", new EnumerationOptions{AttributesToSkip = FileAttributes.Hidden}))
					{
						var I = i.FullName.Substring(SourcePath.Length+1).Replace(Sep, '/');
						HTML+= $"<a class=\"directory\" href=\"/BrowseAll?path={HTMLURLEncode(I)}\">{i.Name}</a><br />";

						Console.WriteLine("Directory: {0}", i);
					}
					HTML+= "<br />"u8;
				}
				var SkipHidden = new EnumerationOptions{AttributesToSkip = FileAttributes.Hidden};

				FileInfo[] files;
				if(LookingForHTML)
				{
					List<FileInfo> fl = new();
					foreach(var f in directory.EnumerateFiles("*.*htm*", new EnumerationOptions{AttributesToSkip = FileAttributes.Hidden, RecurseSubdirectories = true}))
					{
						if
						(
							f.Name.EndsWith(".html", StringComparison.InvariantCultureIgnoreCase) ||
							f.Name.EndsWith(".htm", StringComparison.InvariantCultureIgnoreCase) ||
							f.Name.EndsWith(".htmle", StringComparison.InvariantCultureIgnoreCase) ||
							f.Name.EndsWith(".mhtml", StringComparison.InvariantCultureIgnoreCase)
						)
						{
							fl.Add(f);
						}
					}
					files = fl.ToArray();
				}
				else
					files = directory.GetFiles("*", SkipHidden);

				if(!IgnoreMax && !LookingForHTML && files.Length > 19000)
				{
					HTML+= $"<span class=\"error\">Too many files ({files.Length} is more than 19000)\nOnly showing HTML files.</span><br />";
					
					files = directory.GetFiles("*.html", SkipHidden);
				}
				else if(!LookingForHTML)
				{
					foreach(var i in directory.EnumerateFiles("*.html*", SkipHidden))
					{
						var I = i.FullName.Substring(SourcePath.Length+1).Replace(Sep, '/');
						HTML+= $"<a class=\"file\" href=\"{HTMLURLEncode(I)}\">{i.Name}</a><br />";

						Console.WriteLine("File: {0}", i);
					}
					files = Array.FindAll(files, (i) => !(i.Extension.Equals(".html", StringComparison.InvariantCultureIgnoreCase) || i.Extension.Equals(".htmle", StringComparison.InvariantCultureIgnoreCase)));
				}
				foreach(var i in files)
				{
					var I = i.FullName.Substring(SourcePath.Length+1).Replace(Sep, '/');
					HTML+= $"<a class=\"file\" href=\"{HTMLURLEncode(I)}\">{i.Name}</a><br />";

					Console.WriteLine("File: {0}", i);
				}
				HTML+= "</body>\r\n</html>"u8;
				
				if(HTML is StringWriter HTMLs)
					return encoding.GetBytes(HTMLs.Data);
				else if(HTML is StringWriter_Builder HTMLsb)
					return encoding.GetBytes(HTMLsb.Data.ToString());
				else if(HTML is StringWriterEncoded HTMLe)
					return HTMLe.Data.ToArray();
				else
					throw new NotImplementedException();
			}

			public readonly string GetHeader(HeaderData header, WebResulterResult Result = default)
			{
				return BuildHeader(Expiry: DateTime.MinValue, Type: "text/html; charset="+encoding.WebName);
			}
		}

		struct ErrorWebResulter : WebResulter
		{
			public ErrorWebResulter(ushort Error) => this.Error = Error;

			public ushort Error;

			public readonly WebResulterResult GetResult(HeaderData header)
			{
				var p = $"{SourcePath}{Sep}{Error}.html";
				if(File.Exists(p))
				{
					return File.ReadAllBytes(p);
				}
				else
				{
					return Encoding.ASCII.GetBytes(Error.ToString());
				}
			}

			public readonly string GetHeader(HeaderData header, WebResulterResult Result = default)
			{
				var p = new FileInfo($"{SourcePath}{Sep}{Error}.html");
				if(p.Exists)
				{
					return BuildHeader((HttpStatusCode)Error, "text/html", Length: (Result != default ? Result.LongLength : p.Length).ToString());
				}
				else
				{
					return BuildHeader((HttpStatusCode)Error, "text/plain charset=us-ascii", Length: "3");
				}
			}
		}
		struct RedirectWebResulter : WebResulter
		{
			public RedirectWebResulter(string Redirect, bool IsFind = false)
			{
				this.Redirect = Redirect.Replace(Sep, '/');
				this.IsFind = IsFind;
			}

			public string Redirect;
			public bool IsFind;

			public readonly WebResulterResult GetResult(HeaderData header)
			{
				return new ErrorWebResulter(IsFind ? (ushort)302 : (ushort)301).GetResult(header);
			}

			public readonly string GetHeader(HeaderData header, WebResulterResult Result = default)
			{
				var p = new FileInfo($"{SourcePath}{Sep}{(IsFind ? "302" : "301")}.html");
				if(p.Exists)
					return BuildHeader(IsFind ? HttpStatusCode.Found : HttpStatusCode.Moved, "text/html", Length: (Result == default ? Result.LongLength : p.Length).ToString(), Append: $"location: {Redirect}\n");
				else
					return BuildHeader(IsFind ? HttpStatusCode.Found : HttpStatusCode.Moved, "text/plain; charset=us-ascii", Length: "3", Append: $"location: {Redirect}\n");
			}
		}

		struct MediaWebResulter : WebResulter
		{
			public static string GetHTML(string WebPath)
			{
				return $"<!DOCTYPE html>\r\n<head><html><head><meta charset=\"us-ascii\"><style>*{{width:100%;height:100%;margin:0;padding:0;overflow:hidden;background:black linear-gradient(to bottom,#222,#000);image-rendering:pixelated;object-fit:contain}}</style></head><body><video src=\"{HTMLURLEncode(WebPath)}\" controls></video></body></html>";
			}

			public static byte[] ErrorHtml = Encoding.ASCII.GetBytes("<!DOCTYPE html>\r\n<head><html><head><meta charset=\"us-ascii\"><style>*{background:black linear-gradient(to bottom,#222,#000);font-family:sans-serif;width:100%;height:100%;color:white;margin:0;text-align:center;align-content:center}</style></head><body>The video file was not found.</body></html>");

			public readonly WebResulterResult GetResult(HeaderData header)
			{
				var Path = header.ArgumentsP.GetValueOrDefault("path", "PROVIDE ?path");

				if(!FileWebResulter.Exists(SourcePath+Path))
				{
					//return new ErrorWebResulter(404).GetResult(header);
					return ErrorHtml;
				}
				
				return Encoding.ASCII.GetBytes(GetHTML(Path));
			}

			public readonly string GetHeader(HeaderData header, WebResulterResult Result = default)
			{
				var Path = header.ArgumentsP.GetValueOrDefault("path", "PROVIDE ?path");

				if(!FileWebResulter.Exists(SourcePath+Path))
				{
					//return new ErrorWebResulter(404).GetHeader(header);
					return BuildHeader(HttpStatusCode.OK, "text/html; charset=us-ascii", Length: ErrorHtml.Length.ToString());
				}

				return BuildHeader(HttpStatusCode.OK, "text/html; charset=us-ascii", Length: Result == default ? GetHTML(Path).Length.ToString() : Result.Length.ToString());
			}
		}

		static WebResulter? ResolvePath(HeaderData header)
		{
			if(File.Exists($"{SourcePath}{Sep}redirects.txt"))
			{ 
				var redirects = File.ReadAllLines($"{SourcePath}{Sep}redirects.txt", encoding);
				for(int i = 0; i < redirects.Length; i+= 2)
				{
					if(header.Path.Equals(redirects[i], StringComparison.InvariantCultureIgnoreCase))
					{
						return new RedirectWebResulter(redirects[i+1], false);
					}
				}
			}

			if(EnableExternalFileRedirects && header.Path.EndsWith(".webextredir", StringComparison.InvariantCultureIgnoreCase))
				return new RedirectWebResulter(header.Path[..^12], true);

			if(header.Path.EndsWith(Sep) && File.Exists($"{SourcePath}{header.Path}index.html"))
				return new RedirectWebResulter(header.Path+"index.html", true);
			else if(header.Path.EndsWith(Sep) && File.Exists($"{SourcePath}{header.Path}index.htmle"))
				return new RedirectWebResulter(header.Path+"index.htmle", true);

			if(string.IsNullOrWhiteSpace(header.Method))
				return new ErrorWebResulter(400);
			else if(header.Path.Substring(1) is "BrowseAll" or "BrowseHTML")
				return new FileBrowserWebResulter();
			else if(header.Path.Substring(1) is "MediaPlayer")
				return new MediaWebResulter();
			else
				return new FileWebResulter();
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		static unsafe void HandleUser(Socket d)
		{
			Console.WriteLine("User connected at {0}.\nUser is {1}.\n", DateTime.Now, d.RemoteEndPoint);

			var Sd = new NetworkStream(d, false);
			d.ReceiveBufferSize/= 6;
			d.SendBufferSize*= 2;

			Span<byte> b = stackalloc byte[d.ReceiveBufferSize];
			Console.WriteLine("Buffer Size: {0}", b.Length);
			HeaderData header = new();
			int ReadBytes;
			
			WebResulter result;
			try
			{
				while(d.Connected)
				{
					var WaitEnd = (uint)Environment.TickCount+MaxWaitTime;
					while(d.Available <= 0)
					{
						if((uint)Environment.TickCount > WaitEnd)
						{

							d.Close();
							Console.WriteLine("No data received for a long enough period of time, closed connection.");
							return;
						}
					}

					ReadBytes = d.Receive(b);
					if(ReadBytes <= 0)
						continue;

					#if false
					Console.ForegroundColor = ReceiveC;
					c.Write(b);
					Console.ForegroundColor = DefaultC;

					header.Parse(b);
					#else
					fixed(byte* D = b)
					{
						string txt = encoding.GetString(D, ReadBytes);
						header.Parse(txt);

						Console.ForegroundColor = ReceiveC;
						Console.WriteLine(txt);
						Console.ForegroundColor = DefaultC;
					}
					#endif

					if(header.RequestData.TryGetValue("User-Agent", out string agent))
					{
						bool? IsBlocked = null;
						if(BlockedUserAgents != null)
						{
							foreach(var i in BlockedUserAgents)
							{
								if(agent.Contains(i, StringComparison.InvariantCultureIgnoreCase))
								{
									IsBlocked = true;
									break;
								}
							}
						}

						if
						(
							IsBlocked ?? 
							(
								agent.Contains("modat.io", StringComparison.InvariantCultureIgnoreCase)
								|| agent.Contains("censys.io", StringComparison.InvariantCultureIgnoreCase)
								|| agent.Contains("infrawat.ch", StringComparison.InvariantCultureIgnoreCase)
								//|| agent.Contains("Mozilla/5.0", StringComparison.InvariantCultureIgnoreCase)
							)
						)
						{
							Console.WriteLine("Frick off brah.");

							d.SendFile($"{SourcePath}{Sep}403_response.bin");
							d.Close();
							return;
						}
					}

					Console.WriteLine("{1} file {0}", header.Path, header.Method);
					
					if(header.Path == "/")
					{
						header.Path = $"{Sep}index.htmle";
					}
					else
					{
						header.Path = header.Path.Replace('/', Sep);
					}
					Console.WriteLine("Resolved Path: {0}", header.Path);

					result = ResolvePath(header);
					
					var data = result.GetResult(header);
					string ContentEncoding = null;

					if(EnableCompression && data.LongLength > 326 && (CompressVeryLargeFiles || data.LongLength < (GetFileType(header.Path).StartsWith("video/", StringComparison.InvariantCultureIgnoreCase) ? VideoFile_MaxFileSizeForCompression : File_MaxFileSizeForCompression)) && (header.RequestData.TryGetValue("accept-encoding", out string accept)))
					{
						void AcceptEncoding(string Encoder)
						{
							if(accept.Contains(Encoder, StringComparison.InvariantCultureIgnoreCase))
								ContentEncoding = Encoder;
						}
						
						ContentEncoding = "identity";
						AcceptEncoding("br");
						AcceptEncoding("deflate");
						AcceptEncoding("gzip");
					}
					
					string AdjustedHeader = result.GetHeader(header, data);

					string? VaryHeader = "X-No-ContentLength";

					if(AdjustedHeader.StartsWith("HTTP/1.1 200 OK", StringComparison.InvariantCultureIgnoreCase))
						AdjustedHeader+= "accept-ranges: bytes\r\n";
					if(ContentEncoding != null)
					{
						VaryHeader+= ", Accept-Encoding";
						AdjustedHeader = $"{AdjustedHeader}content-encoding: {ContentEncoding}\r\n";
					}

					if(!string.IsNullOrWhiteSpace(VaryHeader))
						AdjustedHeader = $"{AdjustedHeader}Vary: {VaryHeader}\r\n";

					bool range = header.RequestData.TryGetValue("range", out string Range);
					fLong RangeStart = -1, RangeEnd = -1;
					if(range)
					{
						if(Range.StartsWith("bytes=", StringComparison.InvariantCultureIgnoreCase))
						{
							string sub;
							var i = Range.IndexOf('-', 6);
							sub = Range[6..i];
							Console.WriteLine(sub);
							if(!string.IsNullOrWhiteSpace(sub))
								RangeStart = fLong.Parse(sub);
							sub = Range.Substring(i+1);
							Console.WriteLine(sub);
							if(!string.IsNullOrWhiteSpace(sub))
								RangeEnd = fLong.Parse(sub);

							if(RangeStart <= 0 && RangeEnd == -1)
								range = false;
						}
						else
							range = false;

						AdjustedHeader+= "content-range: bytes ";
						if(RangeStart != -1)
							AdjustedHeader+= RangeStart;
						AdjustedHeader+= "-";
						if(RangeEnd != -1)
							AdjustedHeader+= RangeEnd;
						else
							AdjustedHeader+= data.LongLength-1;
						AdjustedHeader = $"{AdjustedHeader}/{data.LongLength}\r\n";
						VaryHeader+= ", Content-Range";

						AdjustedHeader = AdjustedHeader.Replace("HTTP/1.1 200 OK", "HTTP/1.1 206", StringComparison.InvariantCultureIgnoreCase);
					}

					if(range)
					{
						RangeStart = Math.Max(RangeStart, 0);
						if(RangeEnd == -1)
							RangeEnd = data.LongLength-1;

						if(data.ContentB != null)
						{
							//data.ContentB = new ArraySegment<byte>(data.ContentB, (int)RangeStart, (int)(RangeEnd-RangeStart+1)).ToArray();
							var old = data.ContentB;
							data.ContentB = new byte[RangeEnd-RangeStart+1];
							// MAYBE CHANGE THIS BEHAVIOUR!!!
							Array.Copy(old, RangeStart, data.ContentB, 0, Math.Min(data.ContentB.LongLength, old.LongLength));
						}
						if(data.ContentS != null)
							data.ContentS = new StreamSlice(data.ContentS, RangeStart, RangeEnd-RangeStart+1);

						var Part1I = AdjustedHeader.IndexOf("content-length: ", StringComparison.InvariantCultureIgnoreCase)+16;
						var Part1 = AdjustedHeader.AsSpan(0, Part1I);
						var Part2 = AdjustedHeader.AsSpan(AdjustedHeader.IndexOf('\r', Part1I));
						AdjustedHeader = $"{Part1}{data.LongLength}{Part2}";
					}

					bool FastFetch = IsYes(header.RequestData.GetValueOrDefault("x-no-contentlength", "false"));
					if(FastFetch)
						AdjustedHeader+= "X-No-ContentLength-Supported: true\r\n";

					AdjustedHeader+= "\r\n";

					Console.ForegroundColor = SendC;
					Console.WriteLine(AdjustedHeader);
					Console.ForegroundColor = DefaultC;

					void HEAD()
					{
						d.Send(encoding.GetBytes(AdjustedHeader));
					}

					void GET(bool IncludeHeaderAndSupportCompression = false)
					{
						if(IncludeHeaderAndSupportCompression)
						{
							int LenI;
							ReadOnlySpan<char> P1, P2;

							if(FastFetch)
							{
								LenI = AdjustedHeader.IndexOf("Content-Length: ", StringComparison.InvariantCultureIgnoreCase);
								P1 = AdjustedHeader.AsSpan(0, LenI); P2 = AdjustedHeader.AsSpan(AdjustedHeader.IndexOf('\n', LenI)-1);
								AdjustedHeader = string.Concat(P1, P2);
								HEAD();
							}

							Stream encoder;
							MemoryStream? mem = FastFetch ? null : new MemoryStream(data.LongLength > BufferLimit ? BufferLimit : data.Length);
							Stream WriteTo = FastFetch ? new NetworkStream(d, FileAccess.Write, false) : mem;

							switch(ContentEncoding)
							{
								default:
									Console.WriteLine(data.LongLength);
									HEAD();
									if(data.ContentB != null)
										d.Send(data.ContentB);
									else if(data.ContentS != null)
									{
										NetworkStream stream = new NetworkStream(d, FileAccess.Write, false);

										data.ContentS.CopyTo(stream, d.SendBufferSize);
										stream.Dispose();
									}
								return;
								case "br":
									encoder = Compression == (CompressionLevel)255 ? new BrotliStream(WriteTo, CompressionMode.Compress, true) : new BrotliStream(WriteTo, Compression, true);
								break;
								case "gzip":
									encoder = Compression == (CompressionLevel)255 ? new GZipStream(WriteTo, CompressionMode.Compress, true) : new GZipStream(WriteTo, Compression, true);
								break;
								case "deflate":
									encoder = Compression == (CompressionLevel)255 ? new DeflateStream(WriteTo, CompressionMode.Compress, true) : new DeflateStream(WriteTo, Compression, true);
								break;
							}
							if(data.ContentB != null)
								encoder.Write(data.ContentB);
							else if(data.ContentS != null)
								data.ContentS.CopyTo(encoder);
							encoder.Dispose();

							if(FastFetch)
								return;

							LenI = AdjustedHeader.IndexOf("Content-Length: ", StringComparison.InvariantCultureIgnoreCase)+16;
							P1 = AdjustedHeader.AsSpan(0, LenI); P2 = AdjustedHeader.AsSpan(AdjustedHeader.IndexOf('\n', LenI)-1);
							AdjustedHeader = $"{P1}{mem.Length}{P2}";
							Console.ForegroundColor = SendC;
							Console.WriteLine("ADJUSTED HEAD: {0}", AdjustedHeader);
							Console.ForegroundColor = DefaultC;
							HEAD();
							d.Send(mem.ToArray());
							mem.Dispose();
						}
						else
						{
							HEAD();
							if(data.ContentB != null)
								d.Send(data.ContentB);
							else if(data.ContentS != null)
							{
								NetworkStream stream = new NetworkStream(d, FileAccess.Write, false);
								data.ContentS.CopyTo(stream, d.SendBufferSize);
								stream.Dispose();
							}
						}
					}

					switch(header.Method)
					{
						case "GET":
						GET(true);

						/*if(data.LongLength > BufferLimit)
							GC.Collect(3, GCCollectionMode.Optimized, false);*/
						break;
						case "HEAD":
						HEAD();
						break;
					}

					if((data.flags & WebResulterResult.WebResulterResultFlags.DisposeOnDone) != 0)
						data.ContentS?.Dispose();

					if(ForceInstantCloseConnection || header.RequestData.GetValueOrDefault("connection", string.Empty).Equals("close", StringComparison.InvariantCultureIgnoreCase))
						d.Close(400);
				}
			}
			catch(Exception e)
			{
				if(e is SocketException s && s.Message.StartsWith("An established connection was aborted", StringComparison.CurrentCultureIgnoreCase))
				{
					Console.WriteLine("User forcefully disconnected during a data transmit.");
					goto Disconnect;
				}

				Console.ForegroundColor = ErrorC;
				Console.WriteLine(e);
				Console.ForegroundColor = DefaultC;
				if(d.Connected)
				{
					var text = encoding.GetBytes(e.ToString());
					d.Send(encoding.GetBytes(BuildHeader(HttpStatusCode.FailedDependency, "text/plain; charset="+encoding.WebName, Length: text.Length.ToString(), ConnectionMode: "Close")+"\r\n"));
					d.Send(text);
					d.Close(900);
				}
			}

		Disconnect:
			Console.WriteLine("User disconnected.");
			d.Dispose();
		}
	}
}
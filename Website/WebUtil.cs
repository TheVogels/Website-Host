using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;
using System.IO.Compression;
using System.Net.Security;
using System.Runtime.CompilerServices;
using static Website.Config;

namespace Website
{
	public static class WebUtil
	{
		public static string BuildHeader(HttpStatusCode Status = HttpStatusCode.OK, string Type = "text/html; charset=UTF-8", 
			string? Length = null, DateTime? LastModified = null, string? ConnectionMode = null, string? AcceptedEncoding = null, string? Append = null, DateTime? Expiry = null)
		{
			DateTime Now = DateTime.UtcNow;

			if(string.IsNullOrEmpty(ConnectionMode))
				ConnectionMode = ForceInstantCloseConnection ? "close" : "keep-open";

			string source = $@"HTTP/1.1 {(int)Status} {Status}
content-type: {Type}
connection: {ConnectionMode}
allow: GET, HEAD
expires: {(Expiry.HasValue ? (Expiry.Value == DateTime.MinValue ? "0" : Expiry.Value): Now+new TimeSpan(0,13,3,78)):r}
date: {Now:r}
";

			if(Length != null)
			{
				source+= $"content-length: {Length}\r\n";
			}
			if(LastModified.HasValue)
			{
				source+= $"last-modified: {LastModified:r}\r\n";
			}
			if(AcceptedEncoding != null)
			{
				source+= $"accept-encoding: {AcceptedEncoding}\r\n";
			}
			if(Append != null)
				source+= Append;

			return source;
		}

		public static string GetFileType(string Path) => 
			Path.Substring(Path.LastIndexOf('.')+1).ToLowerInvariant() switch
			{
				// Text
				"html" or "htm" => "text/html",
				"htmle" => "text/html; charset=UTF-8",
				"css" => "text/css",
				"js" => "text/javascript",
				"xml" => "text/xml",
				"csv" => "text/csv",
				"txt" => "text/plain",

				// Image
				"ico" => "image/x-icon",
				"gif" => "image/gif",
				"jpeg" or "jpg" or "jfif" => "image/jpeg",
				"jxr" => "image/jxr",
				"jxl" => "image/jxl",
				"png" => "image/png",
				"tiff" => "image/tiff",
				"svg" => "image/svg+xml",
				"avif" => "image/avif",
				"heic" or "heif" or "hif" => "image/heic",
				"dng" => "image/x-adobe-dng",
				"tga" => "image/x-tga",
				"dds" => "image/x-dds",
				"bmp" or "dib" or "rle" => "image/bmp",
				"webp" => "image/webp",

				// Video
				"mp4" => "video/mp4",
				"mpeg" => "video/mpeg",
				"wmv" => "video/x-ms-wmv",
				"flv" => "video/x-flv",
				"webm" => "video/webm",
				"mov" => "video/mov",
				"3gp" => "video/3gp",
				"avi" => "video/avi",

				// Audio
				"mp3" or "mp2" => "audio/mpeg",
				"wma" => "audio/x-ms-wma",
				"wav" => "audio/wav",
				"flac" => "audio/flac",
				"off" => "audio/x-off",
				"ofr" => "audio/x-ofr",
				"ofs" => "audio/x-ofs",

				_ => "octet-stream"
			};

		public static string UrlDecode(string URL) => HttpUtility.UrlDecode(URL).Replace("%2f", "/", StringComparison.InvariantCultureIgnoreCase);
		public static string UrlEncode(string URL) => HttpUtility.UrlEncode(URL).Replace("%2f", "/", StringComparison.InvariantCultureIgnoreCase);

		public static string HTMLEncode(string URL) => HttpUtility.HtmlEncode(URL).Replace("%2f", "/", StringComparison.InvariantCultureIgnoreCase);
		public static string HTMLURLEncode(string URL) => HttpUtility.HtmlEncode(HttpUtility.UrlEncode(URL)).Replace("%2f", "/", StringComparison.InvariantCultureIgnoreCase);

		public interface IStringWriter
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static IStringWriter operator +(IStringWriter a, string write)
			{
				a.Write(write);
				return a;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static IStringWriter operator +(IStringWriter a, ReadOnlySpan<byte> write)
			{
				a.Write(write);
				return a;
			}

			public abstract void Write(string write);

			public virtual void Write(ReadOnlySpan<byte> write) => Write(encoding.GetString(write));
		}

		public struct StringSocketWriter : IStringWriter
		{
			public readonly Socket Target;
			public Encoding Encode = encoding;

			public StringSocketWriter(Socket target) => this.Target = target;
			public StringSocketWriter(Socket target, Encoding encode) : this(target) => this.Encode = encode;

			public readonly void Write(string write)
			{
				Target.Send(Encode.GetBytes(write));
			}
			public readonly void Write(ReadOnlySpan<byte> write)
			{
				Target.Send(write);
			}
		}

		public struct StringWriter : IStringWriter
		{
			public StringWriter(string StartData) => Data = StartData;

			public string Data;

			public void Write(string write) => Data+= write;
		}
		public struct StringWriter_Builder : IStringWriter
		{
			public StringWriter_Builder()
			{
				Data = new StringBuilder();
			}
			public StringWriter_Builder(string StartData)
			{
				Data = new StringBuilder(StartData);
			}

			public StringBuilder Data;

			public readonly void Write(string write) => Data.Append(write);
		}

		public struct StringWriterEncoded() : IStringWriter
		{
			public StringWriterEncoded(Encoding encode) : this()
			{
				Encode = encode;
				Data = new();
			}
			public StringWriterEncoded(Encoding encode, int Capacity) : this()
			{
				Encode = encode;
				Data = new(Capacity);
			}

			public StringWriterEncoded(Encoding encode, IEnumerable<byte> StartData) : this()
			{
				Encode = encode;
				Data = new(StartData);
			}
			public StringWriterEncoded(Encoding encode, string StartData) : this(encode, (IEnumerable<byte>)encode.GetBytes(StartData))
			{

			}
			public StringWriterEncoded(Encoding encode, ReadOnlySpan<byte> StartData) : this()
			{
				Encode = encode;
				Data = [..StartData];
			}


			public Encoding Encode;
			public List<byte> Data;

			public readonly void Write(string write) => Write(Encode.GetBytes(write));
			public readonly void Write(ReadOnlySpan<byte> write) => Data.AddRange(write);
		}
	}
}
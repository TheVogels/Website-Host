using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Web;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
#pragma warning disable CS8600, CS8622, CS8602, IDE0028

namespace Website
{
	#if PreferReflection
	using System.Reflection;
	#pragma warning disable CS8604
	#endif

	public static class Util
	{
		#if PreferReflection
		const BindingFlags PrivateInstanceFlags = BindingFlags.NonPublic|BindingFlags.Instance;

		internal static class ReflectionCache<T>
		{
			public static readonly FieldInfo? ItemGet = typeof(List<T>).GetField("_items", PrivateInstanceFlags);
		}
		#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<T> ListToReadOnlySpan<T>(List<T> list)
		{
			#if !PreferReflection
			return list.ToArray();
			#else
			return new ArraySegment<T>( (T[])ReflectionCache<T>.ItemGet.GetValue(list), 0, list.Count );
			#endif
		}

		public static bool IsYes([NotNullWhen(true)] string? Value)
		{
			if(Value is null)
				return false;
			else
				return bool.TrueString.Equals(Value, StringComparison.InvariantCultureIgnoreCase) || "1" == Value || "yes".Equals(Value, StringComparison.InvariantCultureIgnoreCase);
		}

		public class StreamSlice : Stream
		{
			public StreamSlice(Stream Source, long Begin, long Length)
			{
				SourceStream = Source;
				Source.Position = Offset = Begin;
				SlicedLength = Length;
			}

			public Stream SourceStream;
			public long Offset, SlicedLength;

			public override bool CanRead
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => SourceStream.CanRead;
			}
			public override bool CanSeek
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => SourceStream.CanSeek;
			}
			public override bool CanWrite
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => SourceStream.CanWrite;
			}
			public override bool CanTimeout
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => SourceStream.CanTimeout;
			}

			public override long Length => SlicedLength;
			public override long Position
			{
				get => SourceStream.Position-Offset;
				set
				{
					if(value > SlicedLength || value < 0)
						throw new IndexOutOfRangeException();

					SourceStream.Position = value+Offset;
				}
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				var result = SourceStream.Seek(offset, origin);
				ThrowIfOutOfRange();
				return result;
			}

			public override int ReadTimeout
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => SourceStream.ReadTimeout;
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				set => SourceStream.ReadTimeout = value;
			}

			protected void ThrowIfOutOfRange()
			{
				if(Position > SlicedLength || Position < 0)
					throw new IndexOutOfRangeException();
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				var result = SourceStream.Read(buffer, offset, count);
				ThrowIfOutOfRange();
				return result;
			}
			public override int Read(Span<byte> buffer)
			{
				var result = SourceStream.Read(buffer);
				ThrowIfOutOfRange();
				return result;
			}
			public override int ReadByte()
			{
				var result = SourceStream.ReadByte();
				ThrowIfOutOfRange();
				return result;
			}
			
			public override void Write(byte[] buffer, int offset, int count)
			{
				SourceStream.Write(buffer, offset, count);
				ThrowIfOutOfRange();
			}
			public override void Write(ReadOnlySpan<byte> buffer)
			{
				SourceStream.Write(buffer);
				ThrowIfOutOfRange();
			}
			public override void WriteByte(byte value)
			{
				SourceStream.WriteByte(value);
				ThrowIfOutOfRange();
			}
			public override void Flush() => SourceStream.Flush();

			public override void SetLength(long value)
			{
				var oldLen = SlicedLength;
				SlicedLength = value;
			}

			public override void Close() => SourceStream.Close();
			public override System.Threading.Tasks.ValueTask DisposeAsync() => SourceStream.DisposeAsync();
			public override void CopyTo(Stream destination, int bufferSize) => SourceStream.CopyTo(destination, bufferSize);
			public override System.Threading.Tasks.Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => SourceStream.CopyToAsync(destination, bufferSize, cancellationToken);
			public override System.Threading.Tasks.Task FlushAsync(CancellationToken cancellationToken) => SourceStream.FlushAsync(cancellationToken);
			protected override void Dispose(bool disposing)
			{
				#if PreferReflection
				SourceStream.GetType().GetMethod("Dispose", BindingFlags.NonPublic|BindingFlags.Instance).Invoke(SourceStream, [disposing]);
				#else
				SourceStream.Dispose();
				#endif
				base.Dispose(disposing);
			}
		}
	}
}

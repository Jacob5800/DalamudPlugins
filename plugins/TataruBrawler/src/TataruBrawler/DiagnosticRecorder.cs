using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TataruBrawler;

public sealed class DiagnosticRecorder : IDisposable
{
	public const long SessionLimit = 67108864L;

	public const long TotalLimit = 268435456L;

	public const int BufferLimit = 65536;

	private readonly object gate = new object();

	private readonly string root;

	private readonly TimeProvider clock;

	private readonly Action<string, byte[]> append;

	private readonly MemoryStream buffer = new MemoryStream();

	private string? previous;

	private string session = "";

	private string? bufferedDate;

	private long started;

	private long flushed;

	private long totalAtStart;

	private long sequence;

	private bool disposed;

	public bool Active { get; private set; }

	public string State { get; private set; } = "DiagIdle";

	public long BytesWritten { get; private set; }

	public long Records { get; private set; }

	public string DirectoryPath => root;

	public DiagnosticRecorder(string root, TimeProvider? clock = null, Action<string, byte[]>? append = null)
	{
		this.root = Path.GetFullPath(root);
		this.clock = clock ?? TimeProvider.System;
		this.append = append ?? new Action<string, byte[]>(Append);
	}

	public bool Start()
	{
		lock (gate)
		{
			if (disposed || Active)
			{
				return Active;
			}
			try
			{
				string[] array = (Directory.Exists(root) ? Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories).Take(1001).ToArray() : Array.Empty<string>());
				if (array.Length > 1000)
				{
					State = "DiagLimit";
					return false;
				}
				totalAtStart = array.Sum((string x) => new FileInfo(x).Length);
				if (totalAtStart >= 268435456)
				{
					State = "DiagLimit";
					return false;
				}
				session = Guid.NewGuid().ToString("N");
				started = (flushed = clock.GetTimestamp());
				long bytesWritten = (Records = (sequence = 0L));
				BytesWritten = bytesWritten;
				previous = null;
				buffer.SetLength(0L);
				bufferedDate = null;
				Active = true;
				State = "DiagRecording";
				Add("start", new
				{
					schema = 1,
					plugin = "TataruBrawler",
					version = "1.0.8.0",
					timeZone = "Asia/Tokyo",
					samplingMs = 100,
					numbers = "KTG number array 161: count then 20 records of 13 integers; meanings are provisional",
					strings = "KTG string array 128: 20 triples, game words/reading/cast names",
					privacy = "No chat, player names, raw keys or input text"
				});
				return Active;
			}
			catch
			{
				State = "DiagError";
				Active = false;
				return false;
			}
		}
	}

	public void Observe(string snapshot)
	{
		lock (gate)
		{
			if (!Active || disposed)
			{
				return;
			}
			if (clock.GetElapsedTime(started) >= TimeSpan.FromMinutes(120L))
			{
				StopCore("DiagLimit");
				return;
			}
			if (snapshot != previous)
			{
				previous = snapshot;
				Add("snapshot", JsonSerializer.Deserialize<JsonElement>(snapshot));
			}
			if (Active && clock.GetElapsedTime(flushed) >= TimeSpan.FromSeconds(2L))
			{
				FlushCore();
			}
		}
	}

	public void Event(string name, object details)
	{
		lock (gate)
		{
			if (Active)
			{
				Add("event", new { name, details });
			}
		}
	}

	public void Mark(string kind)
	{
		lock (gate)
		{
			bool flag = Active;
			if (flag)
			{
				bool flag2 = ((kind == "block" || kind == "cast") ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				Add("marker", new { kind });
				FlushCore();
			}
		}
	}

	public void Stop()
	{
		lock (gate)
		{
			if (Active)
			{
				StopCore("DiagIdle");
			}
		}
	}

	public void Tick()
	{
		lock (gate)
		{
			if (Active && clock.GetElapsedTime(started) >= TimeSpan.FromMinutes(120L))
			{
				StopCore("DiagLimit");
			}
			else if (Active && clock.GetElapsedTime(flushed) >= TimeSpan.FromSeconds(2L))
			{
				FlushCore();
			}
		}
	}

	private void Add(string kind, object value)
	{
		if (!Active)
		{
			return;
		}
		DateTimeOffset utcNow = clock.GetUtcNow();
		string text = utcNow.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		byte[] array = JsonSerializer.SerializeToUtf8Bytes(new
		{
			schema = 1,
			session = session,
			sequence = ++sequence,
			utc = utcNow.ToString("O"),
			elapsedMs = clock.GetElapsedTime(started).TotalMilliseconds,
			kind = kind,
			data = value
		});
		if (array.Length > 32768)
		{
			StopCore("DiagLimit");
			return;
		}
		if (bufferedDate != null && bufferedDate != text)
		{
			FlushCore();
		}
		if (!Active)
		{
			return;
		}
		if (buffer.Length + array.Length + 1 > 65536)
		{
			FlushCore();
		}
		if (Active)
		{
			if (BytesWritten + buffer.Length + array.Length + 1 > 67108864 || totalAtStart + BytesWritten + buffer.Length + array.Length + 1 > 268435456)
			{
				StopCore("DiagLimit");
				return;
			}
			bufferedDate = text;
			buffer.Write(array);
			buffer.WriteByte(10);
			Records++;
		}
	}

	private void StopCore(string state)
	{
		FlushCore();
		Active = false;
		if (State != "DiagError")
		{
			State = state;
		}
		previous = null;
	}

	private void FlushCore()
	{
		if (buffer.Length == 0L)
		{
			flushed = clock.GetTimestamp();
			return;
		}
		try
		{
			byte[] array = buffer.ToArray();
			append(Path.Combine(root, bufferedDate, "ktg-" + session + ".jsonl"), array);
			BytesWritten += array.Length;
			buffer.SetLength(0L);
			flushed = clock.GetTimestamp();
		}
		catch
		{
			Active = false;
			State = "DiagError";
			buffer.SetLength(0L);
			previous = null;
		}
	}

	private static void Append(string path, byte[] bytes)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		using FileStream fileStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
		fileStream.Write(bytes);
		fileStream.Flush(flushToDisk: true);
	}

	public void Dispose()
	{
		lock (gate)
		{
			if (!disposed)
			{
				if (Active)
				{
					StopCore("DiagIdle");
				}
				disposed = true;
				buffer.Dispose();
			}
		}
	}
}

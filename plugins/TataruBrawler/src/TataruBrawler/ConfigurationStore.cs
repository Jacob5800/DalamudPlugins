using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TataruBrawler;

public sealed class ConfigurationStore : IDisposable
{
	private sealed record Snapshot(int Version, string? Language, int MinimumMs, int MaximumMs, bool AutoStart, bool AutoEnableOnMap, bool AutoQueueOnLobbyEntry, bool BackgroundInput, int BetweenWordsMs, bool RecordDiagnostics, int LoopCount, bool ShowServerBarButton, bool SkipOptionalPunctuation, bool WarnMgcCap, int MgcWarningPercent, bool IllegalMode, bool AutoLeaveDuty, bool AutoCommencePartyMember)
	{
		public static Snapshot From(Configuration c)
		{
			return new Snapshot(c.Version, c.Language, c.MinimumMs, c.MaximumMs, c.AutoStart, c.AutoEnableOnMap, c.AutoQueueOnLobbyEntry, c.BackgroundInput, c.BetweenWordsMs, c.RecordDiagnostics, c.LoopCount, c.ShowServerBarButton, c.SkipOptionalPunctuation, c.WarnMgcCap, c.MgcWarningPercent, c.IllegalMode, c.AutoLeaveDuty, c.AutoCommencePartyMember);
		}

		public Configuration ToConfig()
		{
			return new Configuration
			{
				Version = Version,
				Language = Language,
				MinimumMs = MinimumMs,
				MaximumMs = MaximumMs,
				AutoStart = AutoStart,
				AutoEnableOnMap = AutoEnableOnMap,
				AutoQueueOnLobbyEntry = AutoQueueOnLobbyEntry,
				BackgroundInput = BackgroundInput,
				BetweenWordsMs = BetweenWordsMs,
				RecordDiagnostics = RecordDiagnostics,
				LoopCount = LoopCount,
                ShowServerBarButton = ShowServerBarButton,
                SkipOptionalPunctuation = SkipOptionalPunctuation,
                WarnMgcCap = WarnMgcCap,
                MgcWarningPercent = MgcWarningPercent,
                IllegalMode = IllegalMode,
                AutoLeaveDuty = AutoLeaveDuty,
                AutoCommencePartyMember = AutoCommencePartyMember
			};
		}
	}

	private readonly object gate = new object();

	private readonly string path;

	private readonly TimeProvider time;

	private readonly Action<Exception>? onStopped;

	private readonly Action<Exception>? onFinalFailure;

	private readonly Action<string>? beforeCommit;

	private readonly Action<Stream, byte[]>? writePayload;

	private Snapshot current;

	private Snapshot? saved;

	private Snapshot? attemptedFinal;

	private long checkedAt;

	private int failures;

	private bool disposed;

	private bool writing;

	private bool notified;

	public bool LoadBlocked { get; private set; }

	public Configuration Configuration { get; }

	public bool Recovered { get; private set; }

	public string? LoadWarning { get; private set; }

	public Exception? LastError { get; private set; }

	public bool Stopped
	{
		get
		{
			lock (gate)
			{
				return failures >= 3;
			}
		}
	}

	public bool Dirty
	{
		get
		{
			lock (gate)
			{
				return current != saved;
			}
		}
	}

	public long SaveCount { get; private set; }

	public long LogicalBytesWritten { get; private set; }

	public long BackupBytesCreated { get; private set; }

	public long SaveAttempts { get; private set; }

	public string BackupDirectory => path + ".backups";

	public ConfigurationStore(string path, Action<Exception>? onStopped = null, TimeProvider? clock = null, Action<string>? beforeCommit = null, Action<Exception>? onFinalFailure = null, Action<Stream, byte[]>? writePayload = null)
	{
		this.path = path;
		this.onStopped = onStopped;
		this.onFinalFailure = onFinalFailure;
		this.beforeCommit = beforeCommit;
		this.writePayload = writePayload;
		time = clock ?? TimeProvider.System;
		checkedAt = time.GetTimestamp();
		Configuration = Load();
		current = Snapshot.From(Configuration);
	}

	public void Update(Configuration config)
	{
		lock (gate)
		{
			if (!disposed)
			{
				Snapshot snapshot = Snapshot.From(config);
				if (snapshot != current)
				{
					attemptedFinal = null;
				}
				current = snapshot;
			}
		}
	}

	public void Tick()
	{
		lock (gate)
		{
			if (!disposed && !(time.GetElapsedTime(checkedAt) < TimeSpan.FromSeconds(60L)))
			{
				checkedAt = time.GetTimestamp();
				if (failures < 3)
				{
					Save();
				}
			}
		}
	}

	public void Retry()
	{
		lock (gate)
		{
			if (disposed)
			{
				return;
			}
			if (LoadBlocked)
			{
				LoadBlocked = false;
				LoadWarning = null;
				LastError = null;
				Configuration configuration = Load();
				if (LoadBlocked)
				{
					return;
				}
				Configuration.Version = configuration.Version;
				Configuration.Language = configuration.Language;
				Configuration.MinimumMs = configuration.MinimumMs;
				Configuration.MaximumMs = configuration.MaximumMs;
				Configuration.AutoStart = configuration.AutoStart;
				Configuration.AutoEnableOnMap = configuration.AutoEnableOnMap;
				Configuration.AutoQueueOnLobbyEntry = configuration.AutoQueueOnLobbyEntry;
				Configuration.BackgroundInput = configuration.BackgroundInput;
				Configuration.BetweenWordsMs = configuration.BetweenWordsMs;
				Configuration.RecordDiagnostics = configuration.RecordDiagnostics;
				Configuration.LoopCount = configuration.LoopCount;
                Configuration.ShowServerBarButton = configuration.ShowServerBarButton;
                Configuration.SkipOptionalPunctuation = configuration.SkipOptionalPunctuation;
                Configuration.WarnMgcCap = configuration.WarnMgcCap;
                Configuration.MgcWarningPercent = configuration.MgcWarningPercent;
                Configuration.IllegalMode = configuration.IllegalMode;
                Configuration.AutoLeaveDuty = configuration.AutoLeaveDuty;
                Configuration.AutoCommencePartyMember = configuration.AutoCommencePartyMember;
				current = Snapshot.From(configuration);
			}
			failures = 0;
			notified = false;
			attemptedFinal = null;
			LastError = null;
			checkedAt = time.GetTimestamp();
			Save();
		}
	}

	public void Flush()
	{
		lock (gate)
		{
			if (!disposed && !writing && !(current == attemptedFinal))
			{
				attemptedFinal = current;
				Save();
				if (current != saved && LastError != null && !notified)
				{
					notified = true;
					onFinalFailure?.Invoke(LastError);
				}
			}
		}
	}

	public void Dispose()
	{
		lock (gate)
		{
			if (disposed)
			{
				return;
			}
			try
			{
				Flush();
			}
			finally
			{
				disposed = true;
			}
		}
	}

	private void Save()
	{
		if (writing || LoadBlocked || current == saved)
		{
			return;
		}
		writing = true;
		Snapshot value = current;
		string text = null;
		try
		{
			SaveAttempts++;
			byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
			text = WriteTemporary(bytes);
			beforeCommit?.Invoke(text);
			if (!TryRead(text, out Configuration config))
			{
				throw new InvalidDataException("Temporary configuration was corrupted.");
			}
			string destinationBackupFileName = null;
			long num = 0L;
			if (File.Exists(path))
			{
				if (TryRead(path, out config))
				{
					Directory.CreateDirectory(BackupDirectory);
					long value2 = (from p in Backups()
						select long.Parse(Path.GetFileNameWithoutExtension(p))).DefaultIfEmpty(0L).Max() + 1;
					destinationBackupFileName = Path.Combine(BackupDirectory, $"{value2:D20}.json");
					num = new FileInfo(path).Length;
				}
				else
				{
					PreserveCorrupt();
				}
				File.Replace(text, path, destinationBackupFileName);
			}
			else
			{
				File.Move(text, path);
			}
			text = null;
			saved = value;
			SaveCount++;
			BackupBytesCreated += num;
			failures = 0;
			notified = false;
			LastError = null;
			try
			{
				foreach (string item in (from p in Backups()
					where TryRead(p, out Configuration _)
					select p).Skip(3))
				{
					File.Delete(item);
				}
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
		catch (Exception ex3)
		{
			Exception obj = (LastError = ex3);
			if (++failures == 3 && !notified)
			{
				notified = true;
				onStopped?.Invoke(obj);
			}
		}
		finally
		{
			if (text != null)
			{
				try
				{
					File.Delete(text);
				}
				catch (IOException)
				{
				}
				catch (UnauthorizedAccessException)
				{
				}
			}
			writing = false;
		}
	}

	private Configuration Load()
	{
		if (TryRead(path, out Configuration config, out Exception error))
		{
			saved = Snapshot.From(config);
			return config;
		}
		if (error is UnauthorizedAccessException || (error is IOException && !(error is FileNotFoundException) && !(error is DirectoryNotFoundException)))
		{
			LoadBlocked = true;
			failures = 3;
			LastError = error;
			LoadWarning = error.Message;
			return new Configuration();
		}
		try
		{
			foreach (string item in Backups())
			{
				if (!TryRead(item, out config))
				{
					continue;
				}
				Recovered = true;
				try
				{
					string sourceFileName = WriteTemporary(JsonSerializer.SerializeToUtf8Bytes(Snapshot.From(config)));
					try
					{
						if (File.Exists(path))
						{
							PreserveCorrupt();
							File.Replace(sourceFileName, path, null);
						}
						else
						{
							File.Move(sourceFileName, path);
						}
						saved = Snapshot.From(config);
					}
					finally
					{
						if (File.Exists(sourceFileName))
						{
							File.Delete(sourceFileName);
						}
					}
				}
				catch (Exception ex)
				{
					LoadWarning = ex.Message;
				}
				return config;
			}
		}
		catch (Exception ex2)
		{
			LoadWarning = ex2.Message;
		}
		if (File.Exists(path) && LoadWarning == null)
		{
			string text = (LoadWarning = "No valid configuration or backup.");
		}
		return new Configuration();
	}

	private string WriteTemporary(byte[] bytes)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string text = path + ".tmp." + Guid.NewGuid().ToString("N");
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				try
				{
					if (writePayload == null)
					{
						fileStream.Write(bytes);
					}
					else
					{
						writePayload(fileStream, bytes);
					}
				}
				finally
				{
					LogicalBytesWritten += fileStream.Position;
				}
				fileStream.Flush(flushToDisk: true);
			}
			if (!TryRead(text, out Configuration _))
			{
				throw new InvalidDataException("Configuration validation failed.");
			}
			return text;
		}
		catch
		{
			try
			{
				File.Delete(text);
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
			throw;
		}
	}

	private void PreserveCorrupt()
	{
		File.Copy(path, path + ".corrupt." + Guid.NewGuid().ToString("N"));
	}

	private IEnumerable<string> Backups()
	{
		if (!Directory.Exists(BackupDirectory))
		{
			return Array.Empty<string>();
		}
		return (from p in Directory.EnumerateFiles(BackupDirectory, "*.json")
			where long.TryParse(Path.GetFileNameWithoutExtension(p), out var _)
			select p).OrderByDescending((string p) => Path.GetFileName(p), StringComparer.Ordinal);
	}

	private static bool TryRead(string file, out Configuration? config)
	{
		Exception error;
		return TryRead(file, out config, out error);
	}

	private static bool TryRead(string file, out Configuration? config, out Exception? error)
	{
		config = null;
		error = null;
		try
		{
			using FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
			if (fileStream.Length > 1048576)
			{
				return false;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse((Stream)fileStream, default(JsonDocumentOptions));
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.ValueKind != JsonValueKind.Object || !rootElement.TryGetProperty("Version", out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var value2) || value2 != 1)
			{
				return false;
			}
			config = rootElement.Deserialize<Configuration>();
			int result;
			if (config != null)
			{
				int loopCount = config.LoopCount;
				if (loopCount >= 1 && loopCount <= 9999)
				{
					loopCount = config.BetweenWordsMs;
					if (loopCount >= 0 && loopCount <= 60000 && config.MinimumMs >= (config.IllegalMode ? 0 : 20) && config.MaximumMs <= 5000 && config.MinimumMs <= config.MaximumMs)
					{
						result = ((config.Language == null || Loc.IsSupported(config.Language)) ? 1 : 0);
						goto IL_00f9;
					}
				}
			}
			result = 0;
			goto IL_00f9;
			IL_00f9:
			return (byte)result != 0;
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? true : false)
		{
			error = ex;
			return false;
		}
	}
}

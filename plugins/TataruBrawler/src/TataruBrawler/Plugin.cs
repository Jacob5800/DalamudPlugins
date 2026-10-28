using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface;

using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using TataruBrawler.Ui;

namespace TataruBrawler;

public sealed class Plugin : IDalamudPlugin, IDisposable
{
	private const string Command = "/tatarubrawler";

	private readonly IDalamudPluginInterface pi;

	private readonly ICommandManager commands;

	private readonly IFramework framework;

	private readonly IClientState client;

	private readonly IDataManager data;

	private readonly IPluginLog log;

	private readonly ConfigurationStore settings;

	private readonly Configuration config;
	private readonly IDtrBarEntry serverBarEntry;

	private readonly LocalizedFonts fonts;

	private readonly GameReader reader;

	private readonly TypingSession typing = new TypingSession();

	private readonly ImeInput ime = new ImeInput();

	private readonly EncounterPolicy encounter = new EncounterPolicy();

	private readonly AutomationLifecycle automation = new AutomationLifecycle();

	private readonly MapEntrySwitch mapEntry = new MapEntrySwitch(1359u);

	private readonly MapEntrySwitch lobbyEntry = new MapEntrySwitch(1360u);

	private readonly RoundLoop loop = new RoundLoop();

	private readonly ActivityKeeper activity = new ActivityKeeper();

	private readonly LoopGameUi loopUi;

	private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly BackgroundKeys backgroundKeys = new();
    private readonly RewardTracker rewards;

	private long loopSample;

	private string controlGate = "Idle";

	private string preparedWord = "";

	private uint currentEntity;

	private readonly DiagnosticRecorder diagnostics;

	private readonly DiagnosticReader diagnosticReader;

	private bool diagnosticAttempted;

	private long lastDiagnosticSample;

	private readonly CommandInfo command;

	private readonly CancellationTokenRegistration shutdown;

	private readonly CancellationTokenRegistration unload;

	private readonly object executionGate = new object();

	private volatile bool stopping;

	private bool open;

	private bool disposed;

	private long scanned;

	private uint territory;

	private bool inContent;

	private int failures;

	private bool uiCaptures;

	private bool autoQueuePaused;

	private bool autoQueueLoop;

	private bool autoQueueVisitArmed = true;

	public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IFramework framework, IClientState client, IDataManager data, IGameGui gui, IPluginLog log, IGameLifecycle lifecycle, ICondition condition, IObjectTable objects, IDtrBar dtrBar)
	{
		Plugin plugin = this;
		this.pi = pi;
		this.commands = commands;
		this.framework = framework;
		this.client = client;
		this.data = data;
		this.log = log;
		this.condition = condition;
        this.objects = objects;
        rewards = new RewardTracker(Path.Combine(pi.GetPluginConfigDirectory(), "rewards.json"), gui);
		loopUi = new LoopGameUi(gui, data, objects);
		Loc.CheckCompleteness();
		settings = new ConfigurationStore(pi.ConfigFile.FullName, (Exception ex) =>
		{
			log.Error(ex, "Configuration saving stopped");
			plugin.open = true;
		}, null, null, (Exception ex) =>
		{
			log.Error(ex, "Final configuration flush failed");
			plugin.open = true;
		});
		config = settings.Configuration;
		if (StartupDefaults.Apply(config))
		{
			settings.Update(config);
		}
		reader = new GameReader(gui);
		diagnostics = new DiagnosticRecorder(Path.Combine(pi.GetPluginConfigDirectory(), "diagnostics"));
		diagnosticReader = new DiagnosticReader(gui);
		fonts = new LocalizedFonts(pi.UiBuilder.FontAtlas, log, new _003C_003Ez__ReadOnlySingleElementList<string>(diagnostics.DirectoryPath));
		open = settings.Recovered || settings.LoadWarning != null;
		command = new CommandInfo((string _, string args) =>
		{
			if (args.Trim().Equals("log start", StringComparison.OrdinalIgnoreCase))
			{
				plugin.StartDiagnostics();
			}
			else if (args.Trim().Equals("log stop", StringComparison.OrdinalIgnoreCase))
			{
				plugin.SetDiagnostics(enabled: false);
			}
			else if (args.Trim().Equals("mark block", StringComparison.OrdinalIgnoreCase))
			{
				plugin.diagnostics.Mark("block");
			}
			else if (args.Trim().Equals("mark cast", StringComparison.OrdinalIgnoreCase))
			{
				plugin.diagnostics.Mark("cast");
			}
			else if (args.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase))
			{
				plugin.StopByUser();
			}
			else
			{
				plugin.open = true;
			}
		})
		{
			HelpMessage = T("CommandHelp")
		};
		commands.AddHandler("/tatarubrawler", command);
		framework.Update += Update;
		pi.UiBuilder.Draw += Draw;
		pi.UiBuilder.OpenMainUi += Open;
		pi.UiBuilder.OpenConfigUi += Open;
		client.Logout += Logout;
		shutdown = lifecycle.GameShuttingDownToken.Register(Stop);
		unload = lifecycle.DalamudUnloadingToken.Register(Stop);
        serverBarEntry = dtrBar.Get("Keybound Automation", "KB");
        serverBarEntry.Tooltip = "Click to open Keybound Automation";
        serverBarEntry.OnClick = _ => open = !open;
        serverBarEntry.Shown = config.ShowServerBarButton;
	}

	private string T(string key)
	{
		return Loc.Get(config.Language, key);
	}

	private void Open()
	{
		open = true;
	}

	private void Logout(int type, int code)
	{
		lock (executionGate)
		{
			mapEntry.Observe(loggedIn: false, 0u);
			lobbyEntry.Observe(loggedIn: false, 0u);
			autoQueueVisitArmed = true;
			autoQueuePaused = false;
			autoQueueLoop = false;
			loop.Stop();
			typing.Stop();
			backgroundKeys.Release(force: true);
            Keyboard.Release(force: true);
			ime.Restore();
			encounter.Reset();
			automation.Reset();
			reader.Clear();
			diagnostics.Stop();
			diagnosticReader.Reset();
			diagnosticAttempted = false;
			settings.Flush();
            rewards.EndVisit();
            rewards.Flush();
		}
	}

	private void Stop()
	{
		lock (executionGate)
		{
			stopping = true;
            rewards.Flush();
			loop.Stop();
			typing.Stop();
			backgroundKeys.Release(force: true);
            Keyboard.Release(force: true);
			ime.Restore();
			diagnostics.Dispose();
			settings.Dispose();
		}
	}

	private bool IsContent()
	{
		if (!client.IsLoggedIn)
		{
			return false;
		}
		if (territory != client.TerritoryType || territory == 0)
		{
			territory = client.TerritoryType;
			TerritoryType? rowOrDefault = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory);
			inContent = rowOrDefault.HasValue && rowOrDefault.GetValueOrDefault().TerritoryIntendedUse.RowId == 65;
		}
		return inContent;
	}

	private void Update(IFramework _)
	{
		lock (executionGate)
		{
			UpdateCore();
		}
	}

	private void StartTyping(long now, string source)
	{
		SetPlayEnabled(enabled: true);
		automation.Start(now);
		activity.Reset();
		failures = 0;
		scanned = 0L;
		reader.ResetFocus();
		if (typing.Running && typing.ManualPaused)
		{
			typing.ResumeByUser();
		}
		else
		{
			preparedWord = "";
			currentEntity = 0u;
			typing.Start(now);
		}
		controlGate = "Starting";
		diagnostics.Event("typing_start", new { source });
	}

	private void StopByUser()
	{
		SetPlayEnabled(enabled: false);
		loop.Stop();
		activity.Reset();
		automation.Suppress();
		typing.Stop();
		controlGate = "ManualStop";
		backgroundKeys.Release(force: true);
            Keyboard.Release(force: true);
		ime.Restore();
	}

	private void SetPlayEnabled(bool enabled)
	{
		if (config.AutoStart != enabled)
		{
			config.AutoStart = enabled;
			settings.Update(config);
		}
	}

	private void UpdateCore()
	{
		if (stopping)
		{
			return;
		}
		settings.Tick();
        rewards.Tick(Environment.TickCount64, client.IsLoggedIn, client.TerritoryType, condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]);
		if (mapEntry.Observe(client.IsLoggedIn, client.TerritoryType) && config.AutoEnableOnMap)
		{
			SetPlayEnabled(enabled: true);
			automation.Reset();
			StartTyping(Environment.TickCount64, "duty_entry");
            controlGate = "MapEntry";
		}
		bool flag = !client.IsLoggedIn;
		if (!flag)
		{
			uint territoryType = client.TerritoryType;
			bool flag2 = territoryType - 1359 <= 1;
			flag = !flag2;
		}
		if (flag)
		{
			autoQueueVisitArmed = true;
		}
		if (lobbyEntry.Observe(client.IsLoggedIn, client.TerritoryType) && autoQueueVisitArmed && config.AutoQueueOnLobbyEntry && loopUi.Supported && !loop.Active)
		{
			autoQueueVisitArmed = false;
			StartLoop(automatic: true);
		}
		try
		{
			UpdateDiagnostics();
		}
		catch (Exception exception)
		{
			diagnostics.Stop();
			diagnosticAttempted = true;
			log.Error(exception, "KTG diagnostic sampling stopped");
		}
		try
		{
			long tickCount = Environment.TickCount64;
			if (!backgroundKeys.Release() || !Keyboard.Release())
			{
				typing.Stop("SendFailed");
				controlGate = "KeyReleaseFailed";
				if (loop.Active)
				{
					loop.Stop(error: true);
				}
				return;
			}
			UpdateLoop(tickCount);
			bool foreground = Keyboard.ForegroundGame;
			bool background = config.BackgroundInput && !foreground;
			bool flag3 = IsContent();
			activity.Tick(tickCount, flag3 && typing.Running && !typing.ManualPaused && !loop.Paused, ContentActivity.Touch);
			if (!flag3)
			{
				automation.Reset();
				reader.Clear();
				encounter.Reset();
				if (typing.Running)
				{
					typing.Stop("Completed");
					controlGate = "OutsideContent";
				}
				return;
			}
			if (flag3 && (typing.Running || loop.Active || config.AutoStart) && tickCount - scanned >= 50)
			{
				reader.Refresh();
				scanned = tickCount;
                if (client.TerritoryType == 1359 && encounter.Update(tickCount, BlockingCastReader.Read(objects)))
                    diagnostics.Event("input_block_cast", new { waitMs = 4000, source = "native_action_id" });
			}
			if (reader.ContentComplete)
			{
				automation.Suppress();
				typing.Stop("Completed");
				controlGate = "ContentComplete";
				backgroundKeys.Release(force: true);
            Keyboard.Release(force: true);
				return;
			}
			if (!typing.Running)
			{
				ime.Restore();
				preparedWord = "";
				currentEntity = 0u;
				if ((flag3 & foreground) && Keyboard.Escape)
				{
					StopByUser();
					return;
				}
				if (loop.Active || !config.AutoStart || automation.Consumed)
				{
					return;
				}
				bool playable = reader.Available && reader.InputEnabled && reader.Words.Count > 0;
				if (!automation.TryAutoStart(flag3, config.AutoStart, playable, (foreground | background) && (!foreground || (!uiCaptures && !Keyboard.Modifier && !Keyboard.Escape)) && !reader.CompetingFocus()))
				{
					return;
				}
				StartTyping(tickCount, "game_started");
			}
			RunGate runGate = automation.Evaluate(tickCount, flag3, foreground && Keyboard.Escape, foreground | background, foreground && Keyboard.Modifier, reader.InputEnabled && reader.Available, foreground && uiCaptures, reader.CompetingFocus());
			controlGate = runGate.ToString();
			switch (runGate)
			{
			case RunGate.Escape:
				StopByUser();
				break;
			default:
				reader.ResetFocus();
				backgroundKeys.Release(force: true);
            Keyboard.Release(force: true);
				typing.Tick(tickCount, ready: true, null, currentStillVisible: false, config.MinimumMs, config.MaximumMs, (char _) => false, paused: true);
				break;
			case RunGate.Run:
			{
				bool flag4 = encounter.Paused(tickCount) || typing.ManualPaused || (background && (typing.Japanese || !backgroundKeys.Ready));
				bool flag5 = typing.Submitted || (!flag4 && reader.EnsureFocus(tickCount));
				if (typing.CurrentWord.Length == 0)
				{
					preparedWord = "";
					currentEntity = 0u;
				}
				if ((!flag4 & flag5) && !typing.Recovering && typing.NeedsPreparation && preparedWord != typing.CurrentWord)
				{
					if (!string.IsNullOrEmpty(reader.EvaluatedText()))
					{
						controlGate = "ExistingInput";
						typing.RecoverIme(tickCount);
						break;
					}
					if (!background && !ime.Prepare(typing.Japanese))
					{
						controlGate = "ImePreparation";
						typing.RecoverIme(tickCount);
						break;
					}
					if (background)
					{
						ime.Restore();
					}
					preparedWord = typing.CurrentWord;
					controlGate = "ImeSettling";
					break;
				}
				ImeState imeState = (background ? default(ImeState) : ime.Inspect());
				if (((!background && !flag4) & flag5) && !typing.Recovering && typing.Japanese && !typing.Submitted && preparedWord.Length > 0 && (!imeState.Available || !imeState.Open || (imeState.Conversion & 3) != 1))
				{
					controlGate = "ImeState";
					typing.RecoverIme(tickCount);
					break;
				}
				string currentWord = typing.CurrentWord;
				ref string reference = ref controlGate;
				string text;
				if (typing.ManualPaused)
				{
					text = "ManualPause";
				}
				else if (background && typing.Japanese)
				{
					text = "BackgroundJapaneseWait";
				}
				else if (flag4)
				{
					text = "JammerWait";
				}
				else if (flag5)
				{
					text = (background ? "BackgroundTyping" : "Typing");
				}
				else
				{
					text = "FocusWait";
				}
				reference = text;
				string candidate = (background ? reader.Words.FirstOrDefault((string w) =>
				{
					TypingPlan typingPlan = TypingPlan.Create(w);
					return (object)typingPlan != null && !typingPlan.Japanese;
				}) : reader.Words.FirstOrDefault());
				typing.Tick(tickCount, ready: true, candidate, reader.StillVisible(typing.CurrentWord, currentEntity), config.MinimumMs, config.MaximumMs, (char c) => !stopping && IsContent() && (!foreground || !uiCaptures) && !encounter.Paused(Environment.TickCount64) && (c == '\b' || reader.StillVisible(typing.CurrentWord, currentEntity)) && ((!background) ? (reader.FocusedNow() && Keyboard.Send(c)) : reader.SendBackground(c, typing.CurrentWord, backgroundKeys)), flag4 || !flag5, imeState.Composing, reader.EvaluatedText(), config.BetweenWordsMs);
				if (currentWord.Length == 0 && typing.CurrentWord.Length > 0)
				{
					currentEntity = reader.Source(typing.CurrentWord);
				}
				failures = 0;
				break;
			}
			}
		}
		catch (Exception exception2)
		{
			controlGate = "Exception";
			if (loop.Active)
			{
				loop.Stop(error: true);
			}
			typing.Stop("Fault");
			open = true;
			if (++failures <= 3)
			{
				log.Error(exception2, "Keybound read stopped; restart is manual");
			}
		}
		finally
		{
			if (!typing.Running)
			{
				ime.Restore();
				if (loop.Active && loop.Phase == LoopPhase.Playing && typing.Status != "Completed")
				{
					loop.Stop(error: true);
				}
			}
		}
	}

	private void StartLoop()
	{
		StartLoop(automatic: false);
	}

	private void StartLoop(bool automatic)
	{
		bool flag = !loopUi.Supported || !client.IsLoggedIn;
		if (!flag)
		{
			uint territoryType = client.TerritoryType;
			bool flag2 = territoryType - 1359 <= 1;
			flag = !flag2;
		}
		if (!flag)
		{
			long tickCount = Environment.TickCount64;
			autoQueueLoop = automatic;
			autoQueuePaused = false;
			typing.Stop();
			backgroundKeys.Release(force: true);
            Keyboard.Release(force: true);
			ime.Restore();
			activity.Reset();
			automation.Suppress();
			SetPlayEnabled(enabled: true);
			loop.Start(tickCount, config.LoopCount, IsContent(), automatic);
			loopSample = 0L;
			if (IsContent())
			{
				StartTyping(tickCount, "loop_current_game");
			}
		}
	}

	private void UpdateLoop(long now)
	{
		if (!loop.Active)
		{
			return;
		}
		if (Keyboard.ForegroundGame && Keyboard.Escape)
		{
			StopByUser();
		}
		else
		{
			if (now - loopSample < 200)
			{
				return;
			}
			loopSample = now;
			bool loading = condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];
			bool foregroundGame = Keyboard.ForegroundGame;
			RoundLoop roundLoop = loop;
			LoopSnapshot state = loopUi.Read(client.IsLoggedIn, client.TerritoryType, loading);
            // Capture results before the loop can close a short-lived result UI.
            if (state.Result) rewards.Tick(now, client.IsLoggedIn, client.TerritoryType, loading, force: true);
			bool flag = (!foregroundGame && !config.BackgroundInput) || (foregroundGame && (Keyboard.Modifier || uiCaptures));
			LoopPhase phase;
			if (!flag)
			{
				bool flag2 = autoQueueLoop && autoQueuePaused;
				if (flag2)
				{
					phase = loop.Phase;
					bool flag3 = (uint)(phase - 1) <= 1u;
					flag2 = flag3;
				}
				flag = flag2;
			}
			LoopAction loopAction = roundLoop.Tick(now, state, flag);
			switch (loopAction)
			{
			case LoopAction.StartTyping:
				StartTyping(now, "loop_entry");
				break;
			default:
				if (!loopUi.Execute(loopAction, client.TerritoryType))
				{
					loop.Stop(error: true);
				}
				break;
			case LoopAction.None:
				break;
			}
			phase = loop.Phase;
			if ((phase == LoopPhase.Complete || phase == LoopPhase.Error) ? true : false)
			{
				automation.Suppress();
				typing.Stop((loop.Phase == LoopPhase.Complete) ? "Completed" : "Fault");
				backgroundKeys.Release(force: true);
            Keyboard.Release(force: true);
			}
		}
	}

	private void StopAutoQueue()
	{
		config.AutoQueueOnLobbyEntry = false;
		settings.Update(config);
		autoQueuePaused = false;
		autoQueueVisitArmed = false;
		if (autoQueueLoop)
		{
			loop.Stop();
			autoQueueLoop = false;
		}
	}

	private void StartDiagnostics()
	{
		SetDiagnostics(enabled: true);
		diagnostics.Stop();
		diagnosticReader.Reset();
		diagnosticAttempted = IsContent();
		if (diagnosticAttempted)
		{
			diagnostics.Start();
		}
	}

	private void SetDiagnostics(bool enabled)
	{
		if (config.RecordDiagnostics != enabled)
		{
			config.RecordDiagnostics = enabled;
			settings.Update(config);
		}
		diagnosticAttempted = false;
		diagnosticReader.Reset();
		if (!enabled)
		{
			diagnostics.Stop();
		}
	}

	private void UpdateDiagnostics()
	{
		if (!config.RecordDiagnostics)
		{
			return;
		}
		if (!IsContent())
		{
			diagnostics.Stop();
			diagnosticAttempted = false;
			return;
		}
		if (!diagnosticAttempted)
		{
			diagnosticAttempted = true;
			diagnosticReader.Reset();
			diagnostics.Start();
		}
		long tickCount = Environment.TickCount64;
		if (diagnostics.Active && tickCount - lastDiagnosticSample >= 100)
		{
			lastDiagnosticSample = tickCount;
			string text = diagnosticReader.Capture(typing, ime.Inspect(), encounter.Paused(tickCount), new
			{
				gate = controlGate,
				activityFailed = activity.Failed,
                backgroundInput = config.BackgroundInput,
                backgroundPostError = backgroundKeys.LastError,
                rewards = rewards.Status,
				loopPhase = loop.Phase.ToString(),
				Current = loop.Current,
				Total = loop.Total,
				Completed = loop.Completed,
				currentEntity = currentEntity,
				betweenWordsMs = config.BetweenWordsMs,
				FocusState = reader.FocusState,
				FocusAttempts = reader.FocusAttempts,
				FocusRequestAccepted = reader.FocusRequestAccepted,
				uiCaptures = uiCaptures,
				autoStart = config.AutoStart,
				autoConsumed = automation.Consumed
			});
			if (text != null)
			{
				diagnostics.Observe(text);
			}
		}
		diagnostics.Tick();
	}

	private void DrawDiagnostics()
	{
		ImGui.TextWrapped(T("DiagHelp"));
		ImGui.TextUnformatted(T(config.RecordDiagnostics ? diagnostics.State : "DiagDisabled"));
		ImGui.TextWrapped(diagnostics.DirectoryPath);
		if (ImGui.Button(T("DiagStart")))
		{
			StartDiagnostics();
		}
		ImGui.SameLine();
		if (ImGui.Button(T("DiagStop")))
		{
			SetDiagnostics(enabled: false);
		}
		if (ImGui.Button(T("DiagCopy")))
		{
			ImGui.SetClipboardText(diagnostics.DirectoryPath);
		}
		if (ImGui.Button(T("DiagBlock")))
		{
			diagnostics.Mark("block");
		}
		ImGui.SameLine();
		if (ImGui.Button(T("DiagCast")))
		{
			diagnostics.Mark("cast");
		}
		ImGui.TextUnformatted(T("DiagRecords") + ": " + diagnostics.Records + " / " + diagnostics.BytesWritten + " B");
	}

	private void Draw()
	{
		uiCaptures = ImGui.GetIO().WantTextInput || ImGui.IsAnyItemActive();
		if (stopping || !open)
		{
			return;
		}
		fonts.Ensure(reader.Words.Append(typing.CurrentWord));
		using (fonts.Push(config.Language))
		{
			CyberpunkTheme.Push();
			try
			{
				ImGui.SetNextWindowSize(new Vector2(650f, 440f), ImGuiCond.FirstUseEver);
				if (ImGui.Begin("Tataru Brawler###TataruBrawler", ref open))
				{
					if (!fonts.Available)
					{
						ImGui.TextWrapped(Loc.Get("en", fonts.Failed ? "FontFailed" : "FontLoading"));
						ImGui.End();
						return;
					}
					using (pi.UiBuilder.IconFontHandle.Push())
                        ImGui.TextUnformatted(FontAwesomeIcon.Keyboard.ToIconString());
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.15f, 0.95f, 0.95f, 1f), "TATARU // BRAWLER");
					ImGui.SameLine();
					ImGui.TextDisabled("  KEYBOUND SYSTEM");
					ImGui.Separator();
					if (ImGui.BeginTabBar("Tabs"))
					{
						if (ImGui.BeginTabItem(T("Main") + "###Main"))
						{
							ImGui.TextUnformatted(T("Status") + ": " + T(typing.Status));
							if (activity.Failed)
							{
								ImGui.TextWrapped(T("ActivityUnavailable"));
							}
							ImGui.TextUnformatted(T("Words") + ": " + reader.Words.Count);
                            ImGui.Separator();
                            ImGui.TextUnformatted($"Session: {rewards.Session.Games:N0} games | {rewards.Session.Mgp:N0} MGP | {rewards.Session.Mgc:N0} MGC");
                            ImGui.TextUnformatted($"Saved total: {rewards.Total.Games:N0} games | {rewards.Total.Mgp:N0} MGP | {rewards.Total.Mgc:N0} MGC");
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Session starts when the plugin loads. Saved totals cover all characters on this installation. Currency tracks observed gains; capped rewards cannot be measured.");
                            ImGui.TextDisabled(rewards.Status);
                            if (rewards.Error != null) ImGui.TextWrapped(rewards.Error);
                            ImGui.Separator();
							ImGui.TextUnformatted(T("InputVisible") + ": " + T(reader.InputVisible ? "Yes" : "No"));
							ImGui.TextUnformatted(T("InputFocused") + ": " + T(reader.InputFocused ? "Yes" : "No"));
							ImGui.TextUnformatted(T("Sent") + ": " + typing.Sent);
							ImGui.TextWrapped(T("Current") + ": " + typing.CurrentWord);
							ImGui.BeginDisabled(loop.Active);
							int loopCount = config.LoopCount;
							ImGui.SetNextItemWidth(160f);
							if (ImGui.InputInt(T("LoopCount") + "###LoopCount", ref loopCount))
							{
								config.LoopCount = Math.Clamp(loopCount, 1, 9999);
								settings.Update(config);
							}
							ImGui.EndDisabled();
							if (loop.Unlimited)
							{
								ImU8String text = new ImU8String(55, 2);
								text.AppendLiteral("Auto queue: ");
								text.AppendFormatted(loop.Completed);
								text.AppendLiteral(" completed; repeats until Stop auto queue. ");
								text.AppendFormatted(T(loop.Paused ? "UserPaused" : ("Loop" + loop.Phase)));
								ImGui.TextWrapped(text);
							}
							else if (loop.Total > 0)
							{
								ImGui.TextWrapped(string.Format(T("LoopProgress"), loop.Current, loop.Total, loop.Completed) + " / " + T(loop.Paused ? "UserPaused" : ("Loop" + loop.Phase)));
							}
							PlaybackControls.Draw(config.AutoStart || loop.Active, loop.Active ? loop.Paused : typing.ManualPaused, T, () =>
							{
								if (loop.Active)
								{
									loop.Resume();
									typing.ResumeByUser();
									reader.ResetFocus();
								}
								else
								{
									StartTyping(Environment.TickCount64, "button");
								}
							}, () =>
							{
								loop.Pause();
								typing.PauseByUser();
								controlGate = "ManualPause";
								backgroundKeys.Release(force: true);
            Keyboard.Release(force: true);
							}, StopByUser, typing.Running || loop.Active);
							ImGui.SameLine();
							bool flag = !loop.Active && loopUi.Supported && client.IsLoggedIn;
							if (flag)
							{
								uint territoryType = client.TerritoryType;
								bool flag2 = territoryType - 1359 <= 1;
								flag = flag2;
							}
							PlaybackControls.DrawLoop(flag, T("LoopStart"), StartLoop);
							ImGui.SameLine();
							if (ImGui.Button(T("Refresh") + "###Inspect"))
							{
								try
								{
									reader.Refresh();
								}
								catch (Exception exception)
								{
									typing.Stop("Fault");
									log.Error(exception, "Manual word inspection failed");
								}
							}
							ImGui.TextWrapped(T("Guide"));
							ImGui.TextWrapped(T("LoopHelp"));
							ImGui.Separator();
							bool v = config.AutoQueueOnLobbyEntry;
							if (ImGui.Checkbox("Auto queue on lobby entry###AutoQueueOnLobbyEntry", ref v))
							{
								config.AutoQueueOnLobbyEntry = v;
								settings.Update(config);
								if (!v)
								{
									StopAutoQueue();
								}
								else if (client.IsLoggedIn && client.TerritoryType == 1360 && !loop.Active)
								{
									autoQueueVisitArmed = false;
									StartLoop(automatic: true);
								}
							}
							ImGui.TextWrapped("Auto queue waits until you are near the Keybound Registrar, then repeats until you press Stop auto queue. Number of runs applies only to the manual loop.");
							ImGui.BeginDisabled(!autoQueueLoop || !loop.Active);
							if (ImGui.Button((autoQueuePaused ? "Resume auto queue" : "Pause auto queue") + "###AutoQueuePause"))
							{
								autoQueuePaused = !autoQueuePaused;
							}
							ImGui.EndDisabled();
							ImGui.SameLine();
							if (ImGui.Button("Stop auto queue###AutoQueueStop"))
							{
								StopAutoQueue();
							}
							ImGui.TextWrapped("Pause holds new registration steps. Stop prevents further automatic queue actions; an existing duty queue is not cancelled. Play stays on.");
							foreach (string word in reader.Words)
							{
								ImGui.TextUnformatted(word);
							}
							ImGui.EndTabItem();
						}
						if (ImGui.BeginTabItem(T("Diagnostics") + "###Diagnostics"))
						{
							DrawDiagnostics();
							ImGui.EndTabItem();
						}
						StandardPluginTabs.DrawTrailingTabs(T("Settings"), DrawSettings, T("Language"), DrawLanguage);
						ImGui.EndTabBar();
					}
				}
				ImGui.End();
			}
			finally
			{
				CyberpunkTheme.Pop();
			}
		}
	}

	private void DrawSettings()
    {
        bool showServerBar = config.ShowServerBarButton;
        if (ImGui.Checkbox("Show button in the server info bar", ref showServerBar))
        {
            config.ShowServerBarButton = showServerBar;
            serverBarEntry.Shown = showServerBar;
            settings.Update(config);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Show KB in the server info bar. Click it to open or close this window.");
		bool v = config.BackgroundInput;
		if (ImGui.Checkbox(T("BackgroundInput") + "###BackgroundInput", ref v))
		{
			config.BackgroundInput = v;
			settings.Update(config);
		}
		ImGui.TextWrapped(T("BackgroundInputHelp"));
		int num = config.MinimumMs;
		int num2 = config.MaximumMs;
		ImGui.SetNextItemWidth(200f);
		bool flag = ImGui.InputInt(T("Min") + "###Minimum", ref num, 5, 20);
		if (flag)
		{
			num = Math.Clamp(num, 20, 5000);
			num2 = Math.Max(num, num2);
		}
		ImGui.SetNextItemWidth(200f);
		bool flag2 = ImGui.InputInt(T("Max") + "###Maximum", ref num2, 5, 20);
		if (flag2)
		{
			num2 = Math.Clamp(num2, 20, 5000);
			num = Math.Min(num, num2);
		}
		if (flag | flag2)
		{
			config.SetRange(num, num2);
			settings.Update(config);
		}
		ImGui.TextWrapped(T("RangeHelp"));
		int betweenWordsMs = config.BetweenWordsMs;
		ImGui.SetNextItemWidth(200f);
		if (ImGui.InputInt(T("BetweenWordsMs") + "###BetweenWordsMs", ref betweenWordsMs, 50, 250))
		{
			config.BetweenWordsMs = Math.Clamp(betweenWordsMs, 0, 60000);
			settings.Update(config);
		}
		ImGui.TextWrapped(T("BetweenWordsHelp"));
		if (settings.Recovered)
		{
			ImGui.TextWrapped(T("Recovered"));
		}
		if (settings.LoadWarning != null || settings.LastError != null || settings.Stopped)
		{
			ImGui.TextWrapped(T("Persistence"));
			if (ImGui.Button(T("Retry")))
			{
				settings.Retry();
			}
		}
	}

	private void DrawLanguage()
	{
		string text = StandardPluginTabs.DrawLanguageSelector(T("Language"), T("LanguageHelp"), config.Language ?? "en");
		if (text != null)
		{
			config.Language = text;
			settings.Update(config);
			command.HelpMessage = T("CommandHelp");
		}
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			serverBarEntry.Remove();
			Stop();
			framework.Update -= Update;
			pi.UiBuilder.Draw -= Draw;
			pi.UiBuilder.OpenMainUi -= Open;
			pi.UiBuilder.OpenConfigUi -= Open;
			client.Logout -= Logout;
			commands.RemoveHandler("/tatarubrawler");
			shutdown.Dispose();
			unload.Dispose();
			settings.Dispose();
			fonts.Dispose();
			reader.Clear();
		}
	}
}

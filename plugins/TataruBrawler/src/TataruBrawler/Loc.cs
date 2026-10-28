using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game;

namespace TataruBrawler;

internal static class Loc
{
	public static readonly string[] Codes = new string[7] { "ja", "en", "de", "fr", "ko", "zh-Hans", "zh-Hant" };

	private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
	{
		["ActivityUnavailable"] = new string[7] { "操作状態の更新に失敗しました。再開時に再試行します。", "Activity update failed. Restart to retry.", "Aktivitätsaktualisierung fehlgeschlagen. Zum Wiederholen neu starten.", "Échec de mise à jour de l’activité. Redémarrez pour réessayer.", "활동 상태 갱신 실패. 다시 시작하면 재시도합니다.", "操作状态更新失败。重新开始时重试。", "操作狀態更新失敗。重新開始時重試。" },
		["LoopError"] = new string[7] { "周回停止：申請・画面・入力状態を確認してください", "Loop stopped: check queue, screen and input state", "Wiederholung gestoppt: Anmeldung, Bildschirm und Eingabe prüfen", "Boucle arrêtée : vérifiez l’inscription, l’écran et la saisie", "반복 중지: 신청, 화면, 입력 상태를 확인하세요", "循环停止：请检查报名、界面及输入状态", "循環停止：請檢查報名、介面及輸入狀態" },
		["LoopStopped"] = new string[7] { "周回停止", "Loop stopped", "Wiederholung gestoppt", "Boucle arrêtée", "반복 중지", "循环已停止", "循環已停止" },
		["LoopComplete"] = new string[7] { "周回完了", "All runs complete", "Alle Durchläufe abgeschlossen", "Toutes les parties terminées", "반복 완료", "循环完成", "循環完成" },
		["LoopLeaving"] = new string[7] { "退出待ち", "Waiting to leave", "Warten auf Verlassen", "Sortie en attente", "퇴장 대기", "等待退出", "等待退出" },
		["LoopPlaying"] = new string[7] { "プレイ中", "Playing", "Im Spiel", "Partie en cours", "플레이 중", "游玩中", "遊玩中" },
		["LoopWaiting"] = new string[7] { "参加待ち", "Waiting for entry", "Warten auf Eintritt", "En attente d’entrée", "입장 대기", "等待入场", "等待入場" },
		["LoopRequesting"] = new string[7] { "参加申請の準備", "Preparing queue", "Anmeldung vorbereiten", "Préparation de l’inscription", "신청 준비", "准备报名", "準備報名" },
		["LoopOpeningRegistrar"] = new string[7] { "受付画面を開く", "Opening Registrar", "Anmeldung wird geöffnet", "Ouverture de l'inscription", "등록 창 열기", "正在打开报名窗口", "正在開啟報名視窗" },
		["LoopIdle"] = new string[7] { "待機", "Idle", "Bereit", "En attente", "대기", "待机", "待命" },
		["LoopHelp"] = new string[7] { "周回数を指定し、循環矢印で申請から自動実行。スクウェアで開始してください。停止しても申請済みの参加申請は取り消しません。", "Set the run count, then use the loop arrows in the Square to queue and play. Stopping does not cancel an existing queue.", "Anzahl wählen und im Square mit den Kreispfeilen anmelden und spielen. Stopp bricht eine laufende Anmeldung nicht ab.", "Choisissez le nombre de parties puis les flèches en boucle dans le Square. L’arrêt n’annule pas une inscription en cours.", "스퀘어에서 횟수를 정하고 순환 화살표로 신청과 플레이를 시작하세요. 중지해도 기존 참가 신청은 취소되지 않습니다.", "在广场设置次数后点击循环箭头，自动报名并游玩。停止不会取消已有的报名。", "在廣場設定次數後點擊循環箭頭，自動報名並遊玩。停止不會取消已有的報名。" },
		["LoopStart"] = new string[7] { "周回開始", "Start loop", "Wiederholung starten", "Démarrer la boucle", "반복 시작", "开始循环", "開始循環" },
		["LoopProgress"] = new string[7] { "{0} / {1} 周目（完了 {2} 回）", "Run {0} / {1} ({2} completed)", "Durchlauf {0} / {1} ({2} abgeschlossen)", "Partie {0} / {1} ({2} terminées)", "{0} / {1}회차 ({2}회 완료)", "第 {0} / {1} 轮（已完成 {2} 轮）", "第 {0} / {1} 輪（已完成 {2} 輪）" },
		["LoopCount"] = new string[7] { "周回数", "Number of runs", "Anzahl der Durchläufe", "Nombre de parties", "반복 횟수", "循环次数", "循環次數" },
		["DiagDisabled"] = new string[7] { "ログ作成はオフです", "Logging is off", "Protokollierung ist aus", "Journalisation désactivée", "로그 생성 꺼짐", "日志记录已关闭", "日誌記錄已關閉" },
		["Main"] = new string[7] { "自動入力", "Auto typing", "Automatische Eingabe", "Saisie automatique", "자동 입력", "自动输入", "自動輸入" },
		["Settings"] = new string[7] { "設定", "Settings", "Einstellungen", "Paramètres", "설정", "设置", "設定" },
		["Language"] = new string[7] { "言語設定", "Language settings", "Spracheinstellungen", "Langue", "언어 설정", "语言设置", "語言設定" },
		["LanguageHelp"] = new string[7] { "表示言語を変更します。", "Change the display language.", "Anzeigesprache ändern.", "Modifier la langue d’affichage.", "표시 언어를 변경합니다.", "更改显示语言。", "變更顯示語言。" },
		["Min"] = new string[7] { "入力間隔の下限 (ms)", "Minimum interval (ms)", "Mindestintervall (ms)", "Intervalle minimal (ms)", "최소 입력 간격 (ms)", "最小输入间隔 (ms)", "最小輸入間隔 (ms)" },
		["Max"] = new string[7] { "入力間隔の上限 (ms)", "Maximum interval (ms)", "Höchstintervall (ms)", "Intervalle maximal (ms)", "최대 입력 간격 (ms)", "最大输入间隔 (ms)", "最大輸入間隔 (ms)" },
		["RangeHelp"] = new string[7] { "1文字ごとに範囲内の間隔を抽選します。同じ値なら固定です。20〜5000ms。実際の間隔はフレーム遅延で長くなる場合があります。", "A new interval is sampled for each character. Equal limits give a fixed interval. Range: 20–5000 ms. Frame delays can increase actual intervals.", "Pro Zeichen wird ein Intervall gewählt. Gleiche Grenzen ergeben ein festes Intervall. Bereich: 20–5000 ms. Bildverzögerungen können das Intervall verlängern.", "Un intervalle est tiré par caractère. Des limites égales donnent un intervalle fixe. Plage : 20–5000 ms. Les retards d’affichage peuvent l’allonger.", "문자마다 범위 내 간격을 정합니다. 두 값이 같으면 고정입니다. 범위: 20–5000ms. 프레임 지연으로 실제 간격이 길어질 수 있습니다.", "每个字符随机选择间隔。上下限相同则为固定间隔。范围：20–5000毫秒。帧延迟可能延长实际间隔。", "每個字元隨機選擇間隔。上下限相同則為固定間隔。範圍：20–5000毫秒。影格延遲可能延長實際間隔。" },
		["Start"] = new string[7] { "開始", "Start", "Starten", "Démarrer", "시작", "开始", "開始" },
		["Stop"] = new string[7] { "停止", "Stop", "Stoppen", "Arrêter", "중지", "停止", "停止" },
		["BetweenWordsMs"] = new string[7] { "送信後の待ち時間 (ms)", "Delay after submission (ms)", "Wartezeit nach Senden (ms)", "Délai après envoi (ms)", "전송 후 대기 시간 (ms)", "提交后的等待时间 (ms)", "提交後的等待時間 (ms)" },
		["BetweenWordsHelp"] = new string[7] { "0〜60000ms。回答送信から次の入力開始までの待ち時間。キー間隔は別途適用します。", "0–60000 ms from answer submission to starting the next answer. Per-key delay also applies.", "0–60000 ms vom Absenden bis zum Beginn der nächsten Antwort. Tastenabstand gilt zusätzlich.", "0–60000 ms entre l’envoi et le début de la réponse suivante. Le délai par touche s’ajoute.", "답변 전송부터 다음 입력 시작까지 0–60000ms. 키 입력 간격은 별도로 적용됩니다.", "提交答案到开始下一题之间等待0–60000毫秒，另加按键间隔。", "提交答案到開始下一題之間等待0–60000毫秒，另加按鍵間隔。" },
		["BetweenWords"] = new string[7] { "次の入力まで待機中", "Waiting before next answer", "Warten auf nächste Antwort", "Attente avant la réponse suivante", "다음 입력까지 대기 중", "等待输入下一题", "等待輸入下一題" },
		["Guide"] = new string[7] { "▶開始／再開、Ⅱ一時停止、■停止。フェーズ移行やフォーカス喪失中は待機し、コンテンツ終了時に停止します。", "Play starts/resumes, Pause holds, Stop ends. Phase changes and lost focus wait; content completion stops.", "Start/Fortsetzen, Pause, Stopp. Bei Phasenwechsel oder Fokusverlust warten; am Inhaltsende stoppen.", "Lecture : démarrer/reprendre ; Pause ; Arrêt. Attente aux changements de phase ou perte de focus ; arrêt à la fin.", "재생: 시작/재개, 일시 정지, 중지. 단계 전환과 포커스 상실 시 대기하며 콘텐츠 종료 시 중지합니다.", "播放用于开始/继续，暂停保留进度，停止结束。阶段切换或失去焦点时等待，内容结束时停止。", "播放用於開始/繼續，暫停保留進度，停止結束。階段切換或失去焦點時等待，內容結束時停止。" },
		["Status"] = new string[7] { "状態", "Status", "Status", "État", "상태", "状态", "狀態" },
		["Stopped"] = new string[7] { "停止中", "Stopped", "Gestoppt", "Arrêté", "중지됨", "已停止", "已停止" },
		["Arming"] = new string[7] { "開始待ち（3秒）", "Arming (3 s)", "Start in 3 s", "Démarrage dans 3 s", "시작 대기 (3초)", "等待开始（3秒）", "等待開始（3秒）" },
		["Waiting"] = new string[7] { "出題の更新待ち", "Waiting for a new word", "Warte auf neues Wort", "En attente d’un mot", "새 단어 대기", "等待新题目", "等待新題目" },
		["Typing"] = new string[7] { "入力中", "Typing", "Eingabe läuft", "Saisie en cours", "입력 중", "正在输入", "正在輸入" },
		["FocusLost"] = new string[7] { "対象画面・フォーカスを確認できないため停止", "Stopped: game screen or input is not focused", "Gestoppt: Spielbildschirm oder Eingabefokus fehlt", "Arrêt : écran du jeu ou focus absent", "대상 화면 또는 포커스를 확인할 수 없어 중지", "无法确认目标界面或焦点，已停止", "無法確認目標介面或焦點，已停止" },
		["NoProgress"] = new string[7] { "出題が更新されないため停止", "Stopped: word did not change", "Gestoppt: Wort unverändert", "Arrêt : le mot n’a pas changé", "단어가 갱신되지 않아 중지", "题目未更新，已停止", "題目未更新，已停止" },
		["Unsupported"] = new string[7] { "未対応の文字のため停止", "Stopped: unsupported characters", "Gestoppt: nicht unterstützte Zeichen", "Arrêt : caractères non pris en charge", "지원하지 않는 문자로 중지", "存在不支持的字符，已停止", "存在不支援的字元，已停止" },
		["SendFailed"] = new string[7] { "キーを送信できないため停止（自動再送なし）", "Stopped: key delivery failed (no automatic retry)", "Gestoppt: Tastenübertragung fehlgeschlagen (keine Wiederholung)", "Arrêt : échec d’envoi de touche (sans nouvel essai)", "키 전송 실패로 중지 (자동 재시도 없음)", "按键发送失败，已停止（不自动重试）", "按鍵傳送失敗，已停止（不自動重試）" },
		["Fault"] = new string[7] { "読み取りエラーのため停止", "Stopped: read error", "Gestoppt: Lesefehler", "Arrêt : erreur de lecture", "읽기 오류로 중지", "读取错误，已停止", "讀取錯誤，已停止" },
		["Words"] = new string[7] { "検出した出題数", "Detected word count", "Erkannte Wörter", "Nombre de mots détectés", "감지된 단어 수", "检测到的题目数", "偵測到的題目數" },
		["Current"] = new string[7] { "入力対象", "Current word", "Aktuelles Wort", "Mot actuel", "입력 대상", "当前题目", "目前題目" },
		["Sent"] = new string[7] { "送信文字数", "Characters sent", "Gesendete Zeichen", "Caractères envoyés", "전송한 문자 수", "已发送字符数", "已傳送字元數" },
		["Refresh"] = new string[7] { "表示文字を確認", "Inspect displayed words", "Angezeigte Wörter prüfen", "Inspecter les mots affichés", "표시 단어 확인", "检查显示文字", "檢查顯示文字" },
		["Persistence"] = new string[7] { "設定保存の問題があります。設定を確認して再試行してください。", "Configuration storage needs attention. Check settings and retry.", "Konfigurationsspeicher prüfen und erneut versuchen.", "Vérifiez le stockage des paramètres et réessayez.", "설정 저장을 확인한 후 다시 시도하세요.", "设置存储存在问题，请检查后重试。", "設定儲存存在問題，請檢查後重試。" },
		["Recovered"] = new string[7] { "バックアップから設定を復元しました。", "Settings restored from backup.", "Einstellungen aus Sicherung wiederhergestellt.", "Paramètres restaurés depuis une sauvegarde.", "백업에서 설정을 복원했습니다.", "已从备份恢复设置。", "已從備份恢復設定。" },
		["Retry"] = new string[7] { "設定保存を再試行", "Retry saving settings", "Speichern erneut versuchen", "Réessayer l’enregistrement", "설정 저장 재시도", "重试保存设置", "重試儲存設定" },
		["CommandHelp"] = new string[7] { "Tataru Brawler の設定を開きます。", "Open Tataru Brawler settings.", "Tataru-Brawler-Einstellungen öffnen.", "Ouvrir les paramètres de Tataru Brawler.", "Tataru Brawler 설정 열기.", "打开 Tataru Brawler 设置。", "開啟 Tataru Brawler 設定。" },
		["InputVisible"] = new string[7] { "専用入力欄の表示", "Dedicated input visible", "Spieleingabe sichtbar", "Champ du jeu visible", "전용 입력란 표시", "专用输入框可见", "專用輸入框可見" },
		["InputFocused"] = new string[7] { "専用入力欄のフォーカス", "Dedicated input focused", "Spieleingabe fokussiert", "Champ du jeu actif", "전용 입력란 포커스", "专用输入框获得焦点", "專用輸入框取得焦點" },
		["Yes"] = new string[7] { "はい", "Yes", "Ja", "Oui", "예", "是", "是" },
		["No"] = new string[7] { "いいえ", "No", "Nein", "Non", "아니요", "否", "否" },
		["Diagnostics"] = new string[7] { "診断ログ", "Diagnostics", "Diagnose", "Diagnostic", "진단 로그", "诊断日志", "診斷日誌" },
		["DiagHelp"] = new string[7] { "ログは初期値オフです。必要なときだけ設定または記録開始で有効にしてください。コンテンツ内の状態を記録します。チャット・プレイヤー名・入力文字列・生のキーは保存しません。上限は120分、1回64MiB、合計256MiBです。", "Logs are off by default. Enable in Settings or start recording when needed. Records content state, not chat, player names, typed text or raw keys. Limits: 120 minutes, 64 MiB per session, 256 MiB total.", "Protokolle sind standardmäßig aus. Bei Bedarf in Einstellungen oder über Aufnahme starten aktivieren. Nur Inhaltszustand, keine Chats, Spielernamen, Eingabetexte oder rohen Tasten. Grenzen: 120 Minuten, 64 MiB pro Sitzung, insgesamt 256 MiB.", "Les journaux sont désactivés par défaut. Activez-les dans les paramètres ou démarrez l’enregistrement au besoin. État du contenu uniquement, sans chat, noms, texte saisi ni touches brutes. Limites : 120 minutes, 64 Mio par session, 256 Mio au total.", "로그는 기본적으로 꺼져 있습니다. 필요할 때 설정 또는 기록 시작으로 켜세요. 콘텐츠 상태만 기록하며 채팅, 플레이어 이름, 입력 문자열, 원시 키는 저장하지 않습니다. 한도: 120분, 세션당 64MiB, 총 256MiB.", "日志默认关闭。需要时在设置中启用或开始记录。记录副本状态，不保存聊天、玩家名、输入文本或原始按键。上限：120分钟、每次64MiB、总计256MiB。", "日誌預設關閉。需要時在設定中啟用或開始記錄。記錄副本狀態，不儲存聊天、玩家名、輸入文字或原始按鍵。上限：120分鐘、每次64MiB、總計256MiB。" },
		["DiagIdle"] = new string[7] { "記録待機中", "Recording idle", "Aufnahme inaktiv", "En attente", "기록 대기", "等待记录", "等待記錄" },
		["DiagRecording"] = new string[7] { "記録中", "Recording", "Aufzeichnung läuft", "Enregistrement", "기록 중", "正在记录", "正在記錄" },
		["DiagLimit"] = new string[7] { "記録上限に到達したため停止", "Stopped at recording limit", "Aufnahmegrenze erreicht", "Limite atteinte", "기록 한도에 도달하여 중지", "达到记录上限，已停止", "達到記錄上限，已停止" },
		["DiagError"] = new string[7] { "記録に失敗しました。自動再送は行いません。", "Recording failed; no automatic replay.", "Aufnahme fehlgeschlagen; keine automatische Wiederholung.", "Échec d’enregistrement ; aucune réécriture automatique.", "기록 실패. 자동 재전송하지 않습니다.", "记录失败，不自动重写。", "記錄失敗，不自動重寫。" },
		["DiagStart"] = new string[7] { "記録開始／再開", "Start / restart recording", "Aufnahme starten / neu starten", "Démarrer / redémarrer", "기록 시작 / 재시작", "开始／重新记录", "開始／重新記錄" },
		["DiagStop"] = new string[7] { "記録を停止", "Stop recording", "Aufnahme stoppen", "Arrêter", "기록 중지", "停止记录", "停止記錄" },
		["DiagCopy"] = new string[7] { "保存先をコピー", "Copy log directory", "Protokollpfad kopieren", "Copier le dossier", "저장 경로 복사", "复制保存路径", "複製儲存路徑" },
		["DiagBlock"] = new string[7] { "入力禁止を見た時の目印", "Mark typing prohibition", "Eingabeverbot markieren", "Marquer une interdiction", "입력 금지 시점 표시", "标记禁止输入时刻", "標記禁止輸入時刻" },
		["DiagCast"] = new string[7] { "詠唱を見た時の目印", "Mark enemy cast", "Gegnerzauber markieren", "Marquer une incantation", "적 시전 시점 표시", "标记敌人咏唱", "標記敵人詠唱" },
		["DiagRecords"] = new string[7] { "記録件数／書込バイト", "Records / written bytes", "Einträge / geschriebene Bytes", "Entrées / octets écrits", "기록 수 / 기록 바이트", "记录数／写入字节", "記錄數／寫入位元組" },
		["Paused"] = new string[7] { "一時待機中（入力禁止・入力欄の準備）", "Waiting (input prohibition or input preparation)", "Wartezeit (Eingabesperre oder Vorbereitung)", "En attente (interdiction ou préparation)", "일시 대기 (입력 금지 또는 준비)", "暂时等待（禁止输入或输入准备）", "暫時等待（禁止輸入或輸入準備）" },
		["Confirming"] = new string[7] { "ひらがなを確定中", "Confirming hiragana", "Hiragana bestätigen", "Confirmation des hiragana", "히라가나 확정 중", "正在确认平假名", "正在確認平假名" },
		["ImeFailed"] = new string[7] { "IME・確定した読みを確認できないため停止。日本語のひらがな入力を確認してください。", "Stopped: IME or committed reading could not be verified. Check Japanese hiragana input.", "Gestoppt: IME oder bestätigte Lesung nicht verifiziert. Japanische Hiragana-Eingabe prüfen.", "Arrêt : IME ou lecture validée non vérifiée. Vérifiez la saisie japonaise en hiragana.", "IME 또는 확정된 읽기를 확인할 수 없어 중지했습니다. 일본어 히라가나 입력을 확인하세요.", "无法确认IME或已确定的读音，已停止。请检查日语平假名输入。", "無法確認IME或已確定的讀音，已停止。請檢查日語平假名輸入。" },
		["TargetChanged"] = new string[7] { "入力途中で対象が変わったため停止。残った文字を消して再開してください。", "Stopped: target changed during typing. Clear remaining input before restarting.", "Gestoppt: Ziel während der Eingabe geändert. Resttext vor Neustart löschen.", "Arrêt : cible modifiée pendant la saisie. Effacez le texte restant avant de reprendre.", "입력 중 대상이 변경되어 중지했습니다. 남은 입력을 지우고 다시 시작하세요.", "输入途中目标变化，已停止。请清空剩余文字后重新开始。", "輸入途中目標變化，已停止。請清空剩餘文字後重新開始。" },
		["Resume"] = new string[7] { "再開", "Resume", "Fortsetzen", "Reprendre", "재개", "继续", "繼續" },
		["BackgroundInput"] = new string[7] { "バックグラウンドで英語の入力を試す（実験的）", "Type English while FFXIV is unfocused", "Englische Eingabe im Hintergrund versuchen (experimentell)", "Essayer la saisie anglaise en arrière-plan (expérimental)", "백그라운드에서 영어 입력 시도 (실험적)", "尝试在后台输入英文（实验性）", "嘗試在背景輸入英文（實驗性）" },
		["BackgroundInputHelp"] = new string[7] { "ゲーム専用の入力欄を使います。他のアプリには入力しません。日本語の問題はゲームにフォーカスするまで待機します。", "Sends key messages only to FFXIV's own window and dedicated input field, without activating it. Japanese prompts wait for game focus.", "Verwendet nur das Eingabefeld des Spiels. Andere Apps bleiben unberührt. Japanische Texte warten auf den Spielfokus.", "Utilise uniquement le champ du jeu. Aucune saisie dans les autres applications. Les textes japonais attendent le focus du jeu.", "게임 전용 입력란만 사용합니다. 다른 앱에는 입력하지 않습니다. 일본어 문제는 게임에 초점이 돌아올 때까지 기다립니다.", "仅使用游戏专用输入框，不会输入到其他应用。日文题目会等待游戏获得焦点。", "僅使用遊戲專用輸入框，不會輸入到其他應用。日文題目會等待遊戲取得焦點。" },
		["FontLoading"] = new string[7] { "フォントを準備しています。", "Loading UI fonts...", "UI-Schriften werden geladen...", "Chargement des polices...", "UI 글꼴을 준비하고 있습니다.", "正在准备界面字体。", "正在準備介面字型。" },
		["FontFailed"] = new string[7] { "フォントの読込に失敗しました。Dalamudログを確認してください。", "UI font loading failed. Check the Dalamud log.", "UI-Schriften konnten nicht geladen werden. Dalamud-Protokoll prüfen.", "Échec du chargement des polices. Consultez le journal Dalamud.", "UI 글꼴 로드에 실패했습니다. Dalamud 로그를 확인하세요.", "界面字体加载失败，请查看Dalamud日志。", "介面字型載入失敗，請查看Dalamud日誌。" },
		["Pause"] = new string[7] { "一時停止", "Pause", "Pausieren", "Pause", "일시 정지", "暂停", "暫停" },
		["UserPaused"] = new string[7] { "一時停止中", "Paused by user", "Manuell pausiert", "Pause manuelle", "수동 일시 정지", "已手动暂停", "已手動暫停" },
		["Clearing"] = new string[7] { "前の入力を消去中", "Clearing previous input", "Vorherige Eingabe löschen", "Effacement de la saisie précédente", "이전 입력 지우는 중", "正在清除之前的输入", "正在清除先前的輸入" },
		["Completed"] = new string[7] { "今回のプレイ終了。次のゲームを待機中", "Round finished; waiting for the next game", "Runde beendet; warte auf das nächste Spiel", "Partie terminée ; en attente de la suivante", "라운드 종료; 다음 게임 대기 중", "本局结束，等待下一局", "本局結束，等待下一局" }
	};

	public static string FontText => string.Concat(string.Concat(Text.Values.SelectMany((string[] x) => x)), string.Concat(Codes), "日本語EnglishDeutschFrançais한국어简体中文繁體中文");

	public static bool IsSupported(string? value)
	{
		return Array.IndexOf(Codes, value) >= 0;
	}

	public static string Get(string? code, string key)
	{
		if (!Text.TryGetValue(key, out string[] value))
		{
			return key;
		}
		return value[(!IsSupported(code)) ? 1 : Array.IndexOf(Codes, code)];
	}

	public static void CheckCompleteness()
	{
		if (Text.Values.Any((string[] v) => v.Length != 7 || v.Any(string.IsNullOrWhiteSpace)))
		{
			throw new InvalidDataException("Incomplete localization");
		}
	}

	public static string? FromGame(ClientLanguage value)
	{
		return value switch
		{
			ClientLanguage.Japanese => "ja", 
			ClientLanguage.English => "en", 
			ClientLanguage.German => "de", 
			ClientLanguage.French => "fr", 
			_ => null, 
		};
	}

	public static string? Normalize(string? value)
	{
		string text = value?.Trim().Replace('_', '-').ToLowerInvariant();
		if (text != null)
		{
			int length = text.Length;
			if (length != 2)
			{
				if (length != 5)
				{
					if (length == 7)
					{
						char c = text[6];
						if (c != 's')
						{
							if (c == 't' && text == "zh-hant")
							{
								goto IL_027a;
							}
						}
						else if (text == "zh-hans")
						{
							goto IL_0272;
						}
					}
				}
				else
				{
					switch (text[3])
					{
					case 'j':
						break;
					case 'u':
						goto IL_016f;
					case 'g':
						goto IL_0184;
					case 'd':
						goto IL_0199;
					case 'f':
						goto IL_01ae;
					case 'k':
						goto IL_01c3;
					case 'c':
						goto IL_01d8;
					case 's':
						goto IL_01ed;
					case 't':
						goto IL_01ff;
					case 'h':
						goto IL_020e;
					case 'm':
						goto IL_021d;
					default:
						goto IL_0282;
					}
					if (text == "ja-jp")
					{
						goto IL_024a;
					}
				}
			}
			else
			{
				switch (text[0])
				{
				case 'j':
					break;
				case 'e':
					goto IL_0106;
				case 'd':
					goto IL_011b;
				case 'f':
					goto IL_0130;
				case 'k':
					goto IL_0145;
				default:
					goto IL_0282;
				}
				if (text == "ja")
				{
					goto IL_024a;
				}
			}
		}
		goto IL_0282;
		IL_01ed:
		if (text == "zh-sg")
		{
			goto IL_0272;
		}
		goto IL_0282;
		IL_021d:
		if (text == "zh-mo")
		{
			goto IL_027a;
		}
		goto IL_0282;
		IL_026a:
		return "ko";
		IL_01ff:
		if (text == "zh-tw")
		{
			goto IL_027a;
		}
		goto IL_0282;
		IL_0252:
		return "en";
		IL_011b:
		if (text == "de")
		{
			goto IL_025a;
		}
		goto IL_0282;
		IL_0106:
		if (text == "en")
		{
			goto IL_0252;
		}
		goto IL_0282;
		IL_0130:
		if (text == "fr")
		{
			goto IL_0262;
		}
		goto IL_0282;
		IL_0282:
		return null;
		IL_0145:
		if (text == "ko")
		{
			goto IL_026a;
		}
		goto IL_0282;
		IL_016f:
		if (text == "en-us")
		{
			goto IL_0252;
		}
		goto IL_0282;
		IL_0272:
		return "zh-Hans";
		IL_01d8:
		if (text == "zh-cn")
		{
			goto IL_0272;
		}
		goto IL_0282;
		IL_0199:
		if (text == "de-de")
		{
			goto IL_025a;
		}
		goto IL_0282;
		IL_025a:
		return "de";
		IL_01ae:
		if (text == "fr-fr")
		{
			goto IL_0262;
		}
		goto IL_0282;
		IL_0262:
		return "fr";
		IL_0184:
		if (text == "en-gb")
		{
			goto IL_0252;
		}
		goto IL_0282;
		IL_020e:
		if (text == "zh-hk")
		{
			goto IL_027a;
		}
		goto IL_0282;
		IL_027a:
		return "zh-Hant";
		IL_024a:
		return "ja";
		IL_01c3:
		if (text == "ko-kr")
		{
			goto IL_026a;
		}
		goto IL_0282;
	}
}

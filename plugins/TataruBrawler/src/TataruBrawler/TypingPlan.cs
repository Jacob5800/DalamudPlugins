using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TataruBrawler;

public sealed record TypingPlan(string Expected, string Keys, bool Japanese)
{
	private static readonly Dictionary<char, string> Kana = BuildKana();

	private static Dictionary<char, string> BuildKana()
	{
		Dictionary<char, string> result = new Dictionary<char, string>();
		Add("あいうえお", "a i u e o");
		Add("かきくけこ", "ka ki ku ke ko");
		Add("がぎぐげご", "ga gi gu ge go");
		Add("さしすせそ", "sa shi su se so");
		Add("ざじずぜぞ", "za ji zu ze zo");
		Add("たちつてと", "ta chi tsu te to");
		Add("だぢづでど", "da di du de do");
		Add("なにぬねの", "na ni nu ne no");
		Add("はひふへほ", "ha hi fu he ho");
		Add("ばびぶべぼ", "ba bi bu be bo");
		Add("ぱぴぷぺぽ", "pa pi pu pe po");
		Add("まみむめも", "ma mi mu me mo");
		Add("やゆよ", "ya yu yo");
		Add("らりるれろ", "ra ri ru re ro");
		Add("わをん", "wa wo n'");
		Add("ぁぃぅぇぉゃゅょっゎゔ", "xa xi xu xe xo xya xyu xyo xtsu xwa vu");
		Add("ー", "-");
		Add("～〜", "~ ~");
		return result;
		void Add(string chars, string syllables)
		{
			string[] array = syllables.Split(' ');
			for (int i = 0; i < chars.Length; i++)
			{
				result.Add(chars[i], array[i]);
			}
		}
	}

	public static string NormalizeAnswer(string text)
	{
		return text.Replace('〜', '～').Normalize(NormalizationForm.FormKC).Replace('ヴ', 'ゔ');
	}

	public static TypingPlan? Create(string text)
	{
		text = text.Trim();
		int length = text.Length;
		if ((length > 96 || length == 0) ? true : false)
		{
			return null;
		}
		bool japanese = text.Any((char c2) =>
		{
			switch (c2)
			{
			case '〜':
			case 'ぁ':
			case 'あ':
			case 'ぃ':
			case 'い':
			case 'ぅ':
			case 'う':
			case 'ぇ':
			case 'え':
			case 'ぉ':
			case 'お':
			case 'か':
			case 'が':
			case 'き':
			case 'ぎ':
			case 'く':
			case 'ぐ':
			case 'け':
			case 'げ':
			case 'こ':
			case 'ご':
			case 'さ':
			case 'ざ':
			case 'し':
			case 'じ':
			case 'す':
			case 'ず':
			case 'せ':
			case 'ぜ':
			case 'そ':
			case 'ぞ':
			case 'た':
			case 'だ':
			case 'ち':
			case 'ぢ':
			case 'っ':
			case 'つ':
			case 'づ':
			case 'て':
			case 'で':
			case 'と':
			case 'ど':
			case 'な':
			case 'に':
			case 'ぬ':
			case 'ね':
			case 'の':
			case 'は':
			case 'ば':
			case 'ぱ':
			case 'ひ':
			case 'び':
			case 'ぴ':
			case 'ふ':
			case 'ぶ':
			case 'ぷ':
			case 'へ':
			case 'べ':
			case 'ぺ':
			case 'ほ':
			case 'ぼ':
			case 'ぽ':
			case 'ま':
			case 'み':
			case 'む':
			case 'め':
			case 'も':
			case 'ゃ':
			case 'や':
			case 'ゅ':
			case 'ゆ':
			case 'ょ':
			case 'よ':
			case 'ら':
			case 'り':
			case 'る':
			case 'れ':
			case 'ろ':
			case 'ゎ':
			case 'わ':
			case 'ゐ':
			case 'ゑ':
			case 'を':
			case 'ん':
			case 'ゔ':
			case 'ゕ':
			case 'ゖ':
			case 'ー':
			case '～':
				return true;
			default:
				return false;
			}
		});
		StringBuilder stringBuilder = new StringBuilder();
		string text2 = text;
		foreach (char c in text2)
		{
			if (Kana.TryGetValue(c, out string value))
			{
				stringBuilder.Append(value);
				continue;
			}
			bool flag = char.IsAsciiLetterOrDigit(c);
			if (!flag)
			{
				bool flag2;
				switch (c)
				{
				case ' ':
				case '!':
				case '\'':
				case ',':
				case '-':
				case '.':
				case '?':
				case '~':
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				flag = flag2;
			}
			if (flag)
			{
				stringBuilder.Append(c);
				continue;
			}
			return null;
		}
		if (stringBuilder.Length > 384)
		{
			return null;
		}
		return new TypingPlan(text, stringBuilder.ToString(), japanese);
	}
}

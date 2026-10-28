using System.Collections.Generic;
using System.Text.Json;

namespace TataruBrawler;

public sealed class DiagnosticDelta
{
	private int[]? previousNumbers;

	private string[]? previousStrings;

	private readonly Dictionary<string, string> previousUi = new Dictionary<string, string>();

	private long sample;

	public void Reset()
	{
		previousNumbers = null;
		previousStrings = null;
		previousUi.Clear();
		sample = 0L;
	}

	public string? Capture(int[] numbers, string[] strings, IReadOnlyDictionary<string, object> ui)
	{
		List<int[]> list = new List<int[]>();
		List<object> list2 = new List<object>();
		Dictionary<string, JsonElement> dictionary = new Dictionary<string, JsonElement>();
		for (int i = 0; i < numbers.Length; i++)
		{
			if (previousNumbers == null || previousNumbers[i] != numbers[i])
			{
				list.Add(new int[2]
				{
					i,
					numbers[i]
				});
			}
		}
		for (int j = 0; j < strings.Length; j++)
		{
			if (previousStrings == null || previousStrings[j] != strings[j])
			{
				list2.Add(new
				{
					index = j,
					value = strings[j]
				});
			}
		}
		foreach (var (key, value) in ui)
		{
			string text2 = JsonSerializer.Serialize(value);
			if (!previousUi.TryGetValue(key, out string value2) || value2 != text2)
			{
				dictionary[key] = JsonSerializer.Deserialize<JsonElement>(text2);
				previousUi[key] = text2;
			}
		}
		if (list.Count == 0 && list2.Count == 0 && dictionary.Count == 0)
		{
			return null;
		}
		bool full = previousNumbers == null;
		previousNumbers = (int[])numbers.Clone();
		previousStrings = (string[])strings.Clone();
		return JsonSerializer.Serialize(new
		{
			sample = ++sample,
			full = full,
			numericChanges = list,
			textChanges = list2,
			ui = dictionary
		});
	}
}

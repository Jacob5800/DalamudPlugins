using System;

namespace TataruBrawler;

public static class BackgroundText
{
	public static bool TryNext(string current, char key, string expected, out string next)
	{
		next = current;
		TypingPlan typingPlan = TypingPlan.Create(expected);
		if ((object)typingPlan == null || typingPlan.Japanese || current.Length > 96)
		{
			return false;
		}
		bool flag;
		switch (key)
		{
		case '\b':
			if (current.Length == 0)
			{
				return false;
			}
			next = current.Substring(0, current.Length - 1);
			return true;
		case '\r':
			return string.Equals(current, expected, StringComparison.OrdinalIgnoreCase);
		default:
			flag = true;
			break;
		case ' ':
		case '!':
		case '"':
		case '#':
		case '$':
		case '%':
		case '&':
		case '\'':
		case '(':
		case ')':
		case '*':
		case '+':
		case ',':
		case '-':
		case '.':
		case '/':
		case '0':
		case '1':
		case '2':
		case '3':
		case '4':
		case '5':
		case '6':
		case '7':
		case '8':
		case '9':
		case ':':
		case ';':
		case '<':
		case '=':
		case '>':
		case '?':
		case '@':
		case 'A':
		case 'B':
		case 'C':
		case 'D':
		case 'E':
		case 'F':
		case 'G':
		case 'H':
		case 'I':
		case 'J':
		case 'K':
		case 'L':
		case 'M':
		case 'N':
		case 'O':
		case 'P':
		case 'Q':
		case 'R':
		case 'S':
		case 'T':
		case 'U':
		case 'V':
		case 'W':
		case 'X':
		case 'Y':
		case 'Z':
		case '[':
		case '\\':
		case ']':
		case '^':
		case '_':
		case '`':
		case 'a':
		case 'b':
		case 'c':
		case 'd':
		case 'e':
		case 'f':
		case 'g':
		case 'h':
		case 'i':
		case 'j':
		case 'k':
		case 'l':
		case 'm':
		case 'n':
		case 'o':
		case 'p':
		case 'q':
		case 'r':
		case 's':
		case 't':
		case 'u':
		case 'v':
		case 'w':
		case 'x':
		case 'y':
		case 'z':
		case '{':
		case '|':
		case '}':
		case '~':
			flag = false;
			break;
		}
		if (flag || current.Length >= expected.Length || !expected.StartsWith(current, StringComparison.OrdinalIgnoreCase) || char.ToUpperInvariant(key) != char.ToUpperInvariant(expected[current.Length]))
		{
			return false;
		}
		next = current + key;
		return true;
	}
}

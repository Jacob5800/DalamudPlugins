namespace TataruBrawler;

internal readonly record struct ImeState(bool Available, bool Open, uint Conversion, bool Composing);

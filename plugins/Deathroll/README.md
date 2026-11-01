# Deathroll

Open with `/deathroll`. Set player count (default 8), roll maximum (default 999), and whether highest or lowest loses. Start the match before players use `/random 999` (or your chosen maximum).

The first distinct players to roll fill the roster. Only the first valid roll per player counts each round. Native “You roll” messages resolve to the host character. Use Mark out / ignore rolls to manually exclude someone; exclusions reset on a new match. Subsequent rounds accept only remaining players; eliminated players and spectators are ignored. All players must use the configured maximum. English native random-result messages are supported; manual roll entry can recover an unread result.

The window shows the roll count, sorted results, highest and lowest values. Resolve round selects the configured extreme. Ties require only the tied players to reroll. An elimination reduces the next round's count automatically; the last player wins.

Publish result sends the result to Party, Say or Shout. Automatic resolve/announce is optional and off by default. Example: `Alice Example is out with 12. Roll again!` No messages are sent merely by starting a match. Copy result is also available.

This is a fixed-maximum elimination game, not the alternate deathroll variant where each roll lowers the next roll's maximum. Names without world suffixes identify players, so same-name players from different worlds cannot be distinguished in this first version. Match progress is held in memory; settings persist.

Build: `dotnet build Deathroll.csproj -c Release`. Checks: `dotnet run --project tests/Deathroll.Tests.csproj -c Release`.

Validation: build and offline elimination checks passed. Native chat capture and sending announcements still require in-game verification. Published through the collection feed as Deathroll.

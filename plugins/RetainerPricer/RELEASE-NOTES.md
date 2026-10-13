### 0.5.0.7

- Fixed selecting or clearing a specific Sniper item corrupting UI text colors and making other labels disappear. Orange highlighting now resets correctly. Reload the plugin after updating if your UI is already affected.
- Added an independent World (default), Data Center, or Region (home region only) selector to Undercut notifier. The Server column identifies the lowest matching competitor in that scope.
- Starting Sniper with the same market scope also runs notifier checks every 60 seconds. Stopping Sniper ends linked checks unless independent notifier watching is active. Starting the notifier alone never starts Sniper. Notifier checks use separate current-listing requests, not Sniper sales-history estimates.

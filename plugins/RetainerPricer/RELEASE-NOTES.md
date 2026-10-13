### 0.5.0.0

#### Inventory right-click shortcuts
- Added five separate Add/Remove options: Exceptions, Don't reprice, Batch selling, Retainer sale whitelist, and Don't vendor.
- Add options are green for market lists and yellow for the two vendor lists. Remove options are always red.
- Options are on by default. Turn them off with Settings → Show inventory right-click list options.
- Add options are hidden when the selected item cannot use that action, or a protection prevents it. Saved entries still have a Remove option. These lists apply to both HQ and NQ items.
- Adding to Batch selling starts at 10 items per listing with no total limit. Change those amounts in Batch selling.

#### Pricing rules and items with no competitors
- Added Custom gil amount (1–10,000 gil) and Percentage (1–100%, with up to two decimal places, such as 5.2%). The amount input appears only when that rule is selected. Match lowest and Undercut by 1 gil remain available.
- Auto update, Update existing listings, Start listing items, and Start batch selling only use current same-quality competitor listings first. A recent sale is no longer required when competitors exist.
- With no competing listings, existing prices stay unchanged. New-item listing and batch selling can use the same-quality sales median from the last 30 days, applying your selected pricing rule and minimum price. Without usable sales or listings, the item is skipped.
- The minimum price and large-price-drop approval bands still apply. A large drop during repricing is held for your approval rather than applied automatically.

#### Undercut notifier
- Added a tab showing saved retainer prices, cheaper home-world competitors, and the last recorded same-quality sale. Hover the sale price for its date.
- Start watching checks every 60 seconds. Echo alerts are on by default and can be turned off. Repeated alerts for the same retainer, item, quality and saved price are suppressed during the plugin session.
- Pause and hide in duties is on by default. This pauses checks and hides notifier results while you are in a duty.
- Starting Sniper in World scope also runs notifier checks. Stopping Sniper ends linked checks unless you started independent notifier watching. Starting the notifier alone never starts Sniper. Data Center and Region Sniper runs are not linked.
- Listing and batch-selling runs save the current retainer's listings after each confirmed new listing, without starting watching. Use Refresh all retainer listings to capture other retainers or refresh prices after sales and repricing.
- The notifier compares home-world listings of the same HQ/NQ quality. It ignores saved own-retainer IDs and unknown owners. Saved prices and Universalis data can be outdated; both timestamps are shown. Alerts do not require recent sales.

#### Existing listings
- Results now remain visible across the entire retainer update run, including reasons an item was skipped. An empty final retainer no longer clears earlier results.
- Added a retainer dropdown, item-name search, and clickable column sorting. Click Retainer to group results by retainer.
- Added Last sold price with the sale date on hover, and a 30-day sales median reference. Saved listings are shown when no live results are available. A dash means sales data has not been loaded or no matching sale was returned.

#### Sniper deal information
- Deal columns now show Item, Data Center, Server, Quality, Quantity, Listing, history-window median, Discount, Sales/day, Last sale age, Est. profit/item, and Savings/item.
- Discount shows how far the asking price is below the sales median. Sales/day shows recorded transactions divided by the chosen history window. Sniper's history window remains separately configurable from 3–14 days.
- Hover numbers for plain-language explanations. Hover Quantity or Listing for whole-stack purchase cost; hover profit/item or savings/item for whole-stack totals. Separate stack-profit and stack-savings columns have been removed.
- Estimated profit is shown by default. Turn off Show estimated resale profit to hide its column. Estimates assume resale at the median, using configurable purchase-fee and resale-tax assumptions, both defaulting to 5%. Actual fees and sale prices can vary.
- Added optional minimum discount, savings per item, and estimated profit per item filters. All three default to off and update the displayed deals immediately. 1-gil alerts bypass these filters and do not need sales history.
- Columns can be sorted and hidden. Latest-sale age and the history-check time on hover help identify stale estimates. Restart watching to refresh the sales baseline. Purchases remain manual.
- Fixed a different listing's removal incorrectly hiding a grouped 1-gil alert. Deals still disappear when their listing is removed or its updated price no longer qualifies.

#### Ventures and commands
- Added a yellow Start venture cycle button beside Start batch selling only at the top. The original Ventures-tab button remains available; both use your venture settings.
- Venture cycles can run while Sniper watches. Pricing, retainer selling and venture actions still avoid overlapping game menus.
- Added /rp as an alternative to /retainerpricer and /retainer. If another plugin already uses /rp, the existing commands remain available.

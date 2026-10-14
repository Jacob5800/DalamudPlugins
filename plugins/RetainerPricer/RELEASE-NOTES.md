## 0.5.0.27

- Added Skip 1 gil lookup under Scan options, off by default. It skips targeted startup lookups for existing 1-gil offers; new live 1-gil alerts still appear normally. Applies when watching starts.
- Existing 1-gil listing lookups now request up to 100 flagged items together, with sales history disabled. Successful item responses are retained; only missing or failed items stay pending for retry. Confirmed offers still receive history verification afterward.
- Sniper deals-table default widths now fit complete column headings and sort arrows at the current font size. Updated the layout identifier so old narrow saved widths do not carry over. Columns remain resizable and the table scrolls horizontally when necessary.

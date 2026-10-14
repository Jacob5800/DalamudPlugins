## 0.5.0.29

- HQ labels in the Sniper Quality column are highlighted cyan; NQ keeps the normal text color.
- Fixed existing 1-gil batches exceeding the response-size limit. Requests now return only price, quantity, quality and world fields, with sales history disabled. The targeted lookup has an 8 MB bounded response allowance; other market request limits remain unchanged.
- Existing 1-gil checks still batch up to 100 selected/flagged items. They are processed before history verification; failed lookups are deferred so verification can continue. A selected-item watch list limits both lookup and live verification scope.

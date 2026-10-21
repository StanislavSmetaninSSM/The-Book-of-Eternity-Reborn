Complete T407 original Windows cut0 diagnostics, stored in9 consecutive byte parts. No records were omitted. The original ZIP is857617577bytes, SHA2565BE1E5B63FD87BF6A59B03F9AEF110BC7DB9F4B17283D25EF50AFD1F86B11D7F.

Parts are split only to satisfy the remote Git blob size limit. transport.json records all offsets, sizes and hashes. Reconstruct to a new path using Python:

```powershell
python transport.py reassemble --manifest transport.json --output C:/Temp/T407-original-cut0-raw.zip
```

The output must not already exist. Reconstruction verifies all part hashes, the complete byte stream and its final reread. The raw index is ../T407-original-cut0-raw.json. This byte transport does not itself establish game or performance acceptance; see ../T407-original-cut0-outcome.json and the separate leaf findings.

# Complete T411 raw archive, lossless Git transport

These seven files contain every byte of the closed T411 original C:/Temp Cut0 raw ZIP. They are transport parts, not a diagnostic quota. No records or payloads were removed.

Original: T411-original-cut0-raw.zip, 665274231 bytes.
SHA256: 6D79BFA61E73E0D82FE3F5D91905C4ACB1C69C315E5AAF31C7979349D315BC03.

Each offset, part size and SHA256 is in transport.json. Six parts contain 96 MiB each; the last contains61294455 bytes. GitHub's per-blob transport constraint is independent of diagnostic collection or retention.

Using Python, from any directory, supply absolute paths:

    python /absolute/path/transport.py verify --manifest /absolute/path/transport.json
    python /absolute/path/transport.py reassemble --manifest /absolute/path/transport.json --output /absolute/path/to/a/new/T411-original-cut0-raw.zip

The helper verifies every part and the final concatenation. Reassembly writes only a new file, refuses overwrite and rereads the result for SHA256; it deletes nothing. It does not run tests, qualify their result, unpack game roots, alter security settings or Git configuration. Read the sibling T411-original-cut0-raw.json and T411-original-cut0-timeout-qualification.json for raw scope and actual failed-case qualification. The associated complete analysis ZIP has all parent/independent metrics and analyzers.

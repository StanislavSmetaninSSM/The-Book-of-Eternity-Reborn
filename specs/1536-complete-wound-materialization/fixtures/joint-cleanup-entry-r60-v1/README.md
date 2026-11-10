# Immutable cleanup-entry DATA fixture

This small test INPUT is the exact cleanup-entry capture from accepted r60 original
T483-original-full, source7eea8c922a044f857e12196e7cccabbcb7da0977. It contains only
221 inert payload files, generation bytes and their original transport manifest.
There are no diagnostic timer streams, native output logs, active owners or locks.
The exact original SHA, parent/run/cut provenance and accepted archive member are
recorded in source-record.json; no provenance field or payload byte was rebound.

It explicitly replaces the unavailable historical T333 prerequisite for cleanup
validator/history component fixtures. JointObservedDataTemplate still checks its
full exact SHA, inventory, absence and CleanupEntry provenance before copying.
Each test obtains current authority through actual production validators on its
own independent copy. The seven original authority/manifest/hash/backup fault
assertions are unchanged. Detailed raw diagnostics stay outside Git on D:.

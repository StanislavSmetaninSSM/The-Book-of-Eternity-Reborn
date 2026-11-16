# Research decisions

Source #1516; see plan.md for exact owner map.

- Chosen: explicit structured assessment plus canonical evidence checked by existing transaction owners. Prompt-only enforcement cannot prevent frozen state; independent simulation would invent a new gameplay system and violate scope.
- Chosen: existing cycle configuration determines meaningful elapsed time, including newly crossed intervals. Pending-cycle counters alone are insufficient.
- Chosen: separate private report/history. Current news renderer exposes arbitrary event fields; putting hidden causes there would leak information.
- Chosen: existing faction/NPC/project reducers, followed by exact evidence check. Arbitrary JSON patching would bypass authority and lifecycle contracts.
- Chosen: current supported fixtures/bootstrap only, no old-save promotion migration. Later direct user instructions and pre-release governance supersede historical legacy wording.
- Still implementation work, not a product question: exact pre-seal joint/QTE receipt integration and current-root identity resolution. These remain explicit unchecked tasks rather than assumed coverage.

## Environment and first admission checkpoint

Remote `9863701a` preserves tests, the explicit NotImplemented scaffold and its
narrow catalog owner before the first long runner. It is intentionally not a
working implementation. Dependency restore succeeds with writable XDG data,
cache/config/runtime and NuGet paths. The first official runner selected no
cases because its build failed before discovery: MSBuild child-node IPC socket
creation is denied inside the restricted command sandbox (diagnostic stack is
NamedPipeServerStream / SocketException 13). This is not a causal RED or a test
failure. Retry uses the approved command escalation for local process IPC;
no runner limit or selection is weakened and hosted CI remains unused.

## Targeted independent owner consultation

Fresh GPT-6 Astra High consultation identified the following requirements for
T007–T010 (read-only design guidance, not acceptance review):

1. Authenticated original semantic images differ from current working rollback
   images. Retain both. Fresh post-publication checks compare legitimately
   changed paths with expected after-images, not with opening source bytes.
2. Ordinary coordinator also owns Mortal diagnosis, whose admitted duration can
   cross a cycle. Use its real calculated clock contribution; do not treat every
   treatment as zero-time or alter its existing next-pending-cycle semantics.
3. Extend the currently emitted closed Mortal snapshot absence family through
   selection, creation, observed path union, reader validation and diagnosis
   forms. Register report/history in rollback, consumption/retry and privacy
   surfaces as well as snapshot observation.
4. Joint publication requires exact companion/cohort input before seal, candidate
   fingerprint and serialized recovery shape. It does not run all ordinary
   reducers, so command presence cannot prove joint effects.
5. QTE terminal fragments can contain `timeChange`/`setWorldTime`. Guard at offer
   admission AND shared terminal core, using accepted continuation identity.
   Terminal-only rejection can strand an already chosen outcome. Already
   assessed original pending counters must not force a duplicate missing report.
6. Existing absolute calendar override may have no comparable numeric clock.
   Require actionable numeric authority instead of interpreting this as zero
   elapsed time or inventing calendar conversion.
7. Private receipt attests the bounded verified publication, not final accepted
   turn nor processed progression cycles: ordinary progression application
   occurs later and full-turn rollback/retry must include the receipt.

One operation-local detached parsed bundle is reusable DATA; no additional
static registry/cache or reusable authority is proposed. No speedup is claimed.

The escalated command still cannot create Unix sockets in this environment;
loopback TCP is available. A successful explicit single-node build can therefore
precede the unchanged official runner's supported `-NoBuild` mode. This avoids
MSBuild out-of-process IPC without changing test selection or runner behavior.

# T355 — changed Windows storage probe: actual consumer result

Compiled `44320b27e77ff01315eec8685f12344f5c12386c`, accepted release `8a883c4d1dc4a7f1916009b2ea9aa56ee8fb0ac9`. One guarded Windows attempt,2planned/2completed/2PASS, no skip/timeout/retry. Command82.6804269s, category68.6665326s; negative test26.9988788s, positive34.5289886s. [Actual outcome/raw index](T355-profile-outcome.json), [all intervals](T355-timings.json.gz), [all named probe groups](T355-probe-analysis.json), [writer ledger/prefix reconciliation](T355-write-cost-analysis.json), [all storage-probe ancestry](T355-evidence-ancestry.json.gz), [ancestry groups](T355-evidence-groups.json), [DATA/closing checks](T355-analysis-summary.json), [prior-count comparison](T355-comparison.json), [exact offline tools](T355-analysis-tools.zip).

| Actual consumer | Positive | Transient negative |
| --- | ---: | ---: |
| Consumer operation-scope time |23.6814014s|14.2016943s|
| Process CPU during consumer |23.734375s|14.765625s|
| Storage-evidence calls |6,264|4,627|
| Storage-evidence inclusive time |5.4658410s|3.9821942s|
| Disjoint named-probe residuals |5.7578810s|4.1660090s|
| Serializer/write time |1.5073651s|0.5729088s|
| Writer share of component-reported wall |6.3652%|4.0341%|

All ten probe-group counts and EVERY operation count match accepted T352:109named groups/86,502intervals positive,108groups/54,076intervals negative. The same6,264positive storage checks previously occupied7.6596399s; negative4,627previously5.2916947s. These are separate instrumented observations, not a controlled wall-time benchmark or native/CPU attribution. Both clock boundaries are explicit: consumer operation scopes differ slightly from the separately captured component wall clock used for writer shares. Inclusive ancestors overlap and must not be summed.

T354 independently established structural real CreateFile-attempt reduction4→3,6→4,10→6 with unchanged physical results/refusals. Its final34independent small fixtures sum0.5962448s in TRX, maximum0.0861422s; category command19.9853528s includes runner work. These fixtures do not construct GameEngine/world/turn. Actual consumer integration remains deliberately separate and retains real proof/recovery/ownership work.

Full52,315,663JSONLbytes,281,156events and140,578closed intervals retained without quotas. Continuous sequences, zero parse errors/open scopes; every attempted/stored/measured named ledger and raw prefix reconciles.22indexedrawfiles SHA256 `60301E74502A046C715D7A2F3772BF66524A865F07020FB7A3990C18681BE383`; seven indexed analysis tools SHA256 `AC60EA1C98CAE82D0DA12F84FB379A845490E4FBD0B7422FD8409F116340C12D`.

Positive actual validation:0issues/2audits10footprints; exact transient pending-wave injection, retention, refusal and restoration in the negative. All220gamefiles/60manifestdirectories including empty directories/generation match immutable T333input. Actual outer completion/disposal and independent closing roundtrip passed; both component roots are absent. Input/four DLLs/four copies unchanged. T345 uncertain root and the separate T354 rejected-cleanup ACL root remain retained; no cleanup retry.

The change reduces physical work inside existing checks without deleting any observed operation. It does not resolve or qualify the historical original600second recovery hang, full-owner path, source42seed, original five scenarios or Linux. The Linux traversal and NativeLineage/native supervisor remain unchanged. The next bounded scope should assess one current-source full-owner replay using the already prepared independent DATA and unchanged protective bound; do not silently rerun the old original, increase limits or rebind a historical seed. Independent separate actual Sol6.1/xhigh review accepted without findings atbb8c08825; [acceptance](T355-final-review.json), all466clean recovery blobs independently verified.

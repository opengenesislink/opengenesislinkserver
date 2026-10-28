# OpenGenesisLINK Generation 1 – Hypergrid commerce and optional egress lockdown

**Technical build:** 0.9.4.0 Dev  
**Public product:** OpenGenesisLINK Generation 1  
**Normal policy:** Open Hypergrid commerce and authorized inbound imports.

## Policy correction to PR #328

The PR #328 initial settings `[HGInventoryAccessModule] OutboundPermission = false` and `[HGAssetService] DisallowAllExport = true` blocked ordinary outgoing Hypergrid inventory and asset delivery. They would prevent a visiting user from reliably taking a legitimately purchased item home and rezzing it there. That is *not* the Generation 1 marketplace policy.

The active configurations and distributed examples now use:

```ini
[HGInventoryAccessModule]
OutboundPermission = true

[HGAssetService]
DisallowAllExport = false
```

These settings live in **different service configurations**: GridCommon for regions and the public HGAssetService for ROBUST (including its FSAssets-backed `HGFSAssetService`). Check both on the production host: a repository change is not an automatic deployment. Protect the private local `[AssetService]` listener with a firewall.

The default allows ordinary HG asset transfer subject to the existing item, creator, next-owner and grid permissions. It does not guarantee transfer of every item; the originating seller must grant the appropriate rights.

## Optional emergency mode: NOT a Copybot-only switch

The code introduced in PR #328 remains as an **opt-in, grid-wide emergency control**:

- `DisallowAllExport = true` blocks public Hypergrid asset-content, metadata and existence reads for all asset types, including future/unknown types. The inbound HG POST path is independent.
- `OutboundPermission = false` prevents normal outbound Hypergrid inventory/asset delivery.

These controls are deliberately **disabled by default** because they block authorized HG commerce. If temporarily activated for emergency containment, warn visitors and sellers that purchases, attachments and suitcases may fail. Do not advertise this as routine anti-Copybot functionality.

## Copybot technical limit

The viewer tells the server its name/channel/version; these fields can be forged. Existing region `AllowedClients`/`DeniedClients`/`BannedClients` can restrict self-identified clients, but banning a viewer prevents its normal use, not just its export button.

A viewer must receive scene, texture, animation and mesh data to render the world. A modified viewer may save those bytes without invoking any server-side “export” operation. Consequently, there is **no secure server-side switch** that can guarantee “copybot viewer may import and view everything, but cannot export anything” while normal content loading stays available.

The intended protection roadmap is therefore:
1. Audit and enforce real server-side inventory permissions for take, take-copy, give, attachments and transfer (including HG ownership transitions), independent of reported viewer type.
2. Audit the World API, portal, asset/texture/mesh CAPS and privileged IAR/OAR export surfaces for strict authorization, owner scope, signed tokens, limits, and export logging.
3. Consider opt-in detection of excessive bulk asset fetching with operator review, rather than trusting viewer strings or blocking entire grids.
4. Add product provenance/licensing policies for Marketplace deliveries and retain secure administrator backup/restore.

## Required staging acceptance

1. Hypergrid guest using Firestorm visits Stadt-NexVerse, buys or receives a permitted outfit and mesh object, and sees them in inventory.
2. Guest returns to the home grid and can wear/rez the items with complete textures, meshes and permitted scripts.
3. Ordinary cross-grid teleport, attachments, inventory delivery, inbound uploads and authorized import continue to work.
4. Negative permission tests: unauthorized give/copy/take and unsuitable next-owner rights remain blocked independent of viewer name.
5. Confirm private asset service ports remain nonpublic and World API endpoints do not expose private asset data unauthenticated.
6. Optionally test the emergency lockdown on an isolated disposable stage; restore the **open** default afterwards.

**No production rollout is performed by this code change.** Stable `v0.9.3.13` remains unchanged.

## Public Hypergrid batch-enumeration guard (Generation 1, next security block)

The public `POST /get_assets_exist` endpoint historically deserialized arbitrarily large XML arrays and could enumerate unlimited asset IDs in one request. The connector now enforces limits **only for the public [HGAssetService]**. This reduces unbounded batch enumeration and XML resource exhaustion while leaving lawful per-asset GETs and authorized outgoing inventory/assets unchanged.

```ini
[HGAssetService]
DisallowAllExport = false
MaxHGAssetExistIds = 16384
MaxHGAssetExistXmlChars = 4194304
```

Values can be tuned to real grids' legitimate batch sizes. The server clamps the first value to 256–65536 IDs and the second to 65536–16777216 XML characters. The defaults intentionally accommodate large, legitimate objects; above-limit batch IDs receive HTTP 413, and oversized/malformed XML receives HTTP 400. XML DTDs and external entities are prohibited. On a private [AssetService] the legacy batch-limit behavior is unchanged, but XML DTDs are still prohibited. The independent inbound asset POST stays available and is not affected.

This does **not** distinguish Darkstorm from Firestorm, does **not** block a malicious viewer saving rendered asset data, and does not prevent an attacker from splitting requests into smaller batches. A hard cap is an abuse-resistance layer, not copy protection or user authorization.

### Acceptance and negative tests

- Public HG: exactly 16384 IDs parses and passes to the service (valid IDs), 16385 returns 413 before any lookup.
- Public HG: a small valid `string[]` XML request responds as before; malformed XML, DTD, or an XML body over 4194304 characters fails without a lookup.
- Private [AssetService]: large internal batch operations remain unaffected by the public limit.
- Actual HG visitor: purchase, suitcase transfer, home-grid rez, wearables and textured mesh attachments function; import and public HG GET paths remain available.
- Monitor and adjust the HG thresholds only if real production telemetry reveals oversized legitimate batches. Do not disable trade to respond to copybot activity.

# OpenGenesisLINK Generation 1 – Hypergrid Asset Egress Lockdown (Phase 1)

**Technical build:** 0.9.4.0 Dev  
**Public product:** OpenGenesisLINK Generation 1  
**Scope:** Strict server-side outbound **Hypergrid asset exchange** blocking on the public HG connector; authorized inbound asset upload is independent.

## Explicit technical limit

This is **not** 100% Copybot prevention. A viewer must receive mesh, texture, animation and scene information to render a world; a malicious viewer can record delivered data without using the official inventory/export APIs. Hiding the export button, detecting a viewer name or blocking one HTTP route cannot fix this fundamental limitation.

This measure blocks **defined server-side export channels**, not client-side reconstruction or photography/screen capture. It does not change legal ownership or grant permission to import third-party content from Second Life. Only creator-owned or appropriately licensed imports may be accepted.

## Controls introduced

1. `[HGInventoryAccessModule] OutboundPermission = false` on active GridCommon and distributed configuration examples, to prevent normal foreign-visitor inventory transfers and HG asset posting.
2. `[HGAssetService] DisallowAllExport = true` on the ROBUST public Hypergrid FSAsset endpoint, sample ROBUST configs, GridCommon and standalone sample.
3. The shared `AssetPermissions` contract blocks outbound assets **of every type**, including unknown/future enum values. `AllowedImport` is unchanged.
4. The public `HGAssetService` HTTP connector skips registration of `GET /assets` (raw content + metadata) and `POST /get_assets_exist` entirely when lockdown is enabled; `POST /assets` is retained for authorized **inbound** asset storage.
5. The Hypergrid implementations `HGFSAssetService`, `HGAssetService` and `HGRemoteAssetService` deny regular/cached read and metadata/existence enumeration where applicable, even through their in-process interfaces.

## What remains reachable / considerations

- The **private local [AssetService]** is not disabled: simulator/ROBUST communication and normal Firestorm rendering require internal reads. Its private HTTP listener MUST remain behind a firewall/private networking or authenticated network access. An exposed private AssetService would circumvent the public HG lockdown.
- Local viewer CAPS `GetMesh`/`GetTexture`/`ViewerAsset` still deliver assets needed for rendering. This is not an anti-viewer-extraction guarantee.
- Local user and administrator IAR/OAR/console exports, filesystem copies and DB backups are intentionally **not** removed in Phase 1. Those require a separate operator policy and may be essential for disaster recovery.
- Outbound HG suitcase items and attachments may cease to function on foreign grids. Foreign avatars may lose access to assets originally hosted by this grid. Inbound cross-grid teleports can still work, but asset availability must be tested.
- Authorized imports remain possible through existing upload flows, but importing foreign copyrighted content without permission is not authorized by this feature.
- Do not expose the public untrusted HG POST endpoint without considering upload/abuse rate limits and owner verification; these are separate controls.

## Tests and staged acceptance

- Build and run the full repository NexVerse CI; its static egress guard checks all code/config variants.
- On a **staging** ROBUST with both connectors, check that on the public HG listener `GET /assets/{uuid}`, `GET /assets/{uuid}/data`, `GET /assets/{uuid}/metadata` and `POST /get_assets_exist` cannot return a local asset or its metadata. Expect an unregistered route/not-found (exact HTTP code depends on server routing).
- Check that local private `AssetService` reads and Firestorm mesh/texture loading continue to function; do **not** test by exposing the private port publicly.
- Check authorized asset upload/ordinary viewer upload and permitted IAR import; verify the HG public asset POST only accepts the expected input format and cannot fetch unrelated assets.
- Verify denied outbound asset transfers (objects, meshes, textures, scripts and unusual/unknown types), outgoing HG suitcase behavior, inbound HG visitors, border crossing and teleport return.
- Confirm no accidental exports through supplementary World API/portal endpoints. Every export route must have its own authorization and ownership checks.
- Confirm local operator backups and restore work. Investigate any feature regression before production activation.

## Next protection blocks

Phase 2: audit World API inventory/media and asset HTTP endpoints for owner scope, capability token lifetime, abuse/rate limiting and signing, logging and export authorization.  
Phase 3: optional audited IAR/OAR user-export policy (retain secure operator backups), content provenance/licensing and watermarking.  
Phase 4: in-world anomalous scrape monitoring for investigation, not automatic untrusted viewer bans.

**Do not advertise this as complete Copybot protection.** Security expectation: server-side HG egress closed, viewer-data capture still possible.

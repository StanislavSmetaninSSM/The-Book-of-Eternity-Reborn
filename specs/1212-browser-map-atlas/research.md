# Research: Browser Map Atlas Drilldown

## Decision: Keep C# map DTO authoritative

**Rationale**: The existing map DTO already contains node identity, placeholder state, details, links, z-levels, regions, and generated image URLs. Browser work should render this contract rather than invent parallel map state.

**Alternatives considered**: Add browser-only state derivation. Rejected because it would risk console/browser divergence.

## Decision: Use a dedicated media URL allowlist

**Rationale**: Player-copy sanitization correctly removes `/api/...` strings from prose, but map image URLs are not prose. A small allowlist preserves trusted local media URLs while rejecting unsafe schemes.

**Alternatives considered**: Disable sanitization broadly for map nodes. Rejected because labels and details still need player-facing text boundaries.

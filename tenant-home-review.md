disposition: ship

Scope: local Tenant Home preview for user review, not production integration. Review performed inline by the implementing agent using Impeccable's finish-reviewer and documenter guidance; no independent reviewer or automated Impeccable gate ran. The Windows context launcher failed, and direct context reading was used. Existing P3 identity and already-confirmed Home brief retained; no new-world concept round or image comp was used.

## persistence

PRODUCT.md records confirmed scope. tenant-home-design-brief.md records the Home strategy. DESIGN.md and .impeccable/design.json describe the built preview only. Original Program.cs hash remains A7C65A326EE5C6703B7A21ADE59B4009AAB6D98BD9440357A48C105465D1E64E. Feature branch unchanged; no commit or push.

## fidelity

| Element | Result | Evidence |
| --- | --- | --- |
| TYPE | match | Existing app sans; 32px desktop/28px mobile greeting, 14–15px main copy, 13px notice detail. No new display identity. |
| MATERIAL | match | Flat app surfaces; no imitation scenes or decorative textures on Home. Existing room SVGs elsewhere are outside this change. |
| GROUND | match | Home #f6f7f9 ground and #eaf0f6 confirmed-visit panel from the scoped brief and CSS. |
| Confirmed visit | match | Arrives first, explicit status/date/time/address; routes to appointment 2 in sample. Pending-only fixture does not populate it. |
| Notices | match | Two recent entries, complete bell list, read-all and routes to correct appointment. |
| Reading | adaptation | Inline reading and initially collapsed content reduce interruption and page length. Filter, checklist persistence and help links verified. |
| Small room section | match | At most two saved items, no room gallery on Home, shared favorite state and return to Home. |
| Responsive | match | Real DOM order switches below 1000px. Desktop/mobile/tablet captures inspected. |

Captures: C:/Users/ASUS/.codex/visualizations/2026/10/13/01a135d7-a110-7673-955a-c339104538cc/tenant-home-impeccable/home-1440.png, home-390.png, home-834.png; additional review-board.png, notifications-320.png and empty-390.png.

## ceiling

Purposeful app composition with P3 identity retained. Familiar typography, plain list rows and native controls are deliberate. No marketing hero, ornamental motion, synthetic scene, external image/font or new brand claim. Message and support destinations remain labeled prototype scope; no CMS or real notification persistence has been implemented.

## material_fixes

- Resolved: initially open checklist made mobile unnecessarily long. Changed to initially collapsed; final captures inspected and eight Home widths reconfirmed.
- Resolved: Help should open an article even when it was already expanded. Verified after explicitly resetting expansion for that entry path.
- No further material preview findings from these passes. This is not a full accessibility conformance audit or backend review.

## keep

Retain the separation between Home and Search, truthful appointment statuses, small saved-room section, and clear preview labels.

## verification record

First pass: five views × eight widths, no horizontal overflow, duplicate IDs or JavaScript exceptions. Functional checks: article filters and inline reading, checklist state across breakpoints, appointment destinations, read-all and Escape/focus, Help, saved-room return, wrapper resizing. No non-GET requests in the observed flows.

Final confirmation: Home at eight widths with collapsed articles; Help reopening, checkbox persistence, no-confirmed/no-saved fixture, Pending-only fixture, 320px notifications dialog and wrapper view synchronization. No JavaScript exceptions. An intermediate verification selector matched a hidden Help button as well as the article toggle; it was scoped to the story element and the check passed, without an additional UI change.

Documenter output: DESIGN.md and .impeccable/design.json written. Palette: P3 blue with neutral surfaces. Type: app sans and fixed scale. Layout: content plus utility rail above 1000px. Depth: tonal panel and dividers. States: inline article expansion, native checklist and visible focus. Existing out-of-scope preview screens and production MVC styles were not canonized into the Home rules.
## MVC extension — review ngày 15/10/2026

Disposition: ship at the implemented Tenant Home/Notification scope for Dương's review. This is the later, approved MVC extension; the earlier record above describes the standalone outline. Inline review/documentation performed by the implementing agent, not an independent or automated gate.

### persistence

Confirmed user approval of the Home-to-MVC plan. PRODUCT.md, DESIGN.md and design sidecar now name the MVC scope and sources. Detailed integration boundaries and test evidence: docs/tenant-home-mvc.md. Program.cs hash unchanged; no shared schema, migration or team database write. Branch feature/tenant-p3; no commit/push.

### fidelity

TYPE: retained sans app scale, readable fixed sizes and native controls. MATERIAL: flat sections and geometric SVG icons only, no scene imitation on Home. GROUND: retained #f6f7f9 with the #eaf0f6 confirmed-visit panel. Confirmed-only next visit, two recent notices, inline guides and at most two saved rooms match v3. The notification inbox is a page rather than the outline dialog: intentional progressive server rendering for pagination, CSRF and accessible ordinary navigation. Existing MVC modules outside Home retain their styles.

### ceiling

Existing P3 identity retained. Native details/summary exposes reading without JavaScript; optional category filtering enhances it. The current task does not create a new brand or require generated assets. Message/support submission and CMS are visibly incomplete and documented as future work, without fabricated empty conversations or published news.

### material_fixes

Resolved in the single fix batch: unbroken titles overflowed; added inherited wrapping. Resolved: tablet panel did not occupy the full content width through display:contents; explicitly sized flattened section children. Final eight-width confirmation found no overflow with 180-character unbroken titles, kept checkbox/details state, and confirmed reduced-motion behavior. Final desktop/mobile/tablet captures were inspected. No further material findings within these passes; not a full accessibility conformance or whole-system security audit.

### keep

Keep truthful data/statuses, owner-scoped reads and POST writes, allowlisted tenant notification targets, small saved section, and separation between real MVC code and isolated demo data.

Documenter: DESIGN.md and .impeccable/design.json updated to include approved MVC sources. Palette retained. Type scale retained. Flat depth retained. Home-only scope retained. Actual native controls and long-title/tablet fixes recorded. Pre-existing design differences elsewhere were neither repaired nor made Home rules.

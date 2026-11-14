# Steam store checklist (verify sizes against Steamworks docs before upload — Valve updates them)

## Graphical assets → steam/store/
- [ ] Header capsule 920×430
- [ ] Small capsule 462×174 (title readable!)
- [ ] Main capsule 1232×706
- [ ] Vertical capsule 748×896
- [ ] Page background 1438×810 (optional)
- [ ] Library capsule 600×900
- [ ] Library hero 3840×1240 (no text) + library logo 1280×720 PNG w/ transparency
- [ ] Library header 920×430
- [ ] Screenshots ≥5, 1920×1080, gameplay only
- [ ] Trailer (gameplay in first 5 s), 1080p+

## Text (director approves every word)
- [ ] Short description ≤ 300 chars
- [ ] Long description + feature bullets + GIFs
- [ ] Tags (up to 20, most specific first), genre, languages, system requirements
- [ ] AI-generated content disclosure (content survey) — accurate and complete. In the coffee theme, the Vintage sprites (cherry pellet, power-up mug, 7 bonus fruits in `game/Assets/Resources/Themes/Vintage/`) were generated with Gemini (see `art/ai_sprite_prompts.md`); everything else is procedural code art. Declare pre-generated in-game art.

## Release
- [ ] Coming Soon page live ≥ 2 weeks before launch (wishlists)
- [ ] Build on beta branch tested from a clean install
- [ ] Price set, launch discount decided, store review submitted (allow several business days)

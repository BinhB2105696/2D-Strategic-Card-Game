# Duel Presentation Plan

> Prepared 19 October 2026. Planning document; no animation package or asset
> pack has been imported. `Docs/PROJECT_CONTEXT.md` remains the gameplay and
> scope authority.

## Goal and first acceptance slice

A spectator must be able to tell who acted, which card or target was involved,
what changed, and whose turn it is. Borrow the pacing and clarity of Duel Links
card/chain prompts and Pokemon-style damage feedback; use this project's own
art, layout, numbers, and rules. The first slice is one complete local duel
with visible feedback for every action family, including Auto playback.

| Accepted event family | First visual treatment | Information that must stay readable |
|---|---|---|
| Draw / add to Hand / shuffle | Card back moves Deck to Hand; brief shuffle cue | Which player, Hand/Deck counts; hidden card identity stays hidden |
| Summon / tribute / Set / activate | Chosen card lifts then moves to its zone; tributes move to Graveyard first | Card identity when public, destination zone, summon position |
| Attack / response / chain | Source and target highlight; directional cue; chain-link labels resolve last in, first out | Attacker, target, response owner, negation or pass |
| Monster HP, player LP, Energy, stats | Small number tween/flash next to existing HUD | Old and new values, sign of change, active stat |
| Destroy / send to Graveyard | Fade/slide from zone to Graveyard; top Graveyard card updates | Which card left and why |
| Turn / phase / match end | Short banner over board; winner overlay remains until dismissed | Active player and phase, winner |

Typical simple action feedback should be brief (roughly 0.2–0.6 seconds per
beat); important attack or chain sequences can be longer. These are trial
targets, not rules. The user should later be able to use a Fast/Skip control,
especially during Auto and bot simulation. The compact duel log must remain
available when animations are skipped or a device cannot keep up.

## Integration boundary

`GameSession.TryApply(GameAction)` returns an `ActionResult` with ordered
`GameEvent` values. The first queue is implemented: it captures the visible
pre-action board, plays accepted events, then converges to the authoritative
final `GameState`. As of 9 November, Settings > Duel Log is implemented with
basic student Play Mode acceptance. It records accepted actions and ordered
events independently of animation playback, freezes each player's permitted
information, and retains scrolling position while reading older lines. It is
match-local only; persistent event history/post-match viewing are not implemented.
Fast/Skip and full presentation/device acceptance remain outstanding.
The accepted HP HUD now displays current HP only above a rounded five-color bar
about one-third of a Monster Zone wide. HP/ATK/DEF are right-aligned; the bar
shares the status canvas and attack animation clone. Recovery animates only the
actual capped change; maximum-HP events update bar ratios. No animation
may submit a hidden extra action or write to `GameState`.

Human controls, response prompts, and bot ticks pause while a blocking event
sequence runs. Auto and normal play use the same queue. Phase/turn transitions
must not move the perspective until any prior public card movement is legible.
Unexpected interruptions, scene exit, and match end should safely clear or
finish the queue and render the current authoritative state. Skip means finish
visuals immediately, not skip gameplay events.

Implementation order:

1. Add one queue/presenter adapter at the existing board `TryApplyAction`
   boundary; map event IDs to existing card/zone/HUD objects. Check hidden-info
   handling and viewpoint mirroring.
2. Completed at basic acceptance: text-only Settings > Duel Log from accepted
   actions/events, including Auto and automatic Recovery. Opponent draw/set
   identities are hidden and old lines do not reveal later-known private cards.
3. Implement move/fade/scale and number tween for draw, summon, attack,
   damage, destruction, and phase changes using built-in uGUI components and
   `AnimationCurve`. Add small pooled particle bursts only where a basic motion
   cannot convey the impact.
4. Verify a full human-vs-bot and Auto duel in portrait Game view, then an
   Android development build. Inspect UI layering, touch input, and frame
   pacing before adding extra effects.

Do not hand-edit scene, prefab, ScriptableObject, or `.meta` YAML. Keep saved
layout changes subject to the student's visual review. Use Editor scripts or
the Unity Inspector for generated/serialized Unity assets.

## Source shortlist and licenses

The current project already includes uGUI, TextMeshPro, Timeline, and Unity's
built-in Particle System. No new dependency is needed for the first slice.

| Source | Possible use | Decision now |
|---|---|---|
| [Unity Canvas Group](https://docs.unity3d.com/2022.3/Documentation/Manual/class-CanvasGroup.html), [AnimationCurve](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AnimationCurve.html), [Particle System](https://docs.unity3d.com/2022.3/Documentation/Manual/class-ParticleSystem.html) | Fade/disable temporary UI, tune movement, small hit/energy bursts | Use built-in tools first; keep static board and animated overlay separate if profiling shows excessive Canvas rebuilds. [Unity's UI guidance](https://unity.com/how-to/unity-ui-optimization-tips) explains the rebuild cost. |
| [Kenney Particle Pack](https://kenney.nl/assets/particle-pack) | Simple impact, glow, sparkle sprites | CC0; candidate for a few recolored bursts. Download only when the first native prototype needs it. |
| [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds) | Draw/confirm/phase UI sounds | CC0; audio is a later polish slice. |
| [Game-icons.net](https://game-icons.net/faq.html) | Generic status/energy/target symbols | CC BY; individual author attribution is required if used. Prefer existing icons or self-made symbols initially. |
| [Cartoon FX Remaster Free](https://assetstore.unity.com/packages/vfx/particles/cartoon-fx-remaster-free-109565) | Optional elemental hit bursts | Free under the Unity Asset Store EULA; compatibility and look must be tested in the current Built-in pipeline and on Android before adoption. Not required for MVP. |
| [DOTween free](https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676) | Optional tween authoring if hand-written queue motions become cumbersome | Compatible with Unity 2022.3 according to its current Asset Store listing; external dependency and setup cost. Revisit only after the native slice. |

The shortlist is research, not an import list. Before adding any third-party
asset, record its source, author, license, version, and where it is used. Review
the licenses of artwork and avatars already in the project before a public
build. Never copy Duel Links or Pokemon card images, UI art, audio, logos, or
effects directly. Their role here is a reference for feedback timing and
readability.

## Review checklist for the first implementation

- In Edit Mode, the saved board hierarchy and dimensions remain as approved.
- In Play Mode, one summon, one attack with a response, one destruction, and
  one phase change reveal a clear sequence with no duplicate action.
- Auto does not choose the next action until the previous visual sequence is
  complete; Fast/Skip does not alter match outcome.
- Hidden Hand information stays hidden from the other player's view.
- Graveyard top card, counts, Monster HUD, Energy and LP finish at the exact
  values in `GameState` after each sequence.
- Android portrait view has no clipped essential text or untouchable controls.

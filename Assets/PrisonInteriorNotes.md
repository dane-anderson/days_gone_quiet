# Connected Prison Interior

> Historical environment-pass notes: the current scene also includes Negan, combat, doors, and independent survival policies. See [the current survival controls](Survival/README.md). References below to the old yard brain and planned Negan work describe the earlier environment pass.

The PrisonYard scene now includes a physical entrance from the north yard fence into Cellblock C.

## Explore

Enter Play mode, focus the Game view, and use:

- 1–5: the original yard camera views.
- 6: enclosed indoor view of the cellblock, beneath the roof.
- 7: close view of the restrained walkers beside the entrance.
- 8: optional overhead cutaway for looking over the layout.
- T or the on-screen button: start/end a manual tour.
- WASD or arrows: move during the tour. Shift: run.

The prison has a visible concrete ceiling with ribs, pipes and fluorescent lighting. The roof and exterior walls stay visible during manual tours; the shoulder camera stays below the ceiling and avoids walls. Only view 8 hides the enclosure.

The tour starts in the yard in front of the prison. Walk north through the open doors. The stairs on the right lead to the upper gallery and upstairs cells. The west wing remains sealed.

Manual exploration pauses Rick and the original chasing walker, and restores them when the tour ends. The tour is unavailable while a Python trainer is connected. Rick’s saved brain, observation/action contract, training configuration, reward settings, and yard bounds have not been changed. The saved brain still learns in the yard; interior navigation is not trained yet.

## Added environment

- Twelve cells across two floors, with bars, open doors, bunk beds, desks and small props.
- A common room with tables, bedding, supplies and lanterns.
- Guard checkpoint, lockers, open steel entrance doors, overhead pipes and lights.
- Physical walls, floors, walkable stairs and gallery guards.
- Four restrained walkers outside the entrance with torso chains, wrist tethers and a subtle staged struggle.

The restrained walkers are visual-only at this stage: they have no chase AI, damage, death or release behavior. Their posts stay fixed. They do not affect the existing learning experiment.

## Next: Negan and teamwork

The agreed direction is to add Negan after the environment, with Lucille (his barbed-wire bat) and a separate training brain. First build and test the bat-swing/hit/damage interaction, then train him against a walker. Add Rick–Negan cooperation after their individual behaviors work. Negan, combat, a trained Negan model, and teamwork are not included in this environment pass.

## Editor tools

Days Gone Quiet → Build Connected Prison Interior updates only the extension and the named original entrance/facade pieces. It supports Undo. Do not run the older Build Prison Yard Scene command to update the interior; that recreates the original solid exterior and fences. If you deliberately rebuild the yard, run Build Connected Prison Interior afterward.

Days Gone Quiet → Enclose Prison Roof updates the ceiling and interior light details without rebuilding the cells or props.

Days Gone Quiet → Validate Connected Prison Interior checks scene references, cells, restraints, materials and the saved brain contract.

Days Gone Quiet → Test Interior Tour in Play Mode walks the yard-to-upstairs-cell route and verifies restoration. The original yard validation also remains available.

Validation performed: successful Unity compilation; connected-route traversal through the entrance, downstairs cell, stairs and upstairs cell; roof visibility and camera clearance during exploration; 561 indoor-camera enclosure samples across 4:3, 16:9 and 21:9; original yard validation (37 passed, 0 failed on the initial interior pass); and visual inspection of the roofed interior and restrained walkers.

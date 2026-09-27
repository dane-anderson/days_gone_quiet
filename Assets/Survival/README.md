# Prison survival controls

Open **Days Gone Quiet → World Control** in Unity. The panel lives beside the Game view and can be docked or moved to another monitor.

- **Location:** opens scenes already in the project. New city scenes will appear after refreshing the list.
- **Camera:** yard, front gate, courtyard, tower, Rick follow, enclosed cellblock, tied walkers, cutaway, and Rick/Negan/both focus.
- **Simulation:** Play, pause/resume, reset the round, and stop.
- **Survivors:** each character's health, kills, and whether it is using a trainer, a saved model, or a scripted preview.
- **Round settings:** train Rick, Negan, or both independently; set round length, walker count, speed, and health.
- **Doors:** operate the prison entrance, Cell 104, and dark hallway doors. Doors refuse to close on a character. Walkers can damage them until they break open.
- **Manual exploration:** optional inspection mode. T toggles it; WASD/arrows walk, Shift runs, and E operates a nearby door. The simulation pauses during exploration, then resumes its previous state.
- **Training:** separate RickEscape and NeganCombat model slots and the commands for the installed trainer/dashboard. These new brains use a different observation/action layout from the original yard-only Rick model.

Rick's training objective rewards survival and a threatened escape into the prison. Negan's rewards include damage and kills from Lucille. The brains are independent; coordinated teamwork is future work.

The current scripted preview demonstrates the mechanics. It is not evidence that either new brain has learned these skills.

Gameplay validation passed for connected yard/interior/gallery routes, physical door blocking, walker door bashing, bat damage and kills, pause, role selection, and shared round resets.

Manual-tour validation also passed on both the ground floor and upper gallery, including restoring Rick's position and the previous running/paused state.

The initial restricted-run training smoke test could not start because OpenMP shared memory was unavailable. Subsequent local training produced the earlier RickEscape and NeganCombat model snapshots now included in **Assets/Survival/Models/prison-duo-v1**. Neither model is assigned in the saved scene, and scripted preview is disabled: without a connected trainer or an assigned model, the learners remain idle. Use the model slots or **Load newest imported brains** in World Control to assign compatible exports. These are earlier imported snapshots, not the latest exports in the external training results. Their presence is not evidence of reliable survival, combat skill, or teamwork.

Original training logs and resumable checkpoints live outside this repository. Continuing an existing PPO run requires those checkpoints and the same run ID; the included ONNX exports are for inference.

Negan's transparent character artwork was made with the built-in image generation tool. The exact prompt is in **Negan-art-prompt.txt**; the generated PNG is **NeganSurvivor.png**. Lucille is separate animated geometry in Unity.

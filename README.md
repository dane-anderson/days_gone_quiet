# Days Gone Quiet 🧟‍♂️🌲

A small reinforcement learning survival project built in Unity.

The idea is simple:

> **Rick learns how to survive.**

Instead of scripting Rick to automatically run away from danger, Rick is an **RL agent**. He observes the world, takes actions, gets rewarded or punished, and gradually learns what keeps him alive.

---

## 🎮 Current Setup

Right now the world is intentionally simple:

- **Rick** = reinforcement learning agent
- **Walker** = threat that chases Rick
- **Unity** = simulation environment
- **ML-Agents** = reinforcement learning framework
- **C#** = game and agent logic
- **PPO** = training algorithm

Rick can move around a 3D arena while the Walker hunts him.

If the Walker catches Rick:

- Rick receives a negative reward
- the episode ends
- the world resets
- Rick tries again

And again.

And again.

Until he hopefully gets smarter. 😅

---

## 🧠 The Goal

The first milestone is:

**Teach Rick to survive one Walker.**

After that, the environment can become progressively harder:

- multiple Walkers
- obstacles
- safe zones
- stamina
- noise
- hiding
- supplies
- weapons
- larger environments
- groups of survivors
- more complex reward systems

Eventually, the goal is to move from a simple training arena into a much richer 3D survival world.

---

## 🛠️ Built With

- Unity
- C#
- Unity ML-Agents
- Python
- PyTorch
- PPO reinforcement learning

---

## 🚧 Project Status

**Very early development**

Current progress:

- [x] Unity project created
- [x] 3D training arena
- [x] Rick agent created
- [x] Walker chase behavior
- [x] Reward / episode reset system
- [x] ML-Agents trainer connected
- [x] First RL training run
- [ ] Improve Walker collision behavior
- [ ] Improve Rick's reward function
- [ ] Train Rick to reliably evade one Walker
- [ ] Replace placeholder capsules with actual characters
- [ ] Build a proper survival environment
- [ ] Add more Walkers
- [ ] Make it look awesome

---

## 🤖 Why Reinforcement Learning?

The fun part of this project is that Rick is not simply told:

> “If Walker is close, run away.”

Instead, Rick receives observations and consequences.

He has to discover useful behavior through experience.

That means some of the early training may look... questionable.

Rick may:

- run directly into the Walker
- spin around
- hide in a corner
- wander aimlessly
- discover something unexpectedly clever

That's part of the experiment.

---

## 🌎 Long-Term Vision

The eventual idea is a 3D survival environment where agents learn how to navigate a dangerous world filled with Walkers, obstacles, resources, and other survivors.

For now, though:

**One Rick.  
One Walker.  
One very confused neural network.**

🧟‍♂️

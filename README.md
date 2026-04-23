# AI Echo Hunter 

A grid-based stealth game where the enemy hunts the player using **sound instead of vision**.

## Core Idea
The enemy cannot see the player.  
Instead, it listens, predicts, and hunts.

Every player movement generates sound on a grid. The enemy reads these sound patterns to estimate the player’s position and make decisions.

---

##  Gameplay Mechanics

### Grid-Based Movement
- Built using Unity Tilemap
- Player moves one tile per step
- No physics → fully deterministic system

### Environment
- Maze-like grid with walls and open paths
- Both player and AI must navigate obstacles

---

## Sound System

- Each movement generates sound at a grid position  
- Stored in a 2D grid with **intensity values**
- Sound fades over time  
- AI uses strongest/recent sounds to track the player  

---

## Enemy AI System

Designed using **algorithmic AI (no ML)** for controlled, explainable behavior.

### Prediction
- Tracks recent sound positions  
- Estimates player direction  
- Predicts next movement  

### Uncertainty
- Occasionally investigates random nearby sounds  
- Prevents overly predictable behavior  

### Habit Learning
- Detects repeated player patterns  
- Biases future decisions accordingly  

### Pathfinding
- Uses **A\*** algorithm  
- Efficient navigation around obstacles  
- Always finds shortest path to target  

---

## System Design

- Modular architecture:
  - Sound System  
  - AI Logic  
  - Tilemap Movement  

- AI does not “cheat”:
  - No direct access to player position  
  - Relies only on sound data  

---

## Game Controller

- Centralized parameter tuning via Unity Inspector  
- Adjustable:
  - AI speed  
  - Prediction accuracy  
  - Behavior randomness  

---

## Tech Stack

- Unity  
- C#  
- Tilemap System  
- A* Pathfinding  

---

## How to Run

1. Open project in Unity Hub  
2. Load the main scene  
3. Press Play  

---

## Future Improvements

- Dynamic difficulty scaling  
- More complex sound propagation  
- Smarter behavior adaptation  
- UI/UX polish  

---

## Demo

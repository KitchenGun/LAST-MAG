# Enemy Refresh Blender Handoff

## Meshy source tasks

| Enemy | Meshy task | Credits | Source verdict |
|---|---|---:|---|
| Ranged Maw | `019ff515-2c96-7a28-8800-bebea0581f97` | 30 | Pass with texture-tone notes |
| Suicide Capsid | `019ff514-d5bc-7a16-b2bf-b21dcfc842b4` | 30 | Rebuild legs as exactly six |
| Melee Blade | `019ff515-4ce5-7182-9f1b-9f54d2606272` | 30 | Pass after simplifying blade branches |

Meshy balance after the three approved jobs: `650` credits. No automatic regeneration or paid remesh was used.

## Runtime contract

- Unity integration is intentionally deferred. Existing enemy FBX files, prefabs, controllers, scenes, and gameplay scripts are unchanged.
- Humanoid output keeps the existing 24 bone names and adds `Jaw` and `ProjectileOrigin_Mouth`. `RightHand` and `Head` remain available.
- Humanoid material order is `Body` then `Head`. Suicide material order is `Lower` then `Upper`.
- All locomotion is in-place and uses no root motion.
- Gameplay remains code-timed: melee impact at `0.45s`, ranged release at `0.7s`, humanoid death settles within `1.2s`.
- The generated actions are new keyframes for the refreshed bodies. Existing HumanoidBlob animation curves are not copied.

## Final acceptance targets

| Enemy | LOD0 | LOD1 | Required actions |
|---|---:|---:|---|
| Melee Blade | <= 6,000 tris | about 3,000 tris | Idle, Run, Overhead Smash, Hit, Death |
| Ranged Maw | <= 6,000 tris | about 3,000 tris | Idle, Heavy Walk, Maw Discharge, Hit, Death |
| Suicide Capsid | <= 5,000 tris | about 2,500 tris | Idle, Legs-only Move, Explosion Warning |

Every FBX must pass a fresh Blender import check for UVs, two material slots, action ranges, no root translation, and no vertex with more than four deform weights.

# Prefab catalog (DragonVisualCatalog)

Sinh bằng `build_character_reports.py` từ thư mục work của batch. Đường dẫn Unity là nơi **Dravio ▸ Character ▸ Import Batch Manifest** sẽ tạo prefab/controller; catalog thật là `Assets/Dravio/Character/Data/DragonVisualCatalog.asset`. Thứ tự tra: đúng stage → Unstaged → stage gần nhất (có cảnh báo).

| Loài | Stage | Bằng chứng | Rig | Archetype | Clip | Animation QA | LOD tris | Bone | Prefab |
|---|---|---|---|---|---|---|---|---|---|
| aurorawing | Adult | SAMPLE | có | winged | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle, TakeOff, Glide, Land | PASS | 24000/10000/4000 | 32 | `Assets/Dravio/Character/Prefabs/DRG_aurorawing_Adult_Rigged_LODGroup.prefab` |
| aurorawing | Baby | SAMPLE | có | winged | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle, TakeOff, Glide, Land | PASS | 24000/10000/4000 | 32 | `Assets/Dravio/Character/Prefabs/DRG_aurorawing_Baby_Rigged_LODGroup.prefab` |
| aurorawing | Juvenile | SAMPLE | có | winged | Idle, Eat, Sleep, Happy, Walk, Run, Fly, Rest, Wake, LookAround, Curious, Drink, Stretch, Bask, Greet, Nuzzle, TakeOff, Glide, Land | PASS | 24000/10000/4000 | 32 | `Assets/Dravio/Character/Prefabs/DRG_aurorawing_Juvenile_Rigged_LODGroup.prefab` |

## Còn thiếu

- Loài chưa có model (15): emberling, droplet, sprout, zephyr, steamdrake, magma, sparkwing, frostfin, mossback, blossom, solaris, leviathan, amberhorn, celestia, gaia
- Cặp loài/stage dùng fallback stage khác (0): —

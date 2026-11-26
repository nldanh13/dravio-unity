# Báo cáo batch import

Nguồn: `<work>/char/src` — 13 file GLB. Pipeline u1-char8-v1.

| Trạng thái | Số file |
|---|---|
| ok | 1 |
| ok_with_notes | 11 |
| validation_failed | 0 |
| failed | 0 |
| unchanged | 0 |
| unrecognized | 1 |

| File | Loại | Species/Category | Stage | Trạng thái | Rig | Kiểm định | Ghi chú |
|---|---|---|---|---|---|---|---|
| `SAMPLE_droplet_Baby.glb` | dragon | droplet | Baby | ok_with_notes | có | PASS | limbCount 4 taken from the mesh measurement (--limb-count auto): {'limbCountDetected': 4, 'confidence': 'high', 'groundContactsOffMidline': 4, 'groundContactsMidline': 0}. Confirm with the art director.; 244 vertices (floating fragments) bound rigidly to nearest bone. |
| `SAMPLE_emberling_Baby.glb` | dragon | emberling | Baby | ok_with_notes | có | PASS | 112 vertices (floating fragments) bound rigidly to nearest bone. |
| `SAMPLE_frostfin_Juvenile.glb` | dragon | frostfin | Juvenile | ok_with_notes | có | PASS | 155 vertices (floating fragments) bound rigidly to nearest bone. |
| `SAMPLE_leviathan_Adult.glb` | dragon | leviathan | Adult | ok | có | PASS |  |
| `SAMPLE_magma_Baby.glb` | dragon | magma | Baby | ok_with_notes | có | PASS | limbCount 4 taken from the mesh measurement (--limb-count auto): {'limbCountDetected': 4, 'confidence': 'high', 'groundContactsOffMidline': 4, 'groundContactsMidline': 0}. Confirm with the art director.; 112 vertices (floating fragments) bound rigidly to nearest bone. |
| `SAMPLE_mystery_object.glb` | unrecognized |  |  | unrecognized |  |  | no species id or prop keyword in "sample mystery object" |
| `SAMPLE_prop_crystal_cluster.glb` | prop | crystal |  | ok_with_notes |  | PASS_WITH_WARNINGS | Real-world size not given; kept source units (set props.<id>.height in asset-overrides.json). |
| `SAMPLE_prop_floating_island.glb` | prop | floating_island |  | ok_with_notes |  | PASS_WITH_WARNINGS | Real-world size not given; kept source units (set props.<id>.height in asset-overrides.json). |
| `SAMPLE_prop_treasure_chest.glb` | prop | chest |  | ok_with_notes |  | PASS | Real-world size not given; kept source units (set props.<id>.height in asset-overrides.json). |
| `SAMPLE_prop_tree_ancient.glb` | prop | tree |  | ok_with_notes |  | PASS | Real-world size not given; kept source units (set props.<id>.height in asset-overrides.json). |
| `SAMPLE_sprout_Baby.glb` | dragon | sprout | Baby | ok_with_notes | có | PASS | 112 vertices (floating fragments) bound rigidly to nearest bone. |
| `SAMPLE_steamdrake_Juvenile.glb` | dragon | steamdrake | Juvenile | ok_with_notes | có | PASS | limbCount 4 taken from the mesh measurement (--limb-count auto): {'limbCountDetected': 4, 'confidence': 'high', 'groundContactsOffMidline': 4, 'groundContactsMidline': 0}. Confirm with the art director.; 202 vertices (floating fragments) bound rigidly to nearest bone. |
| `SAMPLE_zephyr.glb` | dragon | zephyr | Unstaged | ok_with_notes | có | PASS | limbCount 4 taken from the mesh measurement (--limb-count auto): {'limbCountDetected': 4, 'confidence': 'high', 'groundContactsOffMidline': 4, 'groundContactsMidline': 0}. Confirm with the art director.; 196 vertices (floating fragments) bound rigidly to nearest bone. |

## Còn thiếu

- Loài chưa có mô hình nào (8): sparkwing, mossback, blossom, solaris, amberhorn, aurorawing, celestia, gaia
- Cặp loài/giai đoạn chưa có: 41 / 48
- Nhóm tài nguyên môi trường chưa có: dragon_statue, arch, fountain, coral, rock, flower, nest, fruit_basket
- Cần dữ liệu từ art director/GDD để rig: —

Structural pipeline results only; Unity import, visuals, animation quality and device performance are separate checks.

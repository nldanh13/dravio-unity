# DRG_magma_Baby

- Nguồn: `SAMPLE_magma_Baby.glb` — bằng chứng: **SAMPLE**. GLB nguồn không đổi: có
- Loài / giai đoạn: magma / Baby
- Kiểm định pipeline: PASS; Animation QA: **không có (chưa rig)**

## Rig

- Không rig (thiếu dữ liệu sinh học, xem batch-report). Chỉ có LOD tĩnh; trong game hiện tĩnh, không giả animation.

## LOD

| LOD | Tam giác | Đỉnh | Trong ngân sách |
|---|---|---|---|
| 0 | 24000 | 12090 | có |
| 1 | 10000 | 5090 | có |
| 2 | 4000 | 2090 | có |
- pass: LOD0 rest p95 deviation 0.000% (limit 0.40%)
- pass: LOD1 rest p95 deviation 0.019% (limit 1.00%)
- pass: LOD2 rest p95 deviation 0.054% (limit 2.50%)

## PBR

- Bộ texture URP: metallic_smoothness=`DRG_magma_Baby_MetallicSmoothness.png`, normal=`DRG_magma_Baby_Normal.png`, base_color=`DRG_magma_Baby_BaseColor.png`
- Base Color: image; UV chồng lấn 0.0034

## Unity

- prefab: `Assets/Dravio/Character/Prefabs/DRG_magma_Baby_LODs_LODGroup.prefab`
- material: `Assets/Dravio/Character/Materials/DRG_magma_Baby.mat`

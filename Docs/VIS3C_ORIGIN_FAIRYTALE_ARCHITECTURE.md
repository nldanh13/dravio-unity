# VIS.3c — Origin fairytale architecture and distant hills

Three actual self-contained glTF 2.0 binary mesh drafts generated from original code and committed to ArtSource/Origin_VIS3c:
- Origin_Fairytale_Cottage: 3968 triangles, single vertex-colour material.
- Origin_Fairytale_Lookout: 2072 triangles, single vertex-colour material.
- Origin_Soft_Hills: 1920 triangles, single vertex-colour material.

Cottage: rounded cream walls, curved gold roof, timber trim, blue windows, flower boxes and chimney. Lookout: layered stone tower, golden roof, banding and blue windows. Hills: five layered low rounded hills in cool blue/green. These are simple geometric blockout/art candidates, not hand-painted finished art. One material per mesh, no textures, no extra dependencies. The current camera is orthographic, tilted 32 degrees and the existing forest edge occupies z~13–15; a tall mountain placed behind it may be outside the view. No placement asserted. House and tower are scenery candidates, not Sanctuary projects, not unlocked by any game rule. Do not put them into the playable world or replace a construction reward without review.

Source generator: node scripts/art/environment/build_origin_vis3c.mjs <new-output-directory> (Node built-in fs only). The GLBs include positions, normals and linear RGB vertex colours. Unity project does not have a verified glTF importer; import into Blender first to inspect, bake/map vertex colour to compatible material/atlas before FBX export, preserve asset pivots. Review actual silhouettes from Origin camera, check front direction, shadows and colour. Use existing #89 staged tree/rock pipeline separately. No runtime files, colliders, location data, UI, prefab or gameplay changes in VIS.3c. JS mesh generation checked finite coordinates/unit normals/nondegenerate triangles; no Blender, Unity, external glTF validator or rendered visual QA performed. Material batching and screen visibility unverified. Do not merge solely on triangle counts.

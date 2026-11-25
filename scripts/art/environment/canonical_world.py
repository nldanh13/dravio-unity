"""Canonical zone / microhabitat facts the Ocean and Sky kits must follow (U1-ENV.4).

Read-only snapshot, copied by hand from the C# domain port so the art scripts never
import (or change) Core Domain code:

  * Assets/Dravio/Domain/Catalog/G0BiologyData.cs  (HabitatProfile rows)
  * Assets/Dravio/Domain/Catalog/WorldGraph.cs      (WorldEdge rows; GDD 3.1, ADR 0010)
  as of branch feat/u1-living-dragon @ 1b81caf (port of the TS repo at b33ec7a).

The species list is NOT copied: the tests read Assets/Dravio/Data/species-biology.json
(the original biology data) directly and check every ocean_* / sky_* habitatRules ID
against this table and the generated location data.

Ocean and Sky stay LOCKED in G1 (ADR 0010 section 4): CanTravel is not wired into
moveDragon. The environment only marks where the gateways are; it never unlocks or
moves anything.
"""

SOURCE = "Assets/Dravio/Domain/Catalog (feat/u1-living-dragon @ 1b81caf, TS b33ec7a)"

# id -> profile fields that matter for the environment art.
HABITATS = {
    "origin_meadow": {"zone": "origin", "realm": "Land", "temperature": "Mild", "humidity": "Normal",
                      "salinity": "None", "depth": "None", "altitude": "Ground", "perch": True, "vegetation": 80},
    "origin_pondbank": {"zone": "origin", "realm": "Land", "temperature": "Mild", "humidity": "Humid",
                        "salinity": "Fresh", "depth": "None", "altitude": "Ground", "perch": True, "vegetation": 85},
    "origin_warm_rocks": {"zone": "origin", "realm": "Land", "temperature": "Warm", "humidity": "Dry",
                          "salinity": "None", "depth": "None", "altitude": "Ground", "perch": True, "vegetation": 45},
    "ocean_estuary": {"zone": "ocean", "realm": "Ocean", "temperature": "Mild", "humidity": "Submerged",
                      "salinity": "Brackish", "depth": "Shallow", "altitude": "Ground", "perch": False, "vegetation": 85},
    "ocean_reef": {"zone": "ocean", "realm": "Ocean", "temperature": "Warm", "humidity": "Submerged",
                   "salinity": "Marine", "depth": "Shallow", "altitude": "Ground", "perch": False, "vegetation": 70},
    "ocean_cool_pool": {"zone": "ocean", "realm": "Ocean", "temperature": "Cool", "humidity": "Submerged",
                        "salinity": "Marine", "depth": "Shallow", "altitude": "Ground", "perch": False, "vegetation": 45},
    "sky_low_island": {"zone": "sky", "realm": "Sky", "temperature": "Mild", "humidity": "Normal",
                       "salinity": "None", "depth": "None", "altitude": "Low", "perch": True, "vegetation": 70},
}

# WorldGraph edges: id -> (zone A, zone B, required locomotion, {zone: gateway habitat}).
# There is no direct Ocean <-> Sky edge (the GDD does not name one).
WORLD_EDGES = {
    "origin_ocean_estuary": ("origin", "ocean", ("swim",), {"origin": "origin_pondbank", "ocean": "ocean_estuary"}),
    "origin_sky_low_island": ("origin", "sky", ("fly",), {"origin": "origin_meadow", "sky": "sky_low_island"}),
}

# Domain zone capacity (dragons per zone) - spawn sets must offer at least this many.
ZONE_CAPACITY = 5
LOCKED_IN_G1 = ("ocean", "sky")


def habitats_of(zone):
    return [hid for hid, h in HABITATS.items() if h["zone"] == zone]


def edges_of(zone):
    return {eid: e for eid, e in WORLD_EDGES.items() if zone in (e[0], e[1])}

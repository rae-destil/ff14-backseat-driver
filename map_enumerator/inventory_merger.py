import json

existing_maps_data = {}
with open("existing_instances_data.json", "f") as f:
    existing_maps_data = json.load(f)

new_maps_data = {}
with open("instances_data.json", "f") as f:
    new_maps_data = json.load(f)


with open("merged_instances_data.json", "w") as f: 
    for territory_id, territory in new_maps_data.items():
        if territory_id not in existing_maps:
            continue
        existing_maps = existing_maps[territory_id].get("maps", {})
        new_maps = territory.get("maps", {})
        new_maps.update(existing_maps)
    json.dump(existing_maps, f, indent=4)

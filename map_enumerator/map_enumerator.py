import requests
import time
import json

BASE_URL = "https://xivapi.com"
TERRITORY_ENDPOINT = "/TerritoryType"
MAP_ENDPOINT = "/Map"

headers = {
    "User-Agent": "FFXIVTerritoryMapMapper/1.0"
}

result = {}

def get_json(endpoint, params=None):
    time.sleep(0.25)  # rate limit to be kind to the API
    response = requests.get(BASE_URL + endpoint, params=params, headers=headers)
    response.raise_for_status()
    return response.json()

# # Fetch all TerritoryTypes with PlaceName and Map data
# page = 1
# while True:
#     print(f'Fetching page {page} of territory data...')
#     data = get_json(TERRITORY_ENDPOINT, {
#         "page": page,
#         "columns": "ID,PlaceName.Name_en,MapTargetID",
#     })
    
#     for entry in data["Results"]:
#         territory_id = entry["ID"]
#         place_name = entry["PlaceName"]["Name_en"]
#         map_id = entry["MapTargetID"]

#         territory_obj = {
#             "en": place_name,
#             "maps": {}
#         }

#         if map_id:
#             try:
#                 map_data = get_json(f"{MAP_ENDPOINT}/{map_id}", {
#                     "columns": "ID,PlaceName.Name_en"
#                 })

#                 map_entry = {
#                     "en": map_data["PlaceName"]["Name_en"],
#                     "d": "...",
#                     "h": "...",
#                     "t": "..."
#                 }

#                 territory_obj["maps"][map_data["ID"]] = map_entry
#             except Exception as e:
#                 print(f"Failed to fetch map {map_id} for territory {territory_id}: {e}")

#         result[territory_id] = territory_obj

#     if not data["Pagination"]["PageNext"]:
#         break
#     page += 1

# Fetch all territories
territories = {}
page = 1
while True:
    data = get_json("/TerritoryType", {
        "page": page,
        "columns": "ID,PlaceName.Name_en"
    })
    print(f'Fetched page {page}/{data["Pagination"]["PageTotal"]} of territories data...')
    for e in data["Results"]:
        territories[e["ID"]] = {
            "en": e["PlaceName"]["Name_en"],
            "maps": {}
        }
    if not data["Pagination"]["PageNext"]:
        break
    page += 1

# Fetch all maps and group by territory
page = 1
while True:
    data = get_json("/Map", {
        "page": page,
        "columns": "ID,PlaceName.Name_en,TerritoryType.ID"
    })
    print(f'Fetched page {page}/{data["Pagination"]["PageTotal"]} of maps data...')
    for m in data["Results"]:
        tid = m["TerritoryType"]["ID"]
        if tid in territories:
            territories[tid]["maps"][m["ID"]] = {
                "en": m["PlaceName"]["Name_en"],
                "d": "...",
                "h": "...",
                "t": "..."
            }
    if not data["Pagination"]["PageNext"]:
        break
    page += 1


# Save or print output

with open("instances_data.json", "w") as f:
    json.dump(territories, f, indent=2)

print(json.dumps(territories, indent=2))

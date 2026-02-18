
import json
import time

file_path = r"d:\Projetos\poc_feature\docs\api\insomnia_antigravity_v1.json"

with open(file_path, 'r', encoding='utf-8') as f:
    data = json.load(f)

resources = data.get("resources", [])

# Find Workspace ID
workspace_id = next((r["_id"] for r in resources if r["_type"] == "workspace"), None)
if not workspace_id:
    # Fallback: try to find parent of fld_materials
    fld_materials = next((r for r in resources if r["_id"] == "fld_materials"), None)
    if fld_materials:
        workspace_id = fld_materials.get("parentId")

print(f"Workspace ID: {workspace_id}")

# Helper to check if request exists
def request_exists(name, parent_id):
    return any(r for r in resources if r["_type"] == "request" and r["name"] == name and r["parentId"] == parent_id)

new_requests = []
current_time = int(time.time() * 1000)

# 1. Get Unique Components (Materials)
if not request_exists("Get Unique Components", "fld_materials"):
    new_requests.append({
        "_id": "req_materials_get_components",
        "parentId": "fld_materials",
        "modified": current_time,
        "created": current_time,
        "url": "{{ _.base_url }}/api/v1/materials/components",
        "name": "Get Unique Components",
        "description": "Retrieve a list of all unique components found in materials.",
        "method": "GET",
        "body": {},
        "parameters": [],
        "headers": [],
        "authentication": {},
        "metaSortKey": -1707868650000,
        "isPrivate": False,
        "settingStoreCookies": True,
        "settingSendCookies": True,
        "settingDisableRenderRequestBody": False,
        "settingEncodeUrl": True,
        "settingRebuildPath": True,
        "settingFollowRedirects": "global",
        "_type": "request"
    })

# 2. Get All Prices (Costing)
if not request_exists("Get All Prices", "fld_costing"):
    new_requests.append({
        "_id": "req_costing_get_all_prices",
        "parentId": "fld_costing",
        "modified": current_time,
        "created": current_time,
        "url": "{{ _.base_url }}/api/v1/costing/prices",
        "name": "Get All Prices",
        "description": "Retrieve all component prices.",
        "method": "GET",
        "body": {},
        "parameters": [],
        "headers": [],
        "authentication": {},
        "metaSortKey": -1707868750000,
        "isPrivate": False,
        "settingStoreCookies": True,
        "settingSendCookies": True,
        "settingDisableRenderRequestBody": False,
        "settingEncodeUrl": True,
        "settingRebuildPath": True,
        "settingFollowRedirects": "global",
        "_type": "request"
    })

# 3. Get Prices Count (Costing)
if not request_exists("Get Prices Count", "fld_costing"):
    new_requests.append({
        "_id": "req_costing_get_prices_count",
        "parentId": "fld_costing",
        "modified": current_time,
        "created": current_time,
        "url": "{{ _.base_url }}/api/v1/costing/prices/count",
        "name": "Get Prices Count",
        "description": "Retrieve the total count of component prices.",
        "method": "GET",
        "body": {},
        "parameters": [],
        "headers": [],
        "authentication": {},
        "metaSortKey": -1707868780000,
        "isPrivate": False,
        "settingStoreCookies": True,
        "settingSendCookies": True,
        "settingDisableRenderRequestBody": False,
        "settingEncodeUrl": True,
        "settingRebuildPath": True,
        "settingFollowRedirects": "global",
        "_type": "request"
    })

# 4. Generate Prices (Populator)
if not request_exists("Generate Prices", "fld_populator"):
    new_requests.append({
        "_id": "req_populator_generate_prices",
        "parentId": "fld_populator",
        "modified": current_time,
        "created": current_time,
        "url": "{{ _.base_url }}/api/v1/populator/jobs",
        "name": "Generate Prices",
        "description": "Trigger a job to generate random component prices.",
        "method": "POST",
        "body": {
            "mimeType": "application/json",
            "text": "{\n\t\"target\": \"prices\",\n\t\"count\": 10\n}"
        },
        "parameters": [],
        "headers": [
            {
                "name": "Content-Type",
                "value": "application/json"
            }
        ],
        "authentication": {},
        "metaSortKey": -1707868700000,
        "isPrivate": False,
        "settingStoreCookies": True,
        "settingSendCookies": True,
        "settingDisableRenderRequestBody": False,
        "settingEncodeUrl": True,
        "settingRebuildPath": True,
        "settingFollowRedirects": "global",
        "_type": "request"
    })

if new_requests:
    resources.extend(new_requests)
    data["resources"] = resources
    
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2)
    
    print(f"Added {len(new_requests)} new requests.")
else:
    print("No new requests to add.")

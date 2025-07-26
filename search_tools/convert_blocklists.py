import json
import os
import cvr_api

def fix_world_objects(objs):
    for obj in objs:
        if "Name" in obj.keys():
            obj.update({"SearchType": "Name", "SearchPattern": obj["Name"]})
            obj.pop("Name")
        elif "IndexRange" in obj.keys():
            indexes = obj["IndexRange"]
            if "RangeExclusions" in obj.keys():
                for exclusion in obj["RangeExclusions"]:
                    indexes.append(exclusion)
            indexes_str = str(indexes)
            indexes_str = indexes_str.replace('[', '')
            indexes_str = indexes_str.replace(']', '')

            obj.update({"SearchType": "IndexRange", "SearchPattern": indexes_str})
            obj.pop("IndexRange")
        elif "Index" in obj.keys():
            obj.update({"SearchType": "Index", "SearchPattern": str(obj["Index"])})
            obj.pop("Index")
        if "Children" in obj.keys():
            fix_world_objects(obj["Children"])


if __name__ == "__main__":
    cvr_api.authenticate()
    for block_list, name in zip(
            [json.load(open(f"../BlockLists/{bl}", "r")) for bl in os.listdir("../BlockLists") if ".json" in bl],
            [bl for bl in os.listdir("../BlockLists") if ".json" in bl]):
        print(f"==== {name} ====")
        for world in block_list["Worlds"]:
            world_name = cvr_api.get_world(world["Id"])["name"]
            world.update({"Name": world_name})
            if "Objects" in world.keys():
                fix_world_objects(world["Objects"])
        json.dump(block_list, open(name, "w"), indent=2)

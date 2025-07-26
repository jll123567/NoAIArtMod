import requests
import json
import sys
import os

APIAddress = "https://api.abinteractive.net/1"
headers = {
    'Content-Type': 'application/json',
    'User-Agent': 'NoAIArt-SearchTools',
    'Platform': 'pc_standalone',
    'CompatibleVersions': '0,1,2'
}


def authenticate():
    auth_headers = {'Content-Type': 'application/json', 'User-Agent': 'NoAIArt-SearchTools'}
    payload = {'AuthType': 2}
    with open("login", 'r') as api_keyfile:
        lines = api_keyfile.read().splitlines()
        payload.update({'Username': lines[0], 'Password': lines[1]})
    token_request = requests.post(f"{APIAddress}/users/auth", json.dumps(payload), headers=auth_headers)
    headers.update({
        "Username": token_request.json()["data"]["username"],
        "AccessKey": token_request.json()["data"]["accessKey"]
    })
    return


def get_user_public_uploads(uid):
    worlds_request = requests.get(f"{APIAddress}/users/{uid}/worlds", headers=headers)
    avatars_request = requests.get(f"{APIAddress}/users/{uid}/avatars", headers=headers)
    props_request = requests.get(f"{APIAddress}/users/{uid}/spawnables", headers=headers)
    return {
        "worlds": worlds_request.json()["data"],
        "avatars": avatars_request.json()["data"],
        "props": props_request.json()["data"]
    }


def get_search_restults(term):
    request = requests.get(f"{APIAddress}/search/{term}", headers=headers).json()["data"]

    return {
        "worlds": [item for item in request if item["type"] == 'world'],
        "avatars": [item for item in request if item["type"] == 'avatar'],
        "props": [item for item in request if item["type"] == 'prop']
    }


def get_world_description(wid):
    request = requests.get(f"{APIAddress}/worlds/{wid}", headers=headers).json()["data"]
    return request["description"]


def get_avatar_description(aid):
    request = requests.get(f"{APIAddress}/avatars/{aid}", headers=headers).json()["data"]
    return request["description"]


def get_prop_description(pid):
    request = requests.get(f"{APIAddress}/spawnables/{pid}", headers=headers).json()["data"]
    return request["description"]


def filter_already_blocked(items):
    blocked_worlds, blocked_avatars, blocked_props = [], [], []
    block_lists = [json.load(open(f"../BlockLists/{bl}", "r")) for bl in os.listdir("../BlockLists") if ".json" in bl]
    for block_list in block_lists:
        if "Worlds" in block_list.keys():
            for world in block_list["Worlds"]:
                blocked_worlds.append(world["Id"])
        if "Avatars" in block_list.keys():
            for avatar in block_list["Avatars"]:
                blocked_avatars.append(avatar)
        if "Props" in block_list.keys():
            for prop in block_list["Props"]:
                blocked_props.append(prop)
    for id in reversed(range(len(items["worlds"]))):
        if items["worlds"][id]["id"] in blocked_worlds:
            items["worlds"].pop(id)
    for id in reversed(range(len(items["avatars"]))):
        if items["avatars"][id]["id"] in blocked_avatars:
            items["avatars"].pop(id)
    for id in reversed(range(len(items["props"]))):
        if items["props"][id]["id"] in blocked_props:
            items["props"].pop(id)
    return items


def generate_user_uploads_results(items):
    world_html = ""
    for world in items["worlds"]:
        desc = get_world_description(world['id'])
        world_html += f"      <hr><ul><li>id: {world['id']}</li><li>name: {world['name']}</li><li><img src=\"{world['imageUrl']}\"></li><li>{desc}</li></ul>\n"
    world_html = f"    <ul>\n{world_html}</ul>\n"
    avatar_html = ""
    for avatar in items["avatars"]:
        desc = get_avatar_description(avatar['id'])
        avatar_html += f"      <hr><ul><li>id: {avatar['id']}</li><li>name: {avatar['name']}</li><li><img src=\"{avatar['imageUrl']}\"></li><li>{desc}</li></ul>\n"
    avatar_html = f"    <ul>\n{avatar_html}</ul>\n"
    prop_html = ""
    for prop in items["props"]:
        desc = get_prop_description(prop['id'])
        prop_html += f"      <hr><ul><li>id: {prop['id']}</li><li>name: {prop['name']}</li><li><img src=\"{prop['imageUrl']}\"></li><li>{desc}</li></ul>\n"
    prop_html = f"    <ul>\n{prop_html}</ul>\n"
    full_page = f"<!DOCTYPE html>\n<html>\n  <body>\n    <h2>Worlds</h2>\n{world_html}    <h2>Avatars</h2>\n{avatar_html}    <h2>Props</h2>\n{prop_html}  </body>\n</html>"
    with open("results.html", "w", encoding='utf-8') as results_page:
        results_page.write(full_page)


if __name__ == "__main__":
    """Fill in the blanks and run."""
    authenticate()  # Put an email and password on separate lines for this in ./login
    # items = get_user_public_uploads("")
    items = get_search_restults(" AI ")
    items = filter_already_blocked(items)
    generate_user_uploads_results(items)

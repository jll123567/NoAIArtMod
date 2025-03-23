# No AI Art

A ChilloutVR mod to remove objects based on a block list.

## Installation
Check [Releases](https://github.com/jll123567/NoAIArtMod/releases/latest) for the latest `NoAIArt.dll` and put it in your mods folder in CVR's installation directory.

## Blocklists

Use a blocklist here by downloading it and putting it `<CVRInstall Directory>/UserData/NoAIArt`.
The list must end in a `.json` file extension.

You can write your own as follows...

Blocklists are json files where the root object has lists of `Worlds`, `Props`, and `Avatars` as well as an `UpdateURL`.

The update URL must match the following regular expression `^https:\/\/raw\.githubusercontent\.com\/.*\.json$`.
Basically: it must be a `raw.githubusercontent.com` link that ends with `.json`.

Props and Avatars are just lists of the prop/avatar GUIDs that you want to block.

A World is an object with an `Id`(The world's GUID), A `Skybox` property, and a list of `Objects`.
Each object needs either a `Name`(Name of game object) or an `Index`(Index of game object at its level in the hierarchy) .
Each object can have a list a `Children` objects to build out the heirarchy.
Each object can also have a `Behavior` that specifies what the mod should do with the object
Check [This](https://github.com/jll123567/NoAIArtMod/blob/main/BlockLists/AIList.json) blocklist for an example.

There are four available `Behavior`s:

 - `nothing`: Do nothing to this object. This is the default if you don't specifiy a behavior.
 - `delete`: Destroy this object (and it's children).
 - `no-render`: Destory this object's `MeshRenderer`s and `SkinnedMeshRenderer`s.
 - `change-material`: Change the materials of this object to the Default Standard material.

The `Skybox` property allows you to replace the skybox using the following options:

- `Untouched`: Do not change the skybox, the default if you don't specify a skybox change.
- `None`: Set the skybox to null, I.E. make it full black.
- `White`: An almost white.
- `Gray`: A middle gray.
- `Black`: An almost black.
- `DarkBlue`: The unity default blue when you don't specify a skybox.
- `Default`(or any name not used above): The default unity procedural skybox.

## Building
1. Clone this repo.
2. Install [NStrip](https://github.com/bbepis/NStrip) into this direcotry (or somewhere on PATH).
3. If CVR isn't installed in `C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR` then [Set your CVR folder environment variable](https://github.com/kafeijao/Kafe_CVR_Mods#set-cvr-folder-environment-variable).
4. Run copy_and_nstrip_dll.ps1 (in PowerShell).

(Instructions pulled from https://github.com/kafeijao/Kafe_CVR_Mods)

## Todo

- [x] Index Ranges
- [x] Specifiy a specific material slot for material replacement.
- [ ] Name Regex

## Disclaimer

This is independant of, not affiliated with, supported by, or approved by Alpha Blend Interactive.

Use at your own risk.

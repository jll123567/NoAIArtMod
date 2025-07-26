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

A World is an object with an `Id`(The world's GUID), Possibly a `Skybox` property, and a list of `Objects`.

The `Skybox` property allows you to replace the skybox using the following options:

- `Untouched`: Do not change the skybox, the default if you don't specify a skybox change.
- `None`: Set the skybox to null, I.E. make it full black.
- `White`: An almost white.
- `Gray`: A middle gray.
- `Black`: An almost black.
- `DarkBlue`: The unity default blue when you don't specify a skybox.
- `Default`(or any name not used above): The default unity procedural skybox.

Each object in the world's objects is specified with a `SearchType` and `SearchPattern`.
The pattern depends on the type, which can be:

- `Name`: Name of the game object.
- `Index`: Integer index (as a string) of the game object.
- `IndexRange`: String with the starting and ending indexes (inclusive) as well as excluded indicies of objects.
  - For example: removing objects 2 through 6 but not 3 or 5 would be an index range "2, 6, 3, 5".

The search only works on one level of the hirarchy of the scene at a time, starting at the scene root.
To remove a child of an object, you have to list the parent objects, then list the child with the `Children` property.

For each object the `Behavior` specifies what the mod should do with the object

There are four available `Behavior`s:

 - `nothing`: Do nothing to this object. This is the default if you don't specifiy a behavior.
 - `delete`: Destroy this object (and it's children).
 - `no-render`: Destory this object's `MeshRenderer`s and `SkinnedMeshRenderer`s.
 - `change-material`: Change the materials of this object to the Default Standard material.
   - You can specify `MaterialReplacementIndicies` as a list of the materials to replace on the renderer, so you don't replace everything.

Check [This](https://github.com/jll123567/NoAIArtMod/blob/main/BlockLists/AIList.json) blocklist for an example.



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

using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using ABI.CCK.Components;
using ABI_RC.Core.Savior;
using Newtonsoft.Json;
using ABI_RC.Core.Player;
using RTG;




namespace NoAIArt
{
    public class Core : MelonMod
    {
        private static readonly string ModDataFolder = Path.GetFullPath(Path.Combine("UserData", nameof(NoAIArt)));
        private static List<BlockList> BlockLists = new List<BlockList>();
        private static Material ReplacementMaterial = new Material(Shader.Find("Standard"));
        private static float lastPropBlock = Time.time;
        private static List<string> SeenBadAvis = new List<string>();

        public override void OnInitializeMelon()
        {
            LoadBlocklists();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if ((Input.GetKey(KeyCode.LeftControl)|| Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Equals))
            {
                LoadBlocklists();
            }
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Minus))
            {
                foreach (BlockList blockList in BlockLists)
                {
                    foreach (BlockedWorld blockedWorld in blockList.Worlds)
                    {
                        if (MetaPort.Instance.CurrentWorldId == blockedWorld.Id)
                        {
                            MelonLogger.Msg(System.Drawing.Color.Red, $"World in block list, removing blocked objects: {MetaPort.Instance.CurrentWorldId}");
                            if(blockedWorld.Skybox != "Untouched")
                            {
                                Material skyboxReplacement;
                                switch (blockedWorld.Skybox)
                                {
                                    case "None":
                                        RenderSettings.skybox = null;
                                        break;
                                    case "Black":
                                        skyboxReplacement = new Material(Shader.Find("Skybox/Cubemap"));
                                        skyboxReplacement.SetColor("_Tint", new Color(0.05f, 0.05f, 0.05f, 1f));
                                        RenderSettings.skybox = skyboxReplacement;
                                        break;
                                    case "Gray":
                                        skyboxReplacement = new Material(Shader.Find("Skybox/Cubemap"));
                                        skyboxReplacement.SetColor("_Tint", Color.gray);
                                        RenderSettings.skybox = skyboxReplacement;
                                        break;
                                    case "White":
                                        skyboxReplacement = new Material(Shader.Find("Skybox/Cubemap"));
                                        skyboxReplacement.SetColor("_Tint", new Color(0.9f, 0.9f, 0.9f, 1f));
                                        RenderSettings.skybox = skyboxReplacement;
                                        break;
                                    case "DarkBlue":
                                        skyboxReplacement = new Material(Shader.Find("Skybox/Cubemap"));
                                        skyboxReplacement.SetColor("_Tint", new Color(0.192f, 0.306f, 0.478f, 1f));
                                        RenderSettings.skybox = skyboxReplacement;
                                        break;
                                    default:
                                        skyboxReplacement = new Material(Shader.Find("Skybox/Procedural"));
                                        RenderSettings.skybox = skyboxReplacement;
                                        break;
                                }
                            }
                            foreach (BlockedWorldObject blockedObject in blockedWorld.Objects)
                            {
                                FindBlockedWorldObject(blockedObject);
                            }
                        }
                    }
                }
            }
            //if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Alpha0))
            //{
            //    DisplayObjects();
            //}
        }

        internal static void LoadBlocklists()
        {
            BlockLists = new List<BlockList>();
            int worldTally = 0, propTally = 0, avatarTally = 0;
            var blockListPaths = Directory.GetFiles(ModDataFolder, "*.json");
            foreach (var blockListPath in blockListPaths)
            {
                try
                {
                    string blockListContent = File.ReadAllText(blockListPath);
                    BlockList? deserealizedBlockList = JsonConvert.DeserializeObject<BlockList>(blockListContent);

                    if (deserealizedBlockList is null)
                    {
                        MelonLogger.Error($"Your Blocklist({blockListPath}) is empty.");
                        continue;
                    }



                    string pattern = @"^https:\/\/raw\.githubusercontent\.com\/.*\.json$";  // Updater
                    if (Regex.IsMatch(deserealizedBlockList.UpdateURL, pattern))  // Only update if pulling from a verified source.
                    {
                        string latestBlockList = "";
                        HttpClient httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "NoAIArtMod");
                        Task<string> request = httpClient.GetStringAsync(deserealizedBlockList.UpdateURL);
                        request.Wait();
                        latestBlockList = request.Result;
                        if (!blockListContent.Equals(latestBlockList))
                        {
                            MelonLogger.Msg($"Found update for blocklist({blockListPath}), writing.");
                            File.WriteAllText(blockListPath, latestBlockList);
                            deserealizedBlockList = JsonConvert.DeserializeObject<BlockList>(latestBlockList);
                        }
                    }
                    else if (!deserealizedBlockList.UpdateURL.Equals(""))
                    {
                        MelonLogger.Warning($"This blocklist ({blockListPath}) has a update URL that does not match\n {pattern}\nNot updating.");
                    }

                    BlockLists.Add(deserealizedBlockList);
                    worldTally += deserealizedBlockList.Worlds.Count;
                    propTally += deserealizedBlockList.Props.Count;
                    avatarTally += deserealizedBlockList.Avatars.Count;
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Your Blocklist({blockListPath}) is malformed. Specifics below:\n\n{e.Message}\n\n{File.ReadAllText(blockListPath)}");
                }

            }
            MelonLogger.Msg($"NoAiArt Initialized: Found {worldTally} world(s), {propTally} prop(s), and {avatarTally} avatars(s).");
        }

        internal static void FindBlockedWorldObject(BlockedWorldObject objectSpec, GameObject? parrent = null)
        {
            bool failed = true;
            if(parrent is null) // Check root of world scene.
            {
                if (objectSpec.SearchType.Equals("Index"))  // Get object by index (prefered)
                {
                    failed = false;
                    int index;                    
                    bool parse_pass = int.TryParse(objectSpec.SearchPattern, out index);
                    if (!parse_pass)
                    {
                        MelonLogger.Error($"Your int is malformed: {objectSpec.SearchPattern}");
                        return;
                    }
                    RemoveBlockedWorldObject(objectSpec, SceneManager.GetActiveScene().GetRootGameObjects()[index]);
                }
                else if (objectSpec.SearchType.Equals("IndexRange"))  // Using a range [a,b] possibly with exclusions [e1, ..., en].
                {
                    // Extract range info from pattern.
                    String[] rangeInfoS = objectSpec.SearchPattern.Split(',');
                    bool parsePass;
                    int rangeMin;
                    int rangeMax;
                    int[] rangeExceptions = new int[0];

                    parsePass = int.TryParse(rangeInfoS[0].Trim(), out rangeMin);
                    if (!parsePass) { MelonLogger.Error($"Bad range minimum: {rangeInfoS[0]}"); return; }
                    parsePass = int.TryParse(rangeInfoS[1].Trim(), out rangeMax);
                    if (!parsePass) { MelonLogger.Error($"Bad range maximum: {rangeInfoS[1]}"); return; }

                    if(rangeInfoS.Length > 2)
                    {
                        rangeExceptions = new int[rangeInfoS.Length - 2];
                        for (int i = 2; i < rangeInfoS.Length; i++)
                        {
                            parsePass = int.TryParse(rangeInfoS[i].Trim(), out rangeExceptions[i-2]);
                            if (!parsePass) { MelonLogger.Error($"Bad range execption: {rangeInfoS[i]}"); return; }

                        }
                    }
                    
                    // Parse objects
                    for (int i = rangeMax; i >= rangeMin; i--)
                    {
                        bool skip = false;
                        foreach(int e in rangeExceptions)
                        {
                            if (e == i)
                            {
                                skip = true;
                                break;
                            }
                        }
                        if (!skip)
                        {
                            failed = false;
                            RemoveBlockedWorldObject(objectSpec, SceneManager.GetActiveScene().GetRootGameObjects()[i]);
                        }
                    }
                }
                else if (objectSpec.SearchType.Equals("Name")){  // Get object by name.
                    foreach (GameObject g in SceneManager.GetActiveScene().GetRootGameObjects())  // GameObject.Find is too broad and Transform.Find needs a transform to start with.
                    {
                        if (g.name == objectSpec.SearchPattern)
                        {
                            failed = false;
                            RemoveBlockedWorldObject(objectSpec, g);
                            break;
                        }
                    } 
                }
                else
                {
                    MelonLogger.Error($"Search type is invalid: {objectSpec.SearchType}.\n Use either \"Name\", \"Index\", or \"IndexRange\".");
                    return;
                }
            }
            else  // Check parrent object.
            {
                try
                {
                    if (objectSpec.SearchType.Equals("Index")) // Get object by index (prefered)
                    {
                        failed = false;
                        int index;
                        bool parse_pass = int.TryParse(objectSpec.SearchPattern, out index);
                        if (!parse_pass)
                        {
                            MelonLogger.Error($"Your int is malformed: {objectSpec.SearchPattern}");
                            return;
                        }
                        RemoveBlockedWorldObject(objectSpec, parrent.transform.GetChild(index).gameObject);
                    }
                    else if (objectSpec.SearchType.Equals("IndexRange"))  // Using a range [a,b] possibly with exclusions [e1, ..., en].
                    {
                        // Extract range info from pattern.
                        String[] rangeInfoS = objectSpec.SearchPattern.Split(',');
                        bool parsePass;
                        int rangeMin;
                        int rangeMax;
                        int[] rangeExceptions = new int[0];

                        parsePass = int.TryParse(rangeInfoS[0].Trim(), out rangeMin);
                        if (!parsePass) { MelonLogger.Error($"Bad range minimum: {rangeInfoS[0]}"); return; }
                        parsePass = int.TryParse(rangeInfoS[1].Trim(), out rangeMax);
                        if (!parsePass) { MelonLogger.Error($"Bad range maximum: {rangeInfoS[1]}"); return; }

                        if (rangeInfoS.Length > 2)
                        {
                            rangeExceptions = new int[rangeInfoS.Length - 2];
                            for (int i = 2; i < rangeInfoS.Length; i++)
                            {
                                parsePass = int.TryParse(rangeInfoS[i].Trim(), out rangeExceptions[i - 2]);
                                if (!parsePass) { MelonLogger.Error($"Bad range excpetion: {rangeInfoS[i]}"); return; }

                            }
                        }

                        for (int i = rangeMax; i >= rangeMin; i--)
                        {
                            bool skip = false;
                            foreach (int e in rangeExceptions)
                            {
                                if (e == i)
                                {
                                    skip = true;
                                    break;
                                }
                            }
                            if (!skip)
                            {
                                GameObject? result;
                                try
                                {
                                    failed = false;
                                    RemoveBlockedWorldObject(objectSpec, parrent.transform.GetChild(i).gameObject);
                                }
                                catch(UnityEngine.UnityException e)
                                {
                                    failed = true;
                                    MelonLogger.Warning($"Your IndexRange under {parrent.name} went out of bounds.\n Your blocklist probably has nested ranges that are too big...");
                                }
                                
                            }
                        }
                    }
                    else if (objectSpec.SearchType.Equals("Name")) // Get object by name.
                    {
                        GameObject result = parrent.transform.Find(objectSpec.SearchPattern).gameObject;
                        if (result != null)
                        {
                            failed = false;
                            RemoveBlockedWorldObject(objectSpec, result);  // Find will only check current level. (unless object has a / in name. Hopefully world creator was not insane!)
                        }
                    }
                    else
                    {
                        MelonLogger.Error($"Search type is invalid: {objectSpec.SearchType}.\n Use either \"Name\", \"Index\", or \"IndexRange\".");
                        return;
                    }

                }
                catch(Exception e)
                {
                    MelonLogger.Error(e);
                }
            }
            if(failed)
            {
                MelonLogger.Error($"Could not find object. SearchType:{objectSpec.SearchType}, SearchPattern:{objectSpec.SearchPattern}, Parrent: {parrent?.name}");
            }
        }

        internal static void RemoveBlockedWorldObject(BlockedWorldObject objectSpec, GameObject current)
        {
            if (objectSpec.Behavior == "delete")
            {
                UnityEngine.Object.Destroy(current); // Delete the current game object. NOT PARSING CHILDREN!!!
            }
            else  // Other options require parsing spec children.
            {
                if (objectSpec.Behavior == "no-render")  // Remove mesh renderers.
                {
                    foreach (MeshRenderer m in current.GetComponents<MeshRenderer>())
                    {
                        UnityEngine.Object.Destroy(m);
                    }
                    foreach (SkinnedMeshRenderer sm in current.GetComponents<SkinnedMeshRenderer>())
                    {
                        UnityEngine.Object.Destroy(sm);
                    }
                }
                else if (objectSpec.Behavior == "change-material")  // Change mesh renderer materials.
                {
                    foreach (MeshRenderer m in current.GetComponents<MeshRenderer>())
                    {
                        Material[] newMats = m.materials;
                        if (objectSpec.MaterialReplacementIndicies.Length == 0)  // Replace all.
                        {
                            m.material = ReplacementMaterial;
                            for (int i = 0; i < m.materials.Length; i++)
                            {
                                newMats[i] = ReplacementMaterial;
                            }
                        }
                        else // Replace some.
                        {
                            foreach (int i in objectSpec.MaterialReplacementIndicies)
                            {
                                newMats[i] = ReplacementMaterial;
                            }
                        }
                        m.materials = newMats;
                    }
                    foreach (SkinnedMeshRenderer sm in current.GetComponents<SkinnedMeshRenderer>())
                    {
                        sm.material = ReplacementMaterial;
                        for (int i = 0; i < sm.materials.Length; i++)
                        {
                            sm.materials[i] = ReplacementMaterial;
                        }
                    }
                }
                else if (objectSpec.Behavior == "move")
                {
                    Vector3 moveVector3 = new Vector3(objectSpec.MoveVector[0], objectSpec.MoveVector[1], objectSpec.MoveVector[2]);
                    current.transform.localPosition += moveVector3;
                }

                foreach (BlockedWorldObject childSpec in objectSpec.Children)  // Parse child specs recursively.
                {
                    FindBlockedWorldObject(childSpec, current);
                }
            }
        }

        internal void DisplayObjects()
        {
            MelonLogger.Msg(System.Drawing.Color.GreenYellow, "===============Dumping world/avatar/prop ids==============");

            MelonLogger.Msg(System.Drawing.Color.GreenYellow, $"World: {MetaPort.Instance.CurrentWorldId}");
            MelonLogger.Msg(System.Drawing.Color.GreenYellow, "                      Avatars");
            MelonLogger.Msg(System.Drawing.Color.YellowGreen, $"You are using {PlayerSetup.Instance._avatar.GetComponent<CVRAssetInfo>().objectId}");
            foreach (CVRPlayerEntity playerEntity in CVRPlayerManager.Instance.NetworkPlayers)
            {
                MelonLogger.Msg(System.Drawing.Color.GreenYellow, $"{playerEntity.Username} using {playerEntity.AvatarId}");
            }
            

            MelonLogger.Msg(System.Drawing.Color.GreenYellow, "                       Props");
            foreach (GameObject potentialProp in SceneManager.GetSceneAt(1).GetRootGameObjects())
            {
                if (potentialProp.name.StartsWith("p"))
                {
                    GameObject prop = potentialProp.GetAllChildren()[0];
                    string propId = prop.GetComponent<CVRAssetInfo>().objectId;
                    float distance = Vector3.Distance(prop.transform.position, PlayerSetup.Instance.GetHipBone().position);
                    MelonLogger.Msg(System.Drawing.Color.GreenYellow, $"Prop {propId}: {distance}m");

                }
            }
            MelonLogger.Msg(System.Drawing.Color.GreenYellow, "====================================================");
        }

        [HarmonyPatch]
        internal class HarmonyPatches
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.Start))]
            public static void After_CVRWorldStart()
            {
                foreach(BlockList blockList in BlockLists)
                {
                    foreach(BlockedWorld blockedWorld in blockList.Worlds)
                    {
                        if(MetaPort.Instance.CurrentWorldId == blockedWorld.Id)
                        {
                            MelonLogger.Msg(System.Drawing.Color.Red, $"World in block list, removing blocked objects: {MetaPort.Instance.CurrentWorldId}");
                            if (blockedWorld.Skybox != "Untouched")
                            {
                                Material skyboxReplacement;
                                switch (blockedWorld.Skybox)
                                {
                                    case "None":
                                        RenderSettings.skybox = null;
                                        break;
                                    case "Black":
                                        skyboxReplacement = new Material(Shader.Find("Skybox/Cubemap"));
                                        skyboxReplacement.SetColor("_Tint", new Color(0.05f, 0.05f, 0.05f, 1f));
                                        RenderSettings.skybox = skyboxReplacement;
                                        break;
                                    case "Gray":
                                        skyboxReplacement = new Material(Shader.Find("Skybox/Cubemap"));
                                        skyboxReplacement.SetColor("_Tint", Color.gray);
                                        RenderSettings.skybox = skyboxReplacement;
                                        break;
                                    case "White":
                                        skyboxReplacement = new Material(Shader.Find("Skybox/Cubemap"));
                                        skyboxReplacement.SetColor("_Tint", new Color(0.9f, 0.9f, 0.9f, 1f));
                                        RenderSettings.skybox = skyboxReplacement;
                                        break;
                                    case "DarkBlue":
                                        skyboxReplacement = new Material(Shader.Find("Skybox/Cubemap"));
                                        skyboxReplacement.SetColor("_Tint", new Color(0.192f, 0.306f, 0.478f, 1f));
                                        RenderSettings.skybox = skyboxReplacement;
                                        break;
                                    default:
                                        skyboxReplacement = new Material(Shader.Find("Skybox/Procedural"));
                                        RenderSettings.skybox = skyboxReplacement;
                                        break;
                                }
                            }
                            foreach (BlockedWorldObject blockedObject in blockedWorld.Objects)
                            {
                                FindBlockedWorldObject(blockedObject);
                            }
                        }
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CVRSelfModerationManager), nameof(CVRSelfModerationManager.GetPropVisibility))]
            internal static bool OnGetPropVisibility(string userId, string propId, out bool wasForceHidden, out bool wasForceShown)
            {
                wasForceShown = false;
                wasForceHidden = false;

                string propGUID = propId.StartsWith("p+") ? propId.Substring(2) : propId;  // From https://github.com/Nirv-git/CVRMods-Nirv/blob/main/WorldPropListMod/HarmonyP.cs
                if (propGUID.IndexOf('~') != -1)
                    propGUID = propGUID.Substring(0, propGUID.IndexOf('~'));


                foreach (BlockList bl in BlockLists)
                {
                    foreach (string blockedPropId in bl.Props)
                    {
                        if(propGUID == blockedPropId)
                        {
                            MelonLogger.Msg(System.Drawing.Color.Red, $"Prop in block list, blocking: {propGUID}");
                            wasForceHidden = true;
                            return false;
                        }
                    }
                }
                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CVRSelfModerationManager), nameof(CVRSelfModerationManager.GetAvatarVisibility))]
            internal static bool GetAvatarVisibility(string userId, string avatarId, out bool wasForceHidden, out bool wasForceShown)
            {
                wasForceHidden = false;
                wasForceShown = false;

                string avatarGUID = avatarId.StartsWith("a+") ? avatarId.Substring(2) : avatarId;  // Modified from https://github.com/Nirv-git/CVRMods-Nirv/blob/main/WorldPropListMod/HarmonyP.cs
                if (avatarGUID.IndexOf('~') != -1)
                    avatarGUID = avatarGUID.Substring(0, avatarGUID.IndexOf('~'));

                foreach (BlockList bl in BlockLists)
                {
                    foreach (string blockedAvatarId in bl.Avatars)
                    {
                        
                        if (avatarGUID == blockedAvatarId && !SeenBadAvis.Contains(avatarGUID))
                        {
                            MelonLogger.Msg(System.Drawing.Color.Red, $"Avatar in block list, blocking: {avatarGUID}");
                            wasForceHidden = true;
                            SeenBadAvis.Add(avatarGUID);
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        internal class BlockList
        {
            public List<BlockedWorld> Worlds = new List<BlockedWorld>();
            public List<string> Avatars = new List<string>();
            public List<string> Props = new List<string>();
            public string Comment { get; } = "";
            public string UpdateURL { get; set; } = "";
        }

        internal class BlockedWorld
        {
            public string Id { get; set; } = "";
            public List<BlockedWorldObject> Objects = new List<BlockedWorldObject>();
            public string Name { get; set; } = ""; // Optional to give the name of the world.
            public string Skybox { get; set; } = "Untouched";

        }
        
        internal class BlockedWorldObject
        {
            public string SearchType { get; set; } = "";  // Name, Index, IndexRange
            public string SearchPattern { get; set; } = ""; // The name, an int (index), a list of comma seperated ints.
            public string Name { get; set; } = "";  // Just for commenting lol.
            public int[] MaterialReplacementIndicies { get; set; } = new int[0];
            public float[] MoveVector { get; set; } = new float[3];
            public List<BlockedWorldObject> Children = new List<BlockedWorldObject>();
            public string Behavior { get; set; } = "nothing";
        }


    }
}
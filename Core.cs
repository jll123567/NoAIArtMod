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
using Aura2API;
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
                if (objectSpec.Index >= 0)  // Get object by index (prefered)
                {
                    failed = false;
                    RemoveBlockedWorldObject(objectSpec, SceneManager.GetActiveScene().GetRootGameObjects()[objectSpec.Index]);
                }
                else if (objectSpec.IndexRange.Length == 2)  // Using a range [a,b] possibly with exclusions [e1, ..., en].
                {
                    for (int i = objectSpec.IndexRange[1]; i >= objectSpec.IndexRange[0]; i--)
                    {
                        bool skip = false;
                        foreach(int e in objectSpec.RangeExclusions)
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
                else {  // Get object by name.
                    foreach (GameObject g in SceneManager.GetActiveScene().GetRootGameObjects())  // GameObject.Find is too broad and Transform.Find needs a transform to start with.
                    {
                        if(g.name == objectSpec.Name)
                        {
                            failed = false;
                            RemoveBlockedWorldObject(objectSpec, g);
                            break;
                        }
                    } 
                }
            }
            else  // Check parrent object.
            {
                try
                {
                    if (objectSpec.Index >= 0) // Get object by index (prefered)
                    {
                        failed = false;
                        RemoveBlockedWorldObject(objectSpec, parrent.transform.GetChild(objectSpec.Index).gameObject);
                    }
                    else if (objectSpec.IndexRange.Length == 2)  // Using a range [a,b] possibly with exclusions [e1, ..., en].
                    {
                        for (int i = objectSpec.IndexRange[1]; i >= objectSpec.IndexRange[0]; i--)
                        {
                            bool skip = false;
                            foreach (int e in objectSpec.RangeExclusions)
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
                    else
                    {
                        GameObject result = parrent.transform.Find(objectSpec.Name).gameObject;
                        if (result != null)
                        {
                            failed = false;
                            RemoveBlockedWorldObject(objectSpec, result);  // Find will only check current level. (unless object has a / in name. Hopefully world creator was not insane!)
                        }
                    }
                }
                catch(Exception e)
                {
                    MelonLogger.Error(e);
                }
            }
            if(failed)
            {
                MelonLogger.Error($"Could not find object. Name:{objectSpec.Name}, Index:{objectSpec.Index}, IndexRange: {objectSpec.IndexRange.Length == 2}, Parrent: {parrent?.name}");
            }
        }

        internal static void RemoveBlockedWorldObject(BlockedWorldObject objectSpec, GameObject current)
        {
            if (objectSpec.Behavior == "delete")
            {
                current.Destroy(); // Delete the current game object. NOT PARSING CHILDREN!!!
            }
            else  // Other options require parsing spec children.
            {
                if (objectSpec.Behavior == "no-render")  // Remove mesh renderers.
                {
                    foreach (MeshRenderer m in current.GetComponents<MeshRenderer>())
                    {
                        m.Destroy();
                    }
                    foreach (SkinnedMeshRenderer sm in current.GetComponents<SkinnedMeshRenderer>())
                    {
                        sm.Destroy();
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
            public string UpdateURL { get; set; } = "";
        }

        internal class BlockedWorld
        {
            public string Id { get; set; } = "";
            public List<BlockedWorldObject> Objects = new List<BlockedWorldObject>();
            public string Skybox { get; set; } = "Untouched";

        }
        
        internal class BlockedWorldObject
        {
            public string Name { get; set; } = "";
            public int Index { get; set; } = -1;
            public int[] IndexRange { get; set; } = new int[0];
            public int[] RangeExclusions { get; set; } = new int[0];
            public int[] MaterialReplacementIndicies { get; set; } = new int[0];
            public float[] MoveVector { get; set; } = new float[3];
            public List<BlockedWorldObject> Children = new List<BlockedWorldObject>();
            public string Behavior { get; set; } = "nothing";
        }


    }
}
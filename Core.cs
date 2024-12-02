using MelonLoader;
using HarmonyLib;
using UnityEngine;
using ABI.CCK.Components;
using ABI_RC.Core.Savior;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using Aura2API;
using System;
using System.Text;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;



namespace NoAIArt
{
    public class Core : MelonMod
    {
        private static readonly string ModDataFolder = Path.GetFullPath(Path.Combine("UserData", nameof(NoAIArt)));
        private static List<BlockList> BlockLists = new List<BlockList>();
        private static Material ReplacementMaterial = new Material(Shader.Find("Standard"));
        private static float lastPropBlock = Time.time;

        public override void OnInitializeMelon()
        {
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
                    else if (!deserealizedBlockList.UpdateURL.Equals("")){
                        MelonLogger.Warning($"This blocklist ({blockListPath}) has a update URL that does not match\n {pattern}\nNot updating.");
                    }

                    BlockLists.Add(deserealizedBlockList);
                    worldTally += deserealizedBlockList.Worlds.Count;
                    propTally += deserealizedBlockList.Props.Count;
                    avatarTally += deserealizedBlockList.Avatars.Count;
                }
                catch(Exception e)
                {
                    MelonLogger.Error($"Your Blocklist({blockListPath}) is malformed. Specifics below:\n\n{e.Message}\n\n{File.ReadAllText(blockListPath)}");
                }

            }
            LoggerInstance.Msg($"NoAiArt Initialized: Found {worldTally} world(s), {propTally} prop(s), and {avatarTally} avatars(s).");
            
        }

        internal static void RemoveBlockedWorldObject(BlockedWorldObject objectSpec, GameObject? parrent = null)
        {
            GameObject? current = null;
            if(parrent is null) // Check root of world scene.
            {
                if (objectSpec.Index >= 0)  // Get object by index (prefered)
                {
                    current = SceneManager.GetActiveScene().GetRootGameObjects()[objectSpec.Index];
                }
                else {  // Get object by name.
                    foreach (GameObject g in SceneManager.GetActiveScene().GetRootGameObjects())  // GameObject.Find is too broad and Transform.Find needs a transform to start with.
                    {
                        if(g.name == objectSpec.Name)
                        {
                            current = g;
                            break;
                        }
                    } 
                }
            }
            else  // Check parrent object.
            {
                if(objectSpec.Index >= 0) // Get object by index (prefered)
                {
                    current = parrent.transform.GetChild(objectSpec.Index).gameObject;
                }
                else
                {
                    current = parrent.transform.Find(objectSpec.Name).gameObject;  // Find will only check current level. (unless object has a / in name. Hopefully world creator was not insane!)
                }
            }
            if(current is null)
            {
                MelonLogger.Error($"Could not find object. Name:{objectSpec.Name}, Index:{objectSpec.Index}, Parrent: {parrent?.name}");
                return;
            }

            if(objectSpec.Behavior == "delete")
            {
                current.Destroy(); // Delete the current game object. NOT PARSING CHILDREN!!!
            }
            else  // Other options require parsing spec children.
            {
                if(objectSpec.Behavior == "no-render")  // Remove mesh renderers.
                {
                    foreach(MeshRenderer m in current.GetComponents<MeshRenderer>())
                    {
                        m.Destroy();
                    }
                    foreach(SkinnedMeshRenderer sm in current.GetComponents<SkinnedMeshRenderer>())
                    {
                        sm.Destroy();
                    }
                }
                else if(objectSpec.Behavior == "change-material")  // Change mesh renderer materials.
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

                foreach(BlockedWorldObject childSpec in objectSpec.Children)  // Parse child specs recursively.
                {
                    RemoveBlockedWorldObject(childSpec, current);
                }
            }
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
                                RemoveBlockedWorldObject(blockedObject);
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
                        if (avatarGUID == blockedAvatarId)
                        {
                            MelonLogger.Msg(System.Drawing.Color.Red, $"Avatar in block list, blocking: {avatarGUID}");
                            wasForceHidden = true;
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

        }
        
        internal class BlockedWorldObject
        {
            public string Name { get; set; } = "";
            public int Index { get; set; } = -1;
            public int[] MaterialReplacementIndicies { get; set; } = new int[0];
            public List<BlockedWorldObject> Children = new List<BlockedWorldObject>();
            public string Behavior { get; set; } = "nothing";
        }


    }
}
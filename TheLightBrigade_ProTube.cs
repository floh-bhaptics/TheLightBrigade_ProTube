using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using HarmonyLib;
using LB;
using UnityEngine;
using Unity.Mathematics;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Text.Json;

[assembly: MelonInfo(typeof(TheLightBrigade_ProTube.TheLightBrigade_ProTube), "TheLightBrigade_ProTube", "1.0.0", "Florian Fahrenberger")]
[assembly: MelonGame("Funktronic Labs", "The Light Brigade")]


namespace TheLightBrigade_ProTube
{
    public class TheLightBrigade_ProTube: MelonMod
    {
        public static string configPath = Directory.GetCurrentDirectory() + "\\UserData\\";
        public static bool dualWield = false;
        public static bool rightHanded = true;

        public override void OnInitializeMelon()
        {
            InitializeProTube();
        }

        public static void saveChannel(string channelName, string proTubeName)
        {
            string fileName = configPath + channelName + ".pro";
            File.WriteAllText(fileName, proTubeName, Encoding.UTF8);
        }

        public static string readChannel(string channelName)
        {
            string fileName = configPath + channelName + ".pro";
            if (!File.Exists(fileName)) return "";
            return File.ReadAllText(fileName, Encoding.UTF8);
        }

        public static void dualWieldSort()
        {
            //MelonLogger.Msg("Channels: " + ForceTubeVRInterface.ListChannels());
            JsonDocument doc = JsonDocument.Parse(ForceTubeVRInterface.ListChannels());
            JsonElement pistol1 = doc.RootElement.GetProperty("channels").GetProperty("pistol1");
            JsonElement pistol2 = doc.RootElement.GetProperty("channels").GetProperty("pistol2");
            if ((pistol1.GetArrayLength() > 0) && (pistol2.GetArrayLength() > 0))
            {
                dualWield = true;
                MelonLogger.Msg("Two ProTube devices detected, player is dual wielding.");
                if ((readChannel("rightHand") == "") || (readChannel("leftHand") == ""))
                {
                    MelonLogger.Msg("No configuration files found, saving current right and left hand pistols.");
                    saveChannel("rightHand", pistol1[0].GetProperty("name").ToString());
                    saveChannel("leftHand", pistol2[0].GetProperty("name").ToString());
                }
                else
                {
                    string rightHand = readChannel("rightHand");
                    string leftHand = readChannel("leftHand");
                    MelonLogger.Msg("Found and loaded configuration. Right hand: " + rightHand + ", Left hand: " + leftHand);
                    ForceTubeVRInterface.ClearChannel(4);
                    ForceTubeVRInterface.ClearChannel(5);
                    ForceTubeVRInterface.AddToChannel(4, rightHand);
                    ForceTubeVRInterface.AddToChannel(5, leftHand);
                }
            }
        }

        private async void InitializeProTube()
        {
            MelonLogger.Msg("Initializing ProTube gear...");
            ForceTubeVRInterface.InitAsync(true);
            Thread.Sleep(10000);
            dualWieldSort();
        }

        [HarmonyPatch(typeof(Weapon_Rifle), "TryFire", new Type[] {  })]
        public class bhaptics_RifleFire
        {
            [HarmonyPrefix]
            public static void Prefix(Weapon_Rifle __instance, bool ___boltOpenState, float ___nextShot, bool ___hammerOpenState)
            {
                if (___boltOpenState) { return; }
                if (__instance.TypeOfWeapon != WeaponType.Pistol)
                    if ((UnityEngine.Object)__instance.nodeHammer != (UnityEngine.Object)null && ___hammerOpenState) { return; }
                if ((BaseConfig)__instance.chamber == (BaseConfig)null || __instance.chamberSpent) { return; }
                bool isRight = __instance.grabTrigger.gripController.IsRightController();
                bool twoHanded = false;
                if ((UnityEngine.Object)__instance.grabBarrel != (UnityEngine.Object)null)
                    if ((UnityEngine.Object)__instance.grabBarrel.gripController != (UnityEngine.Object)null)
                        twoHanded = true;
                //twoHanded = __instance.grabTrigger.alternateGrabAlso;
                //twoHanded = (__instance.grabBarrel != null);
                ForceTubeVRChannel myChannel = ForceTubeVRChannel.pistol1;
                ForceTubeVRChannel secondaryChannel = ForceTubeVRChannel.pistol2;
                if (!isRight) { myChannel = ForceTubeVRChannel.pistol2; secondaryChannel = ForceTubeVRChannel.pistol1; }
                if (twoHanded) ForceTubeVRInterface.Kick(120, secondaryChannel);
                if (twoHanded) { ForceTubeVRInterface.Shoot(230, 150, 30f, myChannel); }
                else ForceTubeVRInterface.Kick(230, myChannel);
            }
        }

        [HarmonyPatch(typeof(Weapon_Wand), "OnHeldTriggerRelease", new Type[] { typeof(XRController) })]
        public class bhaptics_CastSpell
        {
            [HarmonyPostfix]
            public static void Postfix(Weapon_Wand __instance, XRController controller)
            {
                bool isRight = controller.IsRightController();
                ForceTubeVRChannel myChannel = ForceTubeVRChannel.pistol1;
                if (!isRight) myChannel = ForceTubeVRChannel.pistol2;
                ForceTubeVRInterface.Rumble(250, 300f, myChannel);
            }
        }

        [HarmonyPatch(typeof(InventorySlot), "OnStoreItemFX", new Type[] {  })]
        public class bhaptics_StoreInventory
        {
            [HarmonyPostfix]
            public static void Postfix(InventorySlot __instance)
            {
                if (__instance.inventorySlotType == InventorySlotType.Ammo)
                {
                    ForceTubeVRChannel myChannel = ForceTubeVRChannel.pistol1;
                    if (!rightHanded) myChannel = ForceTubeVRChannel.pistol2;
                    ForceTubeVRInterface.Rumble(150, 40f, myChannel);
                }
            }
        }

        [HarmonyPatch(typeof(InventorySlot), "OnUnstoreItemFX", new Type[] { })]
        public class bhaptics_ReceiveInventory
        {
            [HarmonyPostfix]
            public static void Postfix(InventorySlot __instance)
            {
                if (__instance.inventorySlotType == InventorySlotType.Ammo)
                {
                    ForceTubeVRChannel myChannel = ForceTubeVRChannel.pistol1;
                    if (!rightHanded) myChannel = ForceTubeVRChannel.pistol2;
                    ForceTubeVRInterface.Rumble(150, 40f, myChannel);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryRoot), "SetHandednessPoses", new Type[] { typeof(Handedness) })]
        public class bhaptics_SetHandedness
        {
            [HarmonyPostfix]
            public static void Postfix(Handedness handedness)
            {
                rightHanded = (handedness == Handedness.Right);
            }
        }

    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace AlterSpawner
{
    [BepInPlugin(PluginId, PluginName, Version)]
    public class AlterSpawner : BaseUnityPlugin
    {
        public const string Version = "1.0.2";
        private const string PluginId = "azumatt.AlterSpawner";
        public const string Author = "Azumatt";
        public const string PluginName = "AlterSpawner";
        private static GameObject _alterSpawner;

        private readonly ConfigSync _configSync = new ConfigSync(PluginId)
            {DisplayName = PluginName, CurrentVersion = Version, MinimumRequiredVersion = Version};

        private readonly Dictionary<string, ConfigEntry<string>> _mLocalizedStrings =
            new Dictionary<string, ConfigEntry<string>>();

        private Harmony _harmony;

        private ConfigFile _localizationFile;

        public static AlterSpawner Instance { get; private set; }

        /* Tried this about 20 ways, for some reason items are never taken from the inventory.*/


        private void Awake()
        {
            _serverConfigLocked = Config("General", "Force Server Config", true, "Force Server Config");
            _configSync.AddLockingConfigEntry(_serverConfigLocked);
            _nexusID = Config("General", "NexusID",
                1398, /* Keeping this value up to date as current latest is 1397 on nexus */
                new ConfigDescription("Nexus mod ID for updates", null, new ConfigurationManagerAttributes()), false);


            /* BOSS PREFAB */
            _bossPrefab = Config("Prefab", "Spawned Prefab", "DragonEgg", "Prefab that will spawn when alter is used");
            _itemPrefab = Config("Prefab", "Item Prefab", "DragonEgg",
                "Prefab item that will consume when alter is used");
            _bossItems = Config("Prefab", "Number of Consumed Items", 2,
                "Prefab item that will spawn when alter is used");
            //spawnOffset = config("Prefab", "SpawnOffset", 1.0f, "Prefab item that will spawn when alter is used");
            _itemStandPrefix = Config("Prefab", "SpawnOffset", "goblinking_totemholder", "Item stand prefab");
            _useItemStands = Config("Prefab", "Should use Item Stands", true, "Should the alter use item stands");
            _bossItem = Config("Prefab", "SpawnOffset", "DragonEgg", "Item stand prefab");


            _localizationFile =
                new ConfigFile(
                    Path.Combine(Path.GetDirectoryName(base.Config.ConfigFilePath) ?? string.Empty,
                        PluginId + ".Localization.cfg"), false);

            LoadAssets();

            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginId);
            Localize();
        }

        private void OnDestroy()
        {
            _localizationFile.Save();
            _harmony?.UnpatchSelf();
        }

        private static void TryRegisterFabs(ZNetScene zNetScene)
        {
            if (zNetScene == null || zNetScene.m_prefabs == null || zNetScene.m_prefabs.Count <= 0) return;
            zNetScene.m_prefabs.Add(_alterSpawner);
        }

        private static AssetBundle GetAssetBundleFromResources(string filename)
        {
            var execAssembly = Assembly.GetExecutingAssembly();
            var resourceName = execAssembly.GetManifestResourceNames()
                .Single(str => str.EndsWith(filename));

            using (var stream = execAssembly.GetManifestResourceStream(resourceName))
            {
                return AssetBundle.LoadFromStream(stream);
            }
        }

        private static void LoadAssets()
        {
            var assetBundle = GetAssetBundleFromResources("prefab_offer_spawner");
            _alterSpawner = assetBundle.LoadAsset<GameObject>("Prefab_offer_spawner");
            assetBundle?.Unload(false);
        }

        private void Localize()
        {
            LocalizeWord("prop_offer_spawner", "Summoning Alter");
            LocalizeWord("prop_offer_spawner_usetext", "Interact to spawn server defined prefab");
        }

        private string LocalizeWord(string key, string val)
        {
            if (_mLocalizedStrings.ContainsKey(key)) return $"${key}";
            var loc = Localization.instance;
            var langSection = loc.GetSelectedLanguage();
            var configEntry = _localizationFile.Bind(langSection, key, val);
            Localization.instance.AddWord(key, configEntry.Value);
            _mLocalizedStrings.Add(key, configEntry);

            return $"${key}";
        }


        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        public static class AlterZNetSceneAwakePatch
        {
            public static bool Prefix(ZNetScene __instance)
            {
                TryRegisterFabs(__instance);
                return true;
            }
        }

        [HarmonyPatch(typeof(OfferingBowl), "Awake")]
        public static class AlterOfferBowlAwake
        {
            public static void Prefix(OfferingBowl __instance)
            {
                if (__instance == null) return;
                if (__instance.m_name.ToLower().Contains("offer") || __instance.name.ToLower().Contains("offer"))
                {
                    var prefabSpawned = ZNetScene.instance.GetPrefab(_bossPrefab.Value).gameObject;
                    __instance.m_bossPrefab = prefabSpawned;
                    __instance.m_itemPrefab =
                        ZNetScene.instance.GetPrefab(_itemPrefab.Value).GetComponent<ItemDrop>();
                    __instance.m_bossItems = _bossItems.Value;
                    //__instance.m_spawnOffset = spawnOffset.Value;
                    __instance.m_itemStandPrefix = _itemStandPrefix.Value;
                    __instance.m_bossItem = ZNetScene.instance.GetPrefab(_bossItem.Value).GetComponent<ItemDrop>();
                    __instance.m_useItemStands = _useItemStands.Value;
                }
                else
                {
                    Debug.LogError("Error offering bowl didn't contain offer");
                }
            }
        }

        /*[HarmonyPatch(typeof(OfferingBowl), "IsBossSpawnQueued")]
        public static class AsOfferBowlIsBossSpawnQueued
        {
            public static void Prefix(OfferingBowl __instance)
            {
                if (__instance.m_name.ToLower().Contains("prefab_offer") ||
                    __instance.name.ToLower().Contains("prefab_offer"))
                {
                    var prefabSpawned = ZNetScene.instance.GetPrefab(_bossPrefab.Value).gameObject;
                    __instance.m_bossPrefab = prefabSpawned;
                    __instance.m_itemPrefab =
                        ZNetScene.instance.GetPrefab(_itemPrefab.Value).GetComponent<ItemDrop>();
                    __instance.m_bossItems = _bossItems.Value;
                    //__instance.m_spawnOffset = spawnOffset.Value;
                    __instance.m_itemStandPrefix = _itemStandPrefix.Value;
                    __instance.m_bossItem = ZNetScene.instance.GetPrefab(_bossItem.Value).GetComponent<ItemDrop>();
                    __instance.m_useItemStands = _useItemStands.Value;
                }
                else
                {
                    Debug.LogError("Error offering bowl didn't contain offer");
                }
            }
        }*/


        [HarmonyPatch(typeof(OfferingBowl), nameof(OfferingBowl.SpawnBoss))]
        public static class AsOfferBowlSpawnBoss
        {
            public static void Prefix(OfferingBowl __instance)
            {
                if (__instance.m_name.ToLower().Contains("prefab_offer") ||
                    __instance.name.ToLower().Contains("prefab_offer"))

                {
                    var prefabSpawned = ZNetScene.instance.GetPrefab(_bossPrefab.Value).gameObject;
                    __instance.m_bossPrefab = prefabSpawned;
                    __instance.m_itemPrefab =
                        ZNetScene.instance.GetPrefab(_itemPrefab.Value).GetComponent<ItemDrop>();
                    __instance.m_bossItems = _bossItems.Value;
                    //__instance.m_spawnOffset = spawnOffset.Value;
                    __instance.m_itemStandPrefix = _itemStandPrefix.Value;
                    __instance.m_bossItem = ZNetScene.instance.GetPrefab(_bossItem.Value).GetComponent<ItemDrop>();
                    __instance.m_useItemStands = _useItemStands.Value;
                }
                else
                {
                    Debug.LogError("Error offering bowl didn't contain offer");
                }
            }
        }

        [HarmonyPatch(typeof(OfferingBowl), nameof(OfferingBowl.UseItem), null)]
        public static class OfferBowlUseItemAlterSpawner
        {
            public static bool Prefix(OfferingBowl __instance,
                Humanoid user,
                ItemDrop.ItemData item,
                Transform ___m_itemSpawnPoint,
                EffectList ___m_fuelAddedEffects,
                ref bool __result)
            {
                var bossFab = ObjectDB.instance.GetItemPrefab(_bossPrefab.Value);
                var itemFab = ObjectDB.instance.GetItemPrefab(_itemPrefab.Value);
                var itemBoss = ObjectDB.instance.GetItemPrefab(_bossItem.Value).GetComponent<ItemDrop>();
                var itemBossData = ObjectDB.instance.GetItemPrefab(_bossItem.Value).GetComponent<ItemDrop>()
                    .m_itemData;
                var itemBossValue = ObjectDB.instance.GetItemPrefab(_bossItem.Value).GetComponent<ItemDrop>()
                    .m_itemData.m_shared.m_name;
                if (item.m_shared.m_name == itemBossValue)
                {
                    var num = user.GetInventory().CountItems(itemBossValue);
                    if (num < _bossItems.Value)
                    {
                        user.Message(MessageHud.MessageType.Center,
                            "$msg_incompleteoffering: " + itemBossValue + " " + num + " / " + _bossItems.Value);
                        return true;
                    }

                    if (bossFab != null)
                    {
                        if (__instance.SpawnBoss(__instance.transform.position))
                        {
                            user.GetInventory().RemoveItem(item.m_shared.m_name, _bossItems.Value);
                            user.ShowRemovedMessage(itemBossData, _bossItems.Value);
                            user.Message(MessageHud.MessageType.Center, "$msg_offerdone");
                            if ((bool) (Object) __instance.m_itemSpawnPoint)
                                __instance.m_fuelAddedEffects.Create(__instance.m_itemSpawnPoint.position,
                                    __instance.transform.rotation);
                        }
                    }
                    else if (itemFab != null && __instance.SpawnItem(itemBoss, user as Player))
                    {
                        user.GetInventory().RemoveItem(item.m_shared.m_name, _bossItems.Value);
                        user.ShowRemovedMessage(itemBossData, _bossItems.Value);
                        user.Message(MessageHud.MessageType.Center, "$msg_offerdone");
                        __instance.m_fuelAddedEffects.Create(__instance.m_itemSpawnPoint.position,
                            __instance.transform.rotation);
                    }

                    if (!string.IsNullOrEmpty(__instance.m_setGlobalKey))
                        ZoneSystem.instance.SetGlobalKey(__instance.m_setGlobalKey);
                    return true;
                }

                user.Message(MessageHud.MessageType.Center, "$msg_offerwrong");
                /* OLD CODE
                var itemBossData = ObjectDB.instance.GetItemPrefab(_bossItem.Value).GetComponent<ItemDrop>()
                    .m_itemData;
                var itemBossValue = ObjectDB.instance.GetItemPrefab(_bossItem.Value).GetComponent<ItemDrop>()
                    .m_itemData.m_shared.m_name;
                // if (__instance.m_name.ToLower().Contains("prefab_offer") &&
                //     __instance.name.ToLower().Contains("prefab_offer")) return true;
                var flag = false;
                var num = user.GetInventory().CountItems(_bossItem.Value);
                if (num < _bossItems.Value) return false;
                if (_bossItem.Value != null) flag = true;
                user.GetInventory().RemoveItem(_itemPrefab.Value, _bossItems.Value);
                user.ShowRemovedMessage(itemBossData, _bossItems.Value);
                if (!flag)
                    return true;
                if ((bool) (Object) ___m_itemSpawnPoint && ___m_fuelAddedEffects != null)
                    ___m_fuelAddedEffects.Create(___m_itemSpawnPoint.position, __instance.transform.rotation);
                Instantiate(ZNetScene.instance.GetPrefab("fx_GP_Activation"), user.GetCenterPoint(),
                    Quaternion.identity);
                __result = true;
                return false;
                */
                return true;
            }
        }

        /*[HarmonyPatch(typeof(OfferingBowl), nameof(OfferingBowl.Interact))]
        public static class OfferBowlSpawnBoss
        {
            public static bool Prefix(OfferingBowl __instance)
            {
                /*if (!__instance.m_name.ToLower().Contains("prefab_offer") &&
                    !__instance.name.ToLower().Contains("prefab_offer")) return true;
                var p = Player.m_localPlayer;

                print(_bossItem.Value);

                var itemBossValue = ObjectDB.instance.GetItemPrefab(_bossItem.Value).GetComponent<ItemDrop>()
                    .m_itemData.m_shared.m_name;
                print(itemBossValue);
                var num = p.GetInventory().CountItems(itemBossValue);
                if (num < _bossItems.Value) return false;

                p.GetInventory().RemoveItem(itemBossValue, _bossItems.Value);#1#
                return true;
            }
        }*/

        #region Configs

        private static ConfigEntry<bool> _serverConfigLocked;
        private static ConfigEntry<int> _nexusID;

        private static ConfigEntry<string> _bossPrefab;
        private static ConfigEntry<string> _itemPrefab;
        private static ConfigEntry<int> _bossItems;
        public static ConfigEntry<float> spawnOffset;
        private static ConfigEntry<string> _itemStandPrefix;
        private static ConfigEntry<string> _bossItem;
        private static ConfigEntry<bool> _useItemStands;


        private ConfigEntry<T> Config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            var configEntry = base.Config.Bind(group, name, value, description);

            var syncedConfigEntry = _configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> Config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return Config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            public bool? browsable = false;
        }

        #endregion
    }
}
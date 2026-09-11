using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TimedCraftSpeed
{
    [BepInPlugin(
        PluginGuid,
        PluginName,
        PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.timedcraftspeed";
        public const string PluginName = "Timed Craft Speed";
        public const string PluginVersion = "1.0.5";

        private const string ConfigSection =
            "Multiplicateurs de vitesse";

        private static Plugin _instance;
        private Harmony _harmony;
        private float _nextLabelRefresh;

        private static readonly Dictionary<string, ConfigEntry<float>>
            Multipliers =
                new Dictionary<string, ConfigEntry<float>>(
                    StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, ConfigurationManagerAttributes>
            DisplayAttributes =
                new Dictionary<string, ConfigurationManagerAttributes>(
                    StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string>
            EnglishNames =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
        {
            { "smelter", "Smelter" },
            { "charcoal_kiln", "Charcoal Kiln" },
            { "blastfurnace", "Blast Furnace" },
            { "windmill", "Windmill" },
            { "piece_spinningwheel", "Spinning Wheel" },
            { "eitrrefinery", "Eitr Refinery" },
            { "fermenter", "Fermenter" },
            { "piece_cookingstation", "Cooking Station" },
            { "piece_cookingstation_iron", "Iron Cooking Station" },
            { "piece_oven", "Stone Oven" },
            { "piece_FrostKiln", "Frigid Kiln" },
            { "piece_FrostFoundry", "Frost Foundry" }
        };

        private static readonly Dictionary<string, string>
            LocalizationTokens =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
        {
            { "smelter", "$piece_smelter" },
            { "charcoal_kiln", "$piece_charcoalkiln" },
            { "blastfurnace", "$piece_blastfurnace" },
            { "windmill", "$piece_windmill" },
            { "piece_spinningwheel", "$piece_spinningwheel" },
            { "eitrrefinery", "$piece_eitrrefinery" },
            { "fermenter", "$piece_fermenter" },
            { "piece_cookingstation", "$piece_cookingstation" },
            { "piece_cookingstation_iron", "$piece_cookingstation_iron" },
            { "piece_oven", "$piece_oven" },
            { "piece_FrostKiln", "$piece_frostkiln" },
            { "piece_FrostFoundry", "$piece_frostfoundry" }
        };

        /*
         * On conserve les anciennes cles internes pour ne pas
         * perdre les valeurs deja enregistrees dans le cfg.
         * Elles ne seront plus affichees dans Configuration Manager.
         */
        private static readonly Dictionary<string, string>
            LegacyNames =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
        {
            { "smelter", "Fonderie" },
            { "charcoal_kiln", "Four a charbon" },
            { "blastfurnace", "Haut fourneau" },
            { "windmill", "Moulin" },
            { "piece_spinningwheel", "Rouet" },
            { "eitrrefinery", "Raffinerie d'Eitr" },
            { "fermenter", "Fermenteur" },
            { "piece_cookingstation", "Station de cuisson" },
            { "piece_cookingstation_iron", "Station de cuisson en fer" },
            { "piece_oven", "Four en pierre" },
            { "piece_FrostKiln", "Four glacial" },
            { "piece_FrostFoundry", "Fonderie du givre" }
        };

        private static readonly Dictionary<string, float>
            FloatBaselines =
                new Dictionary<string, float>();

        private static readonly Dictionary<object, float>
            ConversionBaselines =
                new Dictionary<object, float>(
                    ReferenceComparer.Instance);

        private void Awake()
        {
            _instance = this;

            RegisterDefaultSettings();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo("Timed Craft Speed actif.");
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextLabelRefresh)
                return;

            _nextLabelRefresh = Time.unscaledTime + 1f;

            RefreshLocalizedLabels();
        }

        private void OnDestroy()
        {
            if (_harmony != null)
                _harmony.UnpatchSelf();

            _instance = null;
        }

        private void RegisterDefaultSettings()
        {
            EnsureMultiplier("smelter");
            EnsureMultiplier("charcoal_kiln");
            EnsureMultiplier("blastfurnace");
            EnsureMultiplier("windmill");
            EnsureMultiplier("piece_spinningwheel");
            EnsureMultiplier("eitrrefinery");
            EnsureMultiplier("fermenter");
            EnsureMultiplier("piece_cookingstation");
            EnsureMultiplier("piece_cookingstation_iron");
            EnsureMultiplier("piece_oven");
            EnsureMultiplier("piece_FrostKiln");
            EnsureMultiplier("piece_FrostFoundry");
        }

        private ConfigEntry<float> EnsureMultiplier(
            string prefabName)
        {
            prefabName = NormalizePrefabName(prefabName);

            if (string.IsNullOrEmpty(prefabName))
                return null;

            ConfigEntry<float> existing;

            if (Multipliers.TryGetValue(
                prefabName,
                out existing))
            {
                return existing;
            }

            string english =
                GetEnglishName(prefabName);

            string legacyName;

            if (!LegacyNames.TryGetValue(
                prefabName,
                out legacyName))
            {
                legacyName = english;
            }

            string key =
                SanitizeConfigKey(
                    legacyName +
                    " - " +
                    prefabName);

            ConfigurationManagerAttributes attributes =
                new ConfigurationManagerAttributes
                {
                    DispName = english
                };

            ConfigEntry<float> entry =
                Config.Bind(
                    ConfigSection,
                    key,
                    1.0f,
                    new ConfigDescription(
                        "Multiplicateur de vitesse.",
                        new AcceptableValueRange<float>(
                            0.1f,
                            20.0f),
                        attributes));

            Multipliers[prefabName] = entry;
            DisplayAttributes[prefabName] = attributes;

            entry.SettingChanged +=
                delegate
                {
                    RefreshLiveMachines();
                };

            return entry;
        }

        private static string GetEnglishName(
            string prefabName)
        {
            string name;

            if (EnglishNames.TryGetValue(
                prefabName,
                out name))
            {
                return name;
            }

            name = prefabName;

            if (name.StartsWith(
                "piece_",
                StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(6);
            }

            name = name.Replace('_', ' ');

            return name;
        }

        private static string SanitizeConfigKey(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Machine";

            char[] invalid =
            {
                '=',
                '\n',
                '\t',
                '\\',
                '"',
                '\'',
                '[',
                ']'
            };

            foreach (char c in invalid)
                value = value.Replace(c, '-');

            return value.Trim();
        }

        private static string NormalizePrefabName(
            string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            const string clone = "(Clone)";

            if (name.EndsWith(
                clone,
                StringComparison.Ordinal))
            {
                name =
                    name.Substring(
                        0,
                        name.Length - clone.Length);
            }

            return name.Trim();
        }

        private static object GetLocalizationInstance()
        {
            BindingFlags flags =
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            foreach (Assembly assembly in
                     AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type =
                    assembly.GetType(
                        "Localization",
                        false);

                if (type == null)
                    continue;

                FieldInfo field =
                    type.GetField(
                        "instance",
                        flags);

                if (field == null)
                {
                    field =
                        type.GetField(
                            "m_instance",
                            flags);
                }

                if (field != null)
                    return field.GetValue(null);

                PropertyInfo property =
                    type.GetProperty(
                        "instance",
                        flags);

                if (property != null)
                    return property.GetValue(null, null);
            }

            return null;
        }

        private static string LocalizeGameText(
            string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            object localization =
                GetLocalizationInstance();

            if (localization == null)
                return null;

            MethodInfo method =
                localization.GetType().GetMethod(
                    "Localize",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);

            if (method == null)
                return null;

            try
            {
                return method.Invoke(
                    localization,
                    new object[] { token })
                    as string;
            }
            catch
            {
                return null;
            }
        }

        private void RefreshLocalizedLabels()
        {
            foreach (
                KeyValuePair<string, string> pair
                in LocalizationTokens)
            {
                string localized =
                    LocalizeGameText(
                        pair.Value);

                if (string.IsNullOrWhiteSpace(localized))
                    continue;

                SetDisplayName(
                    pair.Key,
                    localized);
            }
        }
        private static void SetDisplayName(
            string prefabName,
            string localized)
        {
            ConfigurationManagerAttributes attributes;

            if (!DisplayAttributes.TryGetValue(
                prefabName,
                out attributes))
            {
                return;
            }

            string english =
                GetEnglishName(prefabName);

            if (string.IsNullOrWhiteSpace(localized) ||
                localized.StartsWith("$"))
            {
                attributes.DispName = english;
                return;
            }

            attributes.DispName =
                english +
                " (" +
                localized +
                ")";
        }

        private void UpdateDisplayNameFromPiece(
            Component component,
            string prefabName)
        {
            if (component == null)
                return;

            Piece piece =
                component.GetComponent<Piece>();

            if (piece == null)
                piece =
                    component.GetComponentInParent<Piece>();

            if (piece == null)
            {
                piece =
                    component.GetComponentInChildren<Piece>(
                        true);
            }

            if (piece == null ||
                string.IsNullOrEmpty(piece.m_name))
            {
                return;
            }

            string localized =
                LocalizeGameText(
                    piece.m_name);

            if (string.IsNullOrWhiteSpace(localized))
                return;

            SetDisplayName(
                prefabName,
                localized);
        }
        private string GetPrefabName(
            Component component)
        {
            if (component == null)
                return string.Empty;

            ZNetView view =
                component.GetComponent<ZNetView>();

            if (view == null)
                view = component.GetComponentInParent<ZNetView>();

            if (view != null &&
                view.gameObject != null)
            {
                return NormalizePrefabName(
                    view.gameObject.name);
            }

            return NormalizePrefabName(
                component.gameObject.name);
        }

        private static FieldInfo GetFieldSilent(
            Type type,
            string name)
        {
            while (type != null)
            {
                FieldInfo field =
                    type.GetField(
                        name,
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                if (field != null)
                    return field;

                type = type.BaseType;
            }

            return null;
        }

        private void ApplySmelter(
            Smelter smelter)
        {
            if (smelter == null)
                return;

            string prefabName =
                GetPrefabName(smelter);

            ConfigEntry<float> multiplier =
                EnsureMultiplier(prefabName);

            UpdateDisplayNameFromPiece(
                smelter,
                prefabName);

            if (multiplier == null)
                return;

            FieldInfo field =
                GetFieldSilent(
                    smelter.GetType(),
                    "m_secPerProduct");

            if (field == null ||
                field.FieldType != typeof(float))
            {
                return;
            }

            ApplyFloatTimer(
                smelter,
                field,
                prefabName,
                multiplier);
        }

        private void ApplyFermenter(
            Fermenter fermenter)
        {
            if (fermenter == null)
                return;

            string prefabName =
                GetPrefabName(fermenter);

            ConfigEntry<float> multiplier =
                EnsureMultiplier(prefabName);

            UpdateDisplayNameFromPiece(
                fermenter,
                prefabName);

            if (multiplier == null)
                return;

            FieldInfo field =
                GetFieldSilent(
                    fermenter.GetType(),
                    "m_fermentationDuration");

            if (field == null ||
                field.FieldType != typeof(float))
            {
                return;
            }

            ApplyFloatTimer(
                fermenter,
                field,
                prefabName,
                multiplier);
        }

        private void ApplyCookingStation(
            CookingStation station)
        {
            if (station == null)
                return;

            string prefabName =
                GetPrefabName(station);

            ConfigEntry<float> multiplier =
                EnsureMultiplier(prefabName);

            UpdateDisplayNameFromPiece(
                station,
                prefabName);

            if (multiplier == null)
                return;

            FieldInfo conversionsField =
                GetFieldSilent(
                    station.GetType(),
                    "m_conversion");

            if (conversionsField == null)
                return;

            IEnumerable conversions =
                conversionsField.GetValue(station)
                    as IEnumerable;

            if (conversions == null)
                return;

            foreach (object conversion in conversions)
            {
                if (conversion == null)
                    continue;

                FieldInfo cookTime =
                    GetFieldSilent(
                        conversion.GetType(),
                        "m_cookTime");

                if (cookTime == null ||
                    cookTime.FieldType != typeof(float))
                {
                    continue;
                }

                ApplyCookTimer(
                    conversion,
                    cookTime,
                    multiplier);
            }
        }

        private void ApplyFloatTimer(
            Component component,
            FieldInfo field,
            string prefabName,
            ConfigEntry<float> multiplier)
        {
            string key =
                component.GetInstanceID()
                    .ToString() +
                "|" +
                field.DeclaringType.FullName +
                "|" +
                field.Name;

            float original;

            if (!FloatBaselines.TryGetValue(
                key,
                out original))
            {
                object value =
                    field.GetValue(component);

                if (!(value is float))
                    return;

                original = (float)value;
                FloatBaselines[key] = original;
            }

            float speed =
                Math.Max(
                    0.01f,
                    multiplier.Value);

            field.SetValue(
                component,
                original / speed);
        }

        private void ApplyCookTimer(
            object conversion,
            FieldInfo cookTime,
            ConfigEntry<float> multiplier)
        {
            float original;

            if (!ConversionBaselines.TryGetValue(
                conversion,
                out original))
            {
                object value =
                    cookTime.GetValue(conversion);

                if (!(value is float))
                    return;

                original = (float)value;

                ConversionBaselines[conversion] =
                    original;
            }

            float speed =
                Math.Max(
                    0.01f,
                    multiplier.Value);

            cookTime.SetValue(
                conversion,
                original / speed);
        }

        private void RefreshLiveMachines()
        {
            Smelter[] smelters =
                Resources.FindObjectsOfTypeAll<Smelter>();

            foreach (Smelter smelter in smelters)
            {
                if (IsLive(smelter))
                    ApplySmelter(smelter);
            }

            Fermenter[] fermenters =
                Resources.FindObjectsOfTypeAll<Fermenter>();

            foreach (Fermenter fermenter in fermenters)
            {
                if (IsLive(fermenter))
                    ApplyFermenter(fermenter);
            }

            CookingStation[] stations =
                Resources.FindObjectsOfTypeAll<CookingStation>();

            foreach (CookingStation station in stations)
            {
                if (IsLive(station))
                    ApplyCookingStation(station);
            }
        }

        private static bool IsLive(
            Component component)
        {
            if (component == null)
                return false;

            ZNetView view =
                component.GetComponent<ZNetView>();

            if (view == null)
                view = component.GetComponentInParent<ZNetView>();

            if (view == null)
                return false;

            try
            {
                MethodInfo method =
                    AccessTools.Method(
                        typeof(ZNetView),
                        "GetZDO");

                return method != null &&
                       method.Invoke(
                           view,
                           null) != null;
            }
            catch
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(Smelter), "Awake")]
        private static class SmelterAwakePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(
                Smelter __instance)
            {
                if (_instance != null)
                    _instance.ApplySmelter(__instance);
            }
        }

        [HarmonyPatch(typeof(Fermenter), "Awake")]
        private static class FermenterAwakePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(
                Fermenter __instance)
            {
                if (_instance != null)
                    _instance.ApplyFermenter(__instance);
            }
        }

        [HarmonyPatch(typeof(CookingStation), "Awake")]
        private static class CookingStationAwakePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(
                CookingStation __instance)
            {
                if (_instance != null)
                    _instance.ApplyCookingStation(__instance);
            }
        }

        private sealed class ReferenceComparer :
            IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance =
                new ReferenceComparer();

            public new bool Equals(
                object x,
                object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(
                object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}


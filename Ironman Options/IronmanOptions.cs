// Project:         Ironman Options mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2021 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallConnect.Utility;

namespace IronmanOptions
{
    public class IronmanOptions : MonoBehaviour
    {
        static Mod mod;
        static bool EnterExitDungeon = false;
        static bool FullRestDungeon = false;
        static bool CampingDungeon = false;
        static bool EnterExitOutside = false;
        static bool FullRestOutside = false;
        static bool CampingOutside = false;
        static bool SavingPossible = false;
        static int SaveCounter = 0;
        static bool travelOptionsRunning = false;
        static bool Resting = false;

        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            ModSettings settings = mod.GetSettings();

            EnterExitDungeon = settings.GetValue<int>("Dungeon", "WhenToSave") == 1 ? true : false;

            switch (settings.GetValue<int>("Dungeon", "WhenToSave"))
            {
                case 1:
                    EnterExitDungeon = true;
                    break;
                case 2:
                    FullRestDungeon = true;
                    break;
                case 3:
                    CampingDungeon = true;
                    break;
            }
            switch (settings.GetValue<int>("Outside", "WhenToSave"))
            {
                case 1:
                    EnterExitOutside = true;
                    break;
                case 2:
                    FullRestOutside = true;
                    break;
                case 3:
                    CampingOutside = true;
                    break;
            }

            if (EnterExitDungeon)
                PlayerEnterExit.OnPreTransition += EnterExitDungeon_OnPreTransition;
            if (EnterExitOutside)
                PlayerGPS.OnMapPixelChanged += EnterExitSave_OnMapPixelChanged;
            if (CampingDungeon || CampingOutside)
            {
                PlayerActivate.RegisterCustomActivation(mod, 101, 0, CampingSave);
                PlayerActivate.RegisterCustomActivation(mod, 101, 5, CampingSave);
                PlayerActivate.RegisterCustomActivation(mod, 210, 0, CampingSave);
                PlayerActivate.RegisterCustomActivation(mod, 210, 1, CampingSave);
                PlayerActivate.RegisterCustomActivation(mod, 41116, CampingSave);
                PlayerActivate.RegisterCustomActivation(mod, 41117, CampingSave);
                PlayerActivate.RegisterCustomActivation(mod, 41606, CampingSave);
            }

            var go = new GameObject(mod.Title);
            go.AddComponent<IronmanOptions>();

            EntityEffectBroker.OnNewMagicRound += IronmanSaving_OneNewMagicRound;
            playerEntity.OnDeath += DeleteSave;
            SaveLoadManager.Instance.RegisterPreventSaveCondition(() => !SavingPossible);

            mod.IsReady = true;
        }

        static void IronmanSaving_OneNewMagicRound()
        {
            if (!SaveLoadManager.Instance.LoadInProgress && !DaggerfallUI.Instance.FadeBehaviour.FadeInProgress && GameManager.Instance.IsPlayerOnHUD && !playerEntity.InPrison && !playerEntity.Arrested)
            {
                ModManager.Instance.SendModMessage("TravelOptions", "isTravelActive", null, (string message, object data) =>
                {
                    travelOptionsRunning = (bool)data;
                });
                if (!travelOptionsRunning)
                {
                    SaveCounter++;
                    Debug.Log("[Ironman Options] SaveCounter = " + SaveCounter.ToString());
                    if (SaveCounter > 10)
                    {
                        SaveCounter = 0;
                        SaveGame("Ironman");
                    }
                }
            }
            else
            {
                Debug.Log("[Ironman Options] SaveCounter not counting down.");
            }

            if (playerEntity.IsResting)
                Resting = true;
            else if (Resting && !playerEntity.IsResting && playerEntity.CurrentHealth == playerEntity.MaxHealth)
            {
                if (FullRestDungeon && (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle || playerEnterExit.IsPlayerInsideSpecialArea))
                    SaveGame("Ironman Permanent");
                if (FullRestOutside && (!playerEnterExit.IsPlayerInside || playerEnterExit.IsPlayerInsideBuilding))
                    SaveGame("Ironman Permanent");
                Resting = false;
            }
        }

        private static void EnterExitDungeon_OnPreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            SaveGame("Ironman Permanent");
        }

        private static void EnterExitSave_OnMapPixelChanged(DFPosition mapPixel)
        {
            if (!SaveLoadManager.Instance.LoadInProgress && !DaggerfallUI.Instance.FadeBehaviour.FadeInProgress && GameManager.Instance.IsPlayerOnHUD && !playerEntity.InPrison && !playerEntity.Arrested)
            {
                if (GameManager.Instance.PlayerGPS.IsPlayerInTown())
                {
                    SaveGame("Ironman Permanent");
                }
            }
        }

        public static void CampingSave(RaycastHit hit)
        {
            if (CampingDungeon && !GameManager.Instance.AreEnemiesNearby(true) && (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle || playerEnterExit.IsPlayerInsideSpecialArea))
                SaveGame("Ironman Permanent");
            else if (CampingOutside && !GameManager.Instance.AreEnemiesNearby(true))
                SaveGame("Ironman Permanent");
        }

        private static void SaveGame(string saveName)
        {
            Debug.Log("Game saved with string " + saveName.ToString());
            GameManager.Instance.SaveLoadManager.EnumerateSaves();
            GameManager.Instance.SaveLoadManager.Save(GameManager.Instance.PlayerEntity.Name, saveName);
        }

        private static void DeleteSave(DaggerfallEntity entity)
        {
            Debug.Log("Deleting Ironman save.");
            int key = GameManager.Instance.SaveLoadManager.FindSaveFolderByNames(playerEntity.Name, "Ironman");
            GameManager.Instance.SaveLoadManager.DeleteSaveFolder(key);
        }
    }
}
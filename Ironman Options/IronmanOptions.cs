// Project:         Ironman Options mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2021 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility;
using System;

namespace IronmanOptions
{
    public class IronmanOptions : MonoBehaviour
    {
        static Mod mod;
        static bool enterExitDungeon = false;
        static bool fullRestDungeon = false;
        static bool campingDungeon = false;
        static bool enterExitOutside = false;
        static bool fullRestOutside = false;
        static bool campingOutside = false;
        static bool ironmanSaves = true;
        static bool startSave = true;
        static bool savingStart = false;
        static bool savingPossible = false;
        static int saveCounter = 0;
        static bool travelOptionsRunning = false;
        static bool resting = false;
        static bool gameStart = false;
        static KeyCode mainMenuKey = KeyCode.Escape;
        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;


        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            ModSettings settings = mod.GetSettings();

            ironmanSaves = settings.GetValue<bool>("General", "IronmanSaves");
            startSave = settings.GetValue<bool>("General", "StartSave");
            StartGameBehaviour.OnStartGame += StartDetection_OnStartGame;

            if (startSave)
            {
                StartGameBehaviour.OnStartGame += StartSave_OnStartGame;
            }

            switch (settings.GetValue<int>("DungeonPermanentSave", "WhenToSave"))
            {
                case 1:
                    enterExitDungeon = true;
                    break;
                case 2:
                    fullRestDungeon = true;
                    break;
                case 3:
                    campingDungeon = true;
                    break;
            }
            switch (settings.GetValue<int>("OutsidePermanentSave", "WhenToSave"))
            {
                case 1:
                    enterExitOutside = true;
                    break;
                case 2:
                    fullRestOutside = true;
                    break;
                case 3:
                    campingOutside = true;
                    break;
            }

            if (enterExitDungeon)
                PlayerEnterExit.OnPreTransition += EnterExitDungeon_OnPreTransition;
            if (enterExitOutside)
                PlayerEnterExit.OnPreTransition += EnterExitHouse_OnPreTransition;
            if (campingDungeon || campingOutside)
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
            SaveLoadManager.Instance.RegisterPreventSaveCondition(() => !savingPossible);
            mainMenuKey = InputManager.Instance.GetBinding(InputManager.Actions.Escape);
            mod.IsReady = true;
        }

        void Update()
        {
            if (!InputManager.Instance.IsPaused && InputManager.Instance.GetKeyDown(mainMenuKey))
                escSave();

            if (savingStart)
            {
                if (!GameManager.IsGamePaused)
                {
                    savingStart = false;
                    StartSave();
                }
            }

            if (gameStart)
            {
                if (!GameManager.IsGamePaused)
                {
                    gameStart = false;
                    if (enterExitDungeon)
                        SaveGame("Ironman Permanent");
                }
            }
        }

        static void StartSave_OnStartGame(object sender, EventArgs e)
        {
            savingStart = true;
        }

        static void StartDetection_OnStartGame(object sender, EventArgs e)
        {
            gameStart = true;
        }

        static void escSave()
        {
            //Possible alternate coding for this where it does not make a save upon Exit Game if you use one of the other save options instead of default Ironman.
            //if (playerEnterExit.IsPlayerInside && !enterExitDungeon && !fullRestDungeon && !campingDungeon)
            //    SaveGame("Ironman");
            //else if (!playerEnterExit.IsPlayerInside && !enterExitOutside && !fullRestOutside && !campingOutside)
            //    SaveGame("Ironman");
            SaveGame("Ironman");
        }

        static void StartSave()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            string saveName = "Start Save";
            GameManager.Instance.SaveLoadManager.Save(playerEntity.Name, saveName);
        }

        static void IronmanSaving_OneNewMagicRound()
        {
            if (ironmanSaves && !SaveLoadManager.Instance.LoadInProgress && !DaggerfallUI.Instance.FadeBehaviour.FadeInProgress && GameManager.Instance.IsPlayerOnHUD && !playerEntity.InPrison && !playerEntity.Arrested)
            {
                mainMenuKey = InputManager.Instance.GetBinding(InputManager.Actions.Escape);
                ModManager.Instance.SendModMessage("TravelOptions", "isTravelActive", null, (string message, object data) =>
                {
                    travelOptionsRunning = (bool)data;
                });
                if (!travelOptionsRunning)
                {
                    saveCounter++;
                    if (saveCounter >= 60)
                    {
                        saveCounter = 0;
                        SaveGame("Ironman");
                    }
                }
            }

            if (playerEntity.IsResting)
                resting = true;
            else if (resting && !playerEntity.IsResting && playerEntity.CurrentHealth == playerEntity.MaxHealth)
            {
                if (GameManager.Instance.AreEnemiesNearby(true))
                {
                    DaggerfallUI.AddHUDText("Enemies Nearby");
                }
                else if (fullRestDungeon && (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle || playerEnterExit.IsPlayerInsideSpecialArea))
                    SaveGame("Ironman Permanent");
                else if (fullRestOutside && (!playerEnterExit.IsPlayerInside || playerEnterExit.IsPlayerInsideBuilding))
                    SaveGame("Ironman Permanent");
                resting = false;
            }
        }

        private static void EnterExitDungeon_OnPreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            if (!gameStart)
                SaveGame("Ironman Permanent");
        }

        private static void EnterExitHouse_OnPreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            SaveGame("Ironman Permanent");
        }

        public static void CampingSave(RaycastHit hit)
        {
            if (GameManager.Instance.AreEnemiesNearby(true))
            {
                DaggerfallUI.AddHUDText("Enemies Nearby");
            }
            else if (campingDungeon && (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle || playerEnterExit.IsPlayerInsideSpecialArea))
                SaveGame("Ironman Permanent");
            else if (campingOutside && !GameManager.Instance.AreEnemiesNearby(true))
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
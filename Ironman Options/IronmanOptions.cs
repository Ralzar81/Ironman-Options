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
using System.Text.RegularExpressions;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace IronmanOptions
{
    public class IronmanOptions : MonoBehaviour
    {
        static Mod mod;

        public static IronmanOptions Instance { get; private set; }

        public const string IRON_SAVE = "save";
        public const string CAMP_SAVE = "campSave";
        static string ironmanSave = "Ironman";
        static string permanentSave = "Ironman Permanent";
        static bool livesSystem = false;
        static int startLives = 0;
        static int maxLives = 0;
        static bool levelUp1up = false;
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
        static bool leveling = false;
        static KeyCode mainMenuKey = KeyCode.Escape;
        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
        static SaveLoadManager saveLoadManager = GameManager.Instance.SaveLoadManager;


        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<IronmanOptions>();
            ModSettings settings = mod.GetSettings();

            ironmanSaves = settings.GetValue<bool>("General", "IronmanSaves");
            startSave = settings.GetValue<bool>("General", "StartSave");
            StartGameBehaviour.OnStartGame += StartDetection_OnStartGame;

            if (startSave)
            {
                StartGameBehaviour.OnStartGame += StartSave_OnStartGame;
            }
            Debug.Log("[Ironman Options] ioDungeonSetting = " + settings.GetValue<int>("DungeonPermanentSave", "WhenToSave").ToString());
            Debug.Log("[Ironman Options] ioOutsideSetting = " + settings.GetValue<int>("OutsidePermanentSave", "WhenToSave").ToString());
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

            livesSystem = settings.GetValue<bool>("PermanentSaveLives", "LivesSystem");
            if (livesSystem)
            {
                startLives = settings.GetValue<int>("PermanentSaveLives", "StartingLives");
                maxLives = settings.GetValue<int>("PermanentSaveLives", "MaxLives");
                levelUp1up = settings.GetValue<bool>("PermanentSaveLives", "LevelUpExtraLife");
            }            

            if (enterExitDungeon)
                PlayerEnterExit.OnPreTransition += EnterExitDungeon_OnPreTransition;
            if (enterExitOutside)
                PlayerEnterExit.OnPreTransition += EnterExitHouse_OnPreTransition;
            if (campingDungeon || campingOutside)
            {
                    PlayerActivate.RegisterCustomActivation(mod, 101, 0, FireClicked);
                    PlayerActivate.RegisterCustomActivation(mod, 101, 5, FireClicked);
                    PlayerActivate.RegisterCustomActivation(mod, 210, 0, FireClicked);
                    PlayerActivate.RegisterCustomActivation(mod, 210, 1, FireClicked);
                    PlayerActivate.RegisterCustomActivation(mod, 41116, FireClicked);
                    PlayerActivate.RegisterCustomActivation(mod, 41117, FireClicked);
                    PlayerActivate.RegisterCustomActivation(mod, 41606, FireClicked);               
            }

            EntityEffectBroker.OnNewMagicRound += IronmanSaving_OneNewMagicRound;
            playerEntity.OnDeath += DeleteSave;
            SaveLoadManager.Instance.RegisterPreventSaveCondition(() => !savingPossible);
            mainMenuKey = InputManager.Instance.GetBinding(InputManager.Actions.Escape);            
        }

        private void Awake()
        {
            mod.MessageReceiver = MessageReceiver;
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
                    if (enterExitDungeon || livesSystem)
                        SaveGame(permanentSave);
                }
            }

            if (levelUp1up && playerEntity.ReadyToLevelUp)
            {
                leveling = true;
            }
            else if (levelUp1up && leveling && GameManager.Instance.IsPlayingGame())
            {
                leveling = false;
                GainLife();
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
            SaveGame(ironmanSave);
        }

        static void StartSave()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            string saveName = "Start Save";
            saveLoadManager.Save(playerEntity.Name, saveName);
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
                        SaveGame(ironmanSave);
                    }
                }
            }

            if (playerEntity.IsResting && !playerEntity.IsLoitering)
                resting = true;
            else if (resting && !playerEntity.IsResting && playerEntity.CurrentHealth == playerEntity.MaxHealth)
            {
                if (GameManager.Instance.AreEnemiesNearby(true))
                {
                    
                }
                else if (fullRestDungeon && (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle || playerEnterExit.IsPlayerInsideSpecialArea))
                    SaveGame(permanentSave);
                else if (fullRestOutside && (!playerEnterExit.IsPlayerInside || playerEnterExit.IsPlayerInsideBuilding))
                    SaveGame(permanentSave);

                resting = false;
            }
        }

        private static void EnterExitDungeon_OnPreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            if (!gameStart)
                SaveGame(permanentSave);
        }

        private static void EnterExitHouse_OnPreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            SaveGame(permanentSave);
        }

        public static void FireClicked(RaycastHit hit)
        {
            if (!GameManager.Instance.AreEnemiesNearby(true))
            {
                CampSave();
            }
        }

        public static void CampSave()
        {
            if (campingDungeon && (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle || playerEnterExit.IsPlayerInsideSpecialArea))
                SaveGame(permanentSave);
            else if (campingOutside && !GameManager.Instance.AreEnemiesNearby(true))
                SaveGame(permanentSave);
        }

        private static void SaveGame(string saveName)
        {
            Debug.Log("Game saved with string " + saveName.ToString());
            saveLoadManager.EnumerateSaves();
            if (livesSystem && saveName == permanentSave)
            {
                int life = startLives;
                int[] saveKeys = saveLoadManager.GetCharacterSaveKeys(playerEntity.Name);
                foreach (int key in saveKeys)
                {
                    string name = saveLoadManager.GetSaveInfo(key).saveName;
                    if (name.Contains(permanentSave))
                    {
                        if (name != permanentSave)
                            life = int.Parse(Regex.Match(name, @"\d+").Value);
                        if (life > maxLives && maxLives != 0)
                            life = maxLives;
                        saveLoadManager.EnumerateSaves();
                        saveLoadManager.DeleteSaveFolder(key);
                    }
                }
                saveLoadManager.EnumerateSaves();
                saveLoadManager.Save(playerEntity.Name, permanentSave + " " + life.ToString());
            }
            else
                saveLoadManager.Save(playerEntity.Name, saveName);
        }

        private static void DeleteSave(DaggerfallEntity entity)
        {
            Debug.Log("Deleting Ironman save.");
            int key = saveLoadManager.FindSaveFolderByNames(playerEntity.Name, ironmanSave);
            saveLoadManager.DeleteSaveFolder(key);
            if (livesSystem)
                LoseLife();
        }

        private static void LoseLife()
        {
            Debug.Log("Counting down life");
            saveLoadManager.EnumerateSaves();
            int[] saveKeys = saveLoadManager.GetCharacterSaveKeys(playerEntity.Name);
            foreach (int key in saveKeys)
            {
                string name = saveLoadManager.GetSaveInfo(key).saveName;
                if (name.Contains(permanentSave))
                {
                    int life = 0;
                    if (name != permanentSave)
                        life = int.Parse(Regex.Match(name, @"\d+").Value);
                    if (life > maxLives && maxLives != 0)
                        life = maxLives;
                    if (life > 0)
                    {
                        life--;
                        saveLoadManager.Rename(key, permanentSave + " " + life.ToString());
                    }
                    else
                    {
                        Debug.Log("No lives left, deleting Ironman Permanent save.");
                        saveLoadManager.DeleteSaveFolder(key);
                    }
                }
            }
        }

        private static void GainLife()
        {
            Debug.Log("Counting down life");
            saveLoadManager.EnumerateSaves();
            int[] saveKeys = saveLoadManager.GetCharacterSaveKeys(playerEntity.Name);
            foreach (int key in saveKeys)
            {
                string name = saveLoadManager.GetSaveInfo(key).saveName;
                if (name.Contains(permanentSave))
                {
                    int life = maxLives;
                    if (name != permanentSave)
                        life = int.Parse(Regex.Match(name, @"\d+").Value);
                    if (life < maxLives || maxLives == 0)
                    {
                        life++;
                        DaggerfallUI.AddHUDText("Gained one Ironman Life");
                    }
                    else
                        life = maxLives;
                    saveLoadManager.Rename(key, permanentSave + " " + life.ToString());
                }
            }
        }

        void MessageReceiver(string message, object data, DFModMessageCallback callBack)
        {
            Debug.Log("[Ironman Options] mod message recieved");
            switch (message)
            {
                case IRON_SAVE:
                    SaveGame(ironmanSave);
                    break;
                case CAMP_SAVE:
                    CampSave();
                    break;
                default:
                    Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                    break;
            }
        }
    }
}
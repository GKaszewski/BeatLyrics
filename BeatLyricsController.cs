using BeatLyrics.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;

namespace BeatLyrics {
    /// <summary>
    /// Monobehaviours (scripts) are added to GameObjects.
    /// For a full list of Messages a Monobehaviour can receive from the game, see https://docs.unity3d.com/ScriptReference/MonoBehaviour.html.
    /// </summary>
    public class BeatLyricsController : MonoBehaviour {
        public static BeatLyricsController Instance { get; private set; }

        private const BindingFlags NON_PUBLIC_INSTANCE = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo AudioTimeSyncField = typeof(GameSongController).GetField("_audioTimeSyncController", NON_PUBLIC_INSTANCE);
        private static readonly FieldInfo SceneSetupDataField = typeof(BeatmapObjectsInstaller).GetField("_sceneSetupData", NON_PUBLIC_INSTANCE);
        private static readonly FieldInfo FlyingTextEffectPoolField = typeof(FlyingTextSpawner).GetField("_flyingTextEffectPool", NON_PUBLIC_INSTANCE);
        private static readonly FieldInfo ContainerField
            = typeof(MonoInstallerBase).GetField("<Container>k__BackingField", NON_PUBLIC_INSTANCE);

        private static readonly Func<FlyingTextSpawner, float> GetTextSpawnerDuration;
        private static readonly Action<FlyingTextSpawner, float> SetTextSpawnerDuration;

        private GameSongController songController;
        private FlyingTextSpawner textSpawner;
        private AudioTimeSyncController timeSyncController;
        private BeatmapObjectsInstaller sceneSetup;
        private GameplayCoreSceneSetupData sceneSetupData;
        private List<Lyrics> lyrics = new List<Lyrics>();

        private TMP_Text lyricText;


        static BeatLyricsController() {
            var durationField = typeof(FlyingTextSpawner).GetField("_duration", NON_PUBLIC_INSTANCE);
            if (durationField == null) throw new Exception("Cannot find _duration field of FlyingTextSpawner");

            var setterMethod = new DynamicMethod("SetDuration", typeof(void), new[] { typeof(FlyingTextSpawner), typeof(float) }, typeof(FlyingTextSpawner));
            var setterIlGenerator = setterMethod.GetILGenerator(16);

            setterIlGenerator.Emit(OpCodes.Ldarg_0);
            setterIlGenerator.Emit(OpCodes.Ldarg_1);
            setterIlGenerator.Emit(OpCodes.Stfld, durationField);
            setterIlGenerator.Emit(OpCodes.Ret);

            SetTextSpawnerDuration = (Action<FlyingTextSpawner, float>)setterMethod.CreateDelegate(typeof(Action<FlyingTextSpawner, float>));

            var getterMethod = new DynamicMethod("GetDuration", typeof(float), new[] { typeof(FlyingTextSpawner) }, typeof(FlyingTextSpawner));
            var getterIlGenerator = getterMethod.GetILGenerator(16);

            getterIlGenerator.Emit(OpCodes.Ldarg_0);
            getterIlGenerator.Emit(OpCodes.Ldfld, durationField);
            getterIlGenerator.Emit(OpCodes.Ret);

            GetTextSpawnerDuration = (Func<FlyingTextSpawner, float>)getterMethod.CreateDelegate(typeof(Func<FlyingTextSpawner, float>));
        }

        // These methods are automatically called by Unity, you should remove any you aren't using.
        #region Monobehaviour Messages
        /// <summary>
        /// Only ever called once, mainly used to initialize variables.
        /// </summary>
        private void Awake() {
            // For this particular MonoBehaviour, we only want one instance to exist at any time, so store a reference to it in a static property
            //   and destroy any that are created while one already exists.
            if (Instance != null) {
                Plugin.Log?.Warn($"Instance of {GetType().Name} already exists, destroying.");
                GameObject.DestroyImmediate(this);
                return;
            }
            //GameObject.DontDestroyOnLoad(this); // Don't destroy this object on scene changes
            Instance = this;
            Plugin.Log?.Debug($"{name}: Awake()");
        }

        private void Initialize() {
            textSpawner = FindObjectOfType<FlyingTextSpawner>();
            songController = FindObjectOfType<GameSongController>();
            sceneSetup = FindObjectOfType<BeatmapObjectsInstaller>();

            if (songController == null || sceneSetup == null) {
                Plugin.Log?.Warn("GameSongController or GameCoreSceneSetup not found");
                return;
            }
            if (textSpawner == null) {
                Plugin.Log?.Warn("FlyingTextSpawner not found");
                if (ContainerField == null) {
                    Plugin.Log?.Warn("Container field not found");
                    return;
                } else {
                    Plugin.Log?.Debug("Container field found");
                    Plugin.Log?.Debug("Container field type: " + ContainerField.FieldType);
                }

                var installer = FindObjectOfType<GameplayCoreInstaller>();
                if (installer == null) {
                    Plugin.Log?.Warn("GameplayCoreInstaller not found");
                    return;
                }

                var container = (Zenject.DiContainer)ContainerField.GetValue(installer);
                if (container == null) {
                    Plugin.Log?.Warn("Zenject container not found");
                    return;
                }
                textSpawner = container.InstantiateComponentOnNewGameObject<FlyingTextSpawner>("BeatLyricsTextSpawner");
                if (textSpawner == null) {
                    Plugin.Log?.Warn("Failed to instantiate FlyingTextSpawner");
                    return;
                }
                Plugin.Log?.Debug("Created FlyingTextSpawner");
            }

            if (SceneSetupDataField == null) {
                Plugin.Log?.Warn("Cannot find _sceneSetupData field of GameplayCoreSceneSetupData");
                return;
            }

            sceneSetupData = (GameplayCoreSceneSetupData)SceneSetupDataField.GetValue(sceneSetup);
            if (sceneSetupData == null) {
                Plugin.Log?.Warn("GameplayCoreSceneSetupData not found");
                return;
            }

            timeSyncController = (AudioTimeSyncController)AudioTimeSyncField.GetValue(songController);
            if (timeSyncController == null) {
                Plugin.Log?.Warn("AudioTimeSyncController not found");
                return;
            }
            var level = sceneSetupData.difficultyBeatmap.level;
            Plugin.Log?.Debug($"(LevelID): {level.levelID} | Level: {level.songName} - {level.songSubName} - {level.songAuthorName}");
            LyricsFetcher.GetLocalLyrics(level.levelID, lyrics);
            if (lyrics.Count == 0) {
                Plugin.Log?.Debug("No lyrics found");
                SpawnText("Could not find lyrics!", 3f);
                return;
            }
            SpawnText("Found lyrics!", 3f);
            StartCoroutine(DisplayLyrics());
        }

        /// <summary>
        /// Only ever called once on the first frame the script is Enabled. Start is called after any other script's Awake() and before Update().
        /// </summary>
        private void Start() {
            Initialize();
        }

        /// <summary>
        /// Called when the script is being destroyed.
        /// </summary>
        private void OnDestroy() {
            Plugin.Log?.Debug($"{name}: OnDestroy()");
            if (Instance == this)
                Instance = null; // This MonoBehaviour is being destroyed, so set the static instance property to null.

        }

        #endregion

        private IEnumerator DisplayLyrics() {
            var i = 0;
            {
                var currentTime = timeSyncController.songTime;
                while (i < lyrics.Count) {
                    var currentLyrics = lyrics[i];
                    if (currentLyrics.Time > currentTime) break;
                    i++;
                }
            }

            while (i < lyrics.Count) {
                yield return new WaitForSeconds(lyrics[i++].Time - timeSyncController.songTime);
                var currentLyrics = lyrics[i - 1];
                float displayDuration, currentTime = timeSyncController.songTime;
                if (currentLyrics.EndTime.HasValue) displayDuration = currentLyrics.EndTime.Value - currentTime;
                else {
                    displayDuration = i == lyrics.Count ? timeSyncController.songLength - currentTime : lyrics[i].Time - currentTime;
                }

                SpawnText(currentLyrics.Text, displayDuration);
            }
        }

        private void SpawnText(string text, float duration) {
            var initialDuration = GetTextSpawnerDuration(textSpawner);
            SetTextSpawnerDuration(textSpawner, duration);
            textSpawner.SpawnText(new Vector3(0, 4, 0), Quaternion.identity, Quaternion.identity, text);
            if (lyricText) lyricText.text = text;
            SetTextSpawnerDuration(textSpawner, initialDuration);
        }
    }
}

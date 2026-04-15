#pragma warning disable CS0649
using System;
using System.Collections;
using System.Collections.Generic;
using BreathCasino.Core;
using UnityEngine;

namespace BreathCasino.Systems
{
    /// <summary>
    /// Центральный менеджер звука Breath Casino.
    /// Сохраняет старый API, но внутри работает через именованные cue и пул AudioSource.
    /// Это позволяет добавлять новые звуковые сценарии без очередной переписи менеджера.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BCAudioManager : MonoBehaviour
    {
        [Serializable]
        private class AudioCueDefinition
        {
            public string cueId;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            public Vector2 pitchRange = Vector2.one;
            [Range(0f, 1f)] public float spatialBlend = 0f;
        }

        public const string CueAmbient = "ambient";
        public const string CueGunShotLive = "gun.shot.live";
        public const string CueGunShotBlank = "gun.shot.blank";
        public const string CueGunCock = "gun.cock";
        public const string CueGunPutDown = "gun.putdown";
        public const string CueCardPlace = "card.place";
        public const string CueCardDeal = "card.deal";
        public const string CueCardReturn = "card.return";
        public const string CueDrumSpin = "drum.spin";
        public const string CueDrumClose = "drum.close";
        public const string CueDuelWin = "duel.win";
        public const string CueDuelLose = "duel.lose";
        public const string CueCough = "atmo.cough";
        public const string CueTicketEmerge = "ticket.emerge";
        public const string CueTicketUse = "ticket.use";
        public const string CueSpecialCancel = "special.cancel";
        public const string CueSpecialSteal = "special.steal";
        public const string CueSpecialDuplicate = "special.duplicate";
        public const string CueSpecialExchange = "special.exchange";
        public const string CueMechanismRise = "mechanism.rise";
        public const string CueMechanismLower = "mechanism.lower";
        public const string CueTurnChange = "turn.change";
        public const string CueO2Refill = "o2.refill";
        public const string CueO2Critical = "o2.critical";
        public const string CueO2LastBreath = "o2.lastbreath";
        public const string CueHpDamage = "hp.damage";
        public const string CueHpHeal = "hp.heal";
        public const string CueCameraTransition = "camera.transition";
        public const string CueUiHover = "ui.hover";
        public const string CueUiClick = "ui.click";
        public const string CueUiInvalid = "ui.invalid";

        public static BCAudioManager Instance { get; private set; }

        [Header("Ambient")]
        [Tooltip("Фоновый ambient трек. loop=true, воспроизводится при старте.")]
        [SerializeField] private AudioClip ambientClip;
        [SerializeField] [Range(0f, 1f)] private float ambientVolume = 0.25f;

        [Header("SFX - Gun")]
        [SerializeField] private AudioClip gunShotLive;
        [SerializeField] private AudioClip gunShotBlank;
        [SerializeField] private AudioClip gunCock;
        [SerializeField] private AudioClip gunPutDown;

        [Header("SFX - Cards")]
        [SerializeField] private AudioClip cardPlace;
        [SerializeField] private AudioClip cardDeal;
        [SerializeField] private AudioClip cardReturn;

        [Header("SFX - Revolver")]
        [SerializeField] private AudioClip drumSpin;
        [SerializeField] private AudioClip drumClose;

        [Header("SFX - Duel")]
        [SerializeField] private AudioClip duelWin;
        [SerializeField] private AudioClip duelLose;

        [Header("Atmosphere")]
        [SerializeField] private AudioClip[] coughClips;
        [SerializeField] [Range(5f, 120f)] private float coughIntervalMin = 18f;
        [SerializeField] [Range(5f, 120f)] private float coughIntervalMax = 45f;
        [SerializeField] [Range(0f, 1f)] private float coughVolume = 0.55f;

        [Header("SFX - Tickets")]
        [SerializeField] private AudioClip ticketEmerge;
        [SerializeField] private AudioClip ticketUse;

        [Header("SFX - Special Cards")]
        [SerializeField] private AudioClip specialCancel;
        [SerializeField] private AudioClip specialSteal;
        [SerializeField] private AudioClip specialDuplicate;
        [SerializeField] private AudioClip specialExchange;

        [Header("SFX - Mechanism")]
        [SerializeField] private AudioClip mechanismRise;
        [SerializeField] private AudioClip mechanismLower;

        [Header("SFX - Turn")]
        [SerializeField] private AudioClip turnChange;

        [Header("SFX - O2")]
        [SerializeField] private AudioClip o2Refill;
        [SerializeField] private AudioClip o2Critical;
        [SerializeField] private AudioClip o2LastBreath;

        [Header("SFX - HP")]
        [SerializeField] private AudioClip hpDamage;
        [SerializeField] private AudioClip hpHeal;

        [Header("SFX - Camera")]
        [SerializeField] private AudioClip cameraTransition;

        [Header("SFX - UI")]
        [SerializeField] private AudioClip uiHover;
        [SerializeField] private AudioClip uiClick;
        [SerializeField] private AudioClip uiInvalid;

        [Header("Flex Audio")]
        [Tooltip("Дополнительные именованные cue. Можно расширять систему без изменения публичного API.")]
        [SerializeField] private AudioCueDefinition[] additionalCues;
        [SerializeField] [Min(1)] private int sfxSourcePoolSize = 4;
        [SerializeField] [Range(0f, 1f)] private float masterAmbientVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float masterSfxVolume = 1f;

        private readonly List<AudioSource> _sfxSources = new();
        private readonly Dictionary<string, AudioCueDefinition> _cueLookup = new(StringComparer.OrdinalIgnoreCase);

        private AudioSource _ambientSource;
        private GameManager _gm;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // DontDestroyOnLoad работает только на root-объектах.
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            DontDestroyOnLoad(gameObject);

            _ambientSource = GetComponent<AudioSource>();
            _ambientSource.loop = true;
            _ambientSource.spatialBlend = 0f;
            _ambientSource.volume = ambientVolume;
            _ambientSource.playOnAwake = false;

            EnsureSfxPool();
            RebuildCueLookup();
        }

        private void OnValidate()
        {
            RebuildCueLookup();
        }

        private void Start()
        {
            PlayAmbient();
            StartCoroutine(RandomCoughLoop());
            SubscribeToGameManager();
        }

        private void OnDestroy()
        {
            if (_gm != null)
            {
                _gm.OnPhaseChanged -= HandlePhase;
            }
        }

        private void SubscribeToGameManager()
        {
            _gm = FindFirstObjectByType<GameManager>();
            if (_gm != null)
            {
                _gm.OnPhaseChanged -= HandlePhase;
                _gm.OnPhaseChanged += HandlePhase;
            }
            else
            {
                Debug.LogWarning("[AudioManager] GameManager not found - phase sounds disabled.");
            }
        }

        private void HandlePhase(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.BulletReveal:
                    TryPlayCue(CueDrumSpin);
                    break;

                case GamePhase.Dealing:
                    TryPlayCue(CueMechanismRise);
                    TryPlayCue(CueCardDeal);
                    break;

                case GamePhase.Shooting:
                    TryPlayCue(CueGunCock);
                    break;

                case GamePhase.Resolution:
                    TryPlayCue(CueMechanismLower);
                    break;
            }
        }

        public void PlayGunShot(BulletType bulletType)
        {
            TryPlayCue(bulletType == BulletType.Blank ? CueGunShotBlank : CueGunShotLive);
        }

        public void PlayGunPickup() => TryPlayCue(CueGunCock);
        public void PlayGunReturn() => TryPlayCue(CueGunPutDown);
        public void PlayCardPlace() => TryPlayCue(CueCardPlace);
        public void PlayCardReturn() => TryPlayCue(CueCardReturn);
        public void PlayDrumClose() => TryPlayCue(CueDrumClose);
        public void PlayDuelWin() => TryPlayCue(CueDuelWin);
        public void PlayDuelLose() => TryPlayCue(CueDuelLose);
        public void PlayTicketEmerge() => TryPlayCue(CueTicketEmerge);
        public void PlayTicketUse() => TryPlayCue(CueTicketUse);
        public void PlaySpecialCancel() => TryPlayCue(CueSpecialCancel);
        public void PlaySpecialSteal() => TryPlayCue(CueSpecialSteal);
        public void PlaySpecialDuplicate() => TryPlayCue(CueSpecialDuplicate);
        public void PlaySpecialExchange() => TryPlayCue(CueSpecialExchange);
        public void PlayMechanismRise() => TryPlayCue(CueMechanismRise);
        public void PlayMechanismLower() => TryPlayCue(CueMechanismLower);
        public void PlayTurnChange() => TryPlayCue(CueTurnChange);
        public void PlayO2Refill() => TryPlayCue(CueO2Refill);
        public void PlayO2Critical() => TryPlayCue(CueO2Critical);
        public void PlayO2LastBreath() => TryPlayCue(CueO2LastBreath);
        public void PlayHPDamage() => TryPlayCue(CueHpDamage);
        public void PlayHPHeal() => TryPlayCue(CueHpHeal);
        public void PlayCameraTransition() => TryPlayCue(CueCameraTransition);
        public void PlayUIHover() => TryPlayCue(CueUiHover);
        public void PlayUIClick() => TryPlayCue(CueUiClick);
        public void PlayUIInvalid() => TryPlayCue(CueUiInvalid);

        public bool HasCue(string cueId)
        {
            return !string.IsNullOrWhiteSpace(cueId) && _cueLookup.ContainsKey(cueId);
        }

        public bool TryPlayCue(string cueId, float volumeScale = 1f)
        {
            if (string.IsNullOrWhiteSpace(cueId) || !_cueLookup.TryGetValue(cueId, out AudioCueDefinition cue))
            {
                return false;
            }

            return PlayCue(cue, volumeScale);
        }

        public bool TryPlayCueAtPoint(string cueId, Vector3 worldPosition, float volumeScale = 1f)
        {
            if (string.IsNullOrWhiteSpace(cueId) || !_cueLookup.TryGetValue(cueId, out AudioCueDefinition cue))
            {
                return false;
            }

            AudioClip clip = PickClip(cue);
            if (clip == null)
            {
                return false;
            }

            AudioSource.PlayClipAtPoint(clip, worldPosition, Mathf.Clamp01(cue.volume * masterSfxVolume * volumeScale));
            return true;
        }

        public void PlayCustomClip(AudioClip clip, float volume = 1f, float pitch = 1f, float spatialBlend = 0f)
        {
            if (clip == null)
            {
                return;
            }

            EnsureSfxPool();
            AudioSource source = GetAvailableSfxSource();
            if (source == null)
            {
                return;
            }

            source.pitch = pitch;
            source.spatialBlend = spatialBlend;
            source.PlayOneShot(clip, Mathf.Clamp01(volume * masterSfxVolume));
        }

        public void SetAmbientClip(AudioClip clip, bool playImmediately = true)
        {
            ambientClip = clip;
            RebuildCueLookup();

            if (playImmediately)
            {
                PlayAmbient();
            }
        }

        public void StopAmbient()
        {
            if (_ambientSource != null && _ambientSource.isPlaying)
            {
                _ambientSource.Stop();
            }
        }

        private IEnumerator RandomCoughLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(coughIntervalMin, coughIntervalMax));
                TryPlayCue(CueCough);
            }
        }

        private void PlayAmbient()
        {
            if (_ambientSource == null || !_cueLookup.TryGetValue(CueAmbient, out AudioCueDefinition cue))
            {
                return;
            }

            AudioClip clip = PickClip(cue);
            if (clip == null)
            {
                return;
            }

            _ambientSource.Stop();
            _ambientSource.clip = clip;
            _ambientSource.pitch = GetPitch(cue);
            _ambientSource.spatialBlend = cue.spatialBlend;
            _ambientSource.volume = Mathf.Clamp01(cue.volume * masterAmbientVolume);
            _ambientSource.loop = true;
            _ambientSource.Play();
        }

        private void EnsureSfxPool()
        {
            while (_sfxSources.Count < Mathf.Max(1, sfxSourcePoolSize))
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.loop = false;
                source.spatialBlend = 0f;
                source.playOnAwake = false;
                source.volume = 1f;
                _sfxSources.Add(source);
            }
        }

        private AudioSource GetAvailableSfxSource()
        {
            EnsureSfxPool();

            for (int i = 0; i < _sfxSources.Count; i++)
            {
                if (!_sfxSources[i].isPlaying)
                {
                    return _sfxSources[i];
                }
            }

            return _sfxSources[0];
        }

        private void RebuildCueLookup()
        {
            _cueLookup.Clear();

            RegisterCue(new AudioCueDefinition { cueId = CueAmbient, clips = ToArray(ambientClip), volume = ambientVolume, pitchRange = Vector2.one, spatialBlend = 0f });
            RegisterCue(new AudioCueDefinition { cueId = CueGunShotLive, clips = ToArray(gunShotLive) });
            RegisterCue(new AudioCueDefinition { cueId = CueGunShotBlank, clips = ToArray(gunShotBlank) });
            RegisterCue(new AudioCueDefinition { cueId = CueGunCock, clips = ToArray(gunCock) });
            RegisterCue(new AudioCueDefinition { cueId = CueGunPutDown, clips = ToArray(gunPutDown) });
            RegisterCue(new AudioCueDefinition { cueId = CueCardPlace, clips = ToArray(cardPlace) });
            RegisterCue(new AudioCueDefinition { cueId = CueCardDeal, clips = ToArray(cardDeal) });
            RegisterCue(new AudioCueDefinition { cueId = CueCardReturn, clips = ToArray(cardReturn) });
            RegisterCue(new AudioCueDefinition { cueId = CueDrumSpin, clips = ToArray(drumSpin) });
            RegisterCue(new AudioCueDefinition { cueId = CueDrumClose, clips = ToArray(drumClose) });
            RegisterCue(new AudioCueDefinition { cueId = CueDuelWin, clips = ToArray(duelWin) });
            RegisterCue(new AudioCueDefinition { cueId = CueDuelLose, clips = ToArray(duelLose) });
            RegisterCue(new AudioCueDefinition { cueId = CueCough, clips = coughClips, volume = coughVolume });
            RegisterCue(new AudioCueDefinition { cueId = CueTicketEmerge, clips = ToArray(ticketEmerge) });
            RegisterCue(new AudioCueDefinition { cueId = CueTicketUse, clips = ToArray(ticketUse) });
            RegisterCue(new AudioCueDefinition { cueId = CueSpecialCancel, clips = ToArray(specialCancel) });
            RegisterCue(new AudioCueDefinition { cueId = CueSpecialSteal, clips = ToArray(specialSteal) });
            RegisterCue(new AudioCueDefinition { cueId = CueSpecialDuplicate, clips = ToArray(specialDuplicate) });
            RegisterCue(new AudioCueDefinition { cueId = CueSpecialExchange, clips = ToArray(specialExchange) });
            RegisterCue(new AudioCueDefinition { cueId = CueMechanismRise, clips = ToArray(mechanismRise) });
            RegisterCue(new AudioCueDefinition { cueId = CueMechanismLower, clips = ToArray(mechanismLower) });
            RegisterCue(new AudioCueDefinition { cueId = CueTurnChange, clips = ToArray(turnChange) });
            RegisterCue(new AudioCueDefinition { cueId = CueO2Refill, clips = ToArray(o2Refill) });
            RegisterCue(new AudioCueDefinition { cueId = CueO2Critical, clips = ToArray(o2Critical) });
            RegisterCue(new AudioCueDefinition { cueId = CueO2LastBreath, clips = ToArray(o2LastBreath) });
            RegisterCue(new AudioCueDefinition { cueId = CueHpDamage, clips = ToArray(hpDamage) });
            RegisterCue(new AudioCueDefinition { cueId = CueHpHeal, clips = ToArray(hpHeal) });
            RegisterCue(new AudioCueDefinition { cueId = CueCameraTransition, clips = ToArray(cameraTransition) });
            RegisterCue(new AudioCueDefinition { cueId = CueUiHover, clips = ToArray(uiHover) });
            RegisterCue(new AudioCueDefinition { cueId = CueUiClick, clips = ToArray(uiClick) });
            RegisterCue(new AudioCueDefinition { cueId = CueUiInvalid, clips = ToArray(uiInvalid) });

            if (additionalCues == null)
            {
                return;
            }

            for (int i = 0; i < additionalCues.Length; i++)
            {
                RegisterCue(additionalCues[i]);
            }
        }

        private void RegisterCue(AudioCueDefinition cue)
        {
            if (cue == null || string.IsNullOrWhiteSpace(cue.cueId) || cue.clips == null || cue.clips.Length == 0)
            {
                return;
            }

            _cueLookup[cue.cueId] = cue;
        }

        private bool PlayCue(AudioCueDefinition cue, float volumeScale)
        {
            AudioClip clip = PickClip(cue);
            if (clip == null)
            {
                return false;
            }

            EnsureSfxPool();
            AudioSource source = GetAvailableSfxSource();
            if (source == null)
            {
                return false;
            }

            source.pitch = GetPitch(cue);
            source.spatialBlend = cue.spatialBlend;
            source.PlayOneShot(clip, Mathf.Clamp01(cue.volume * masterSfxVolume * volumeScale));
            return true;
        }

        private static AudioClip PickClip(AudioCueDefinition cue)
        {
            if (cue == null || cue.clips == null || cue.clips.Length == 0)
            {
                return null;
            }

            int index = cue.clips.Length == 1 ? 0 : UnityEngine.Random.Range(0, cue.clips.Length);
            return cue.clips[index];
        }

        private static float GetPitch(AudioCueDefinition cue)
        {
            if (cue == null)
            {
                return 1f;
            }

            float min = cue.pitchRange.x <= 0f ? 1f : cue.pitchRange.x;
            float max = cue.pitchRange.y <= 0f ? 1f : cue.pitchRange.y;
            if (max < min)
            {
                (min, max) = (max, min);
            }

            return Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
        }

        private static AudioClip[] ToArray(AudioClip clip)
        {
            return clip != null ? new[] { clip } : Array.Empty<AudioClip>();
        }
    }
}
#pragma warning restore CS0649

#pragma warning disable CS0649
using System;
using System.Collections;
using System.Collections.Generic;
using BreathCasino.Core;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    public class BCEnemyDialogueController : MonoBehaviour
    {
        [Serializable]
        public struct DialogueSet
        {
            public string key;
            public string[] lineKeys;
            [TextArea] public string[] legacyLines;
            public float duration;
        }

        [SerializeField] private Transform enemyRoot;
        [SerializeField] private bool placeBelowEnemy;
        [SerializeField] private BCWorldSpaceDialogueDisplay display;
        [SerializeField] private float sequenceGap = 0.25f;
        [SerializeField] private DialogueSet[] scriptedSets =
        {
            new DialogueSet
            {
                key = "opening_monologue",
                lineKeys = new[] { "dialog.enemy.intro.opening.1", "dialog.enemy.intro.opening.2" },
                duration = 2.9f
            },
            new DialogueSet
            {
                key = "intro_opening",
                lineKeys = new[] { "dialog.enemy.intro.opening.1", "dialog.enemy.intro.opening.2" },
                duration = 2.9f
            },
            new DialogueSet
            {
                key = "intro_bullets",
                lineKeys = new[] { "dialog.enemy.intro.bullets.1", "dialog.enemy.intro.bullets.2" },
                duration = 2.9f
            },
            new DialogueSet
            {
                key = "intro_lever",
                lineKeys = new[] { "dialog.enemy.intro.lever.1", "dialog.enemy.intro.lever.2" },
                duration = 2.7f
            },
            new DialogueSet
            {
                key = "post_shot_reflection",
                lineKeys = new[]
                {
                    "dialog.enemy.post_shot_reflection.1",
                    "dialog.enemy.post_shot_reflection.2",
                    "dialog.enemy.post_shot_reflection.3",
                    "dialog.enemy.post_shot_reflection.4",
                    "dialog.enemy.post_shot_reflection.5"
                },
                duration = 3.05f
            },
            new DialogueSet { key = "phase_attack", lineKeys = new[] { "dialog.enemy.phase_attack.1", "dialog.enemy.phase_attack.2" }, duration = 2.2f },
            new DialogueSet { key = "phase_defense", lineKeys = new[] { "dialog.enemy.phase_defense.1", "dialog.enemy.phase_defense.2" }, duration = 2.2f },
            new DialogueSet { key = "player_open_ticket", lineKeys = new[] { "dialog.enemy.player_open_ticket.1", "dialog.enemy.player_open_ticket.2" }, duration = 2.2f },
            new DialogueSet { key = "enemy_use_ticket", lineKeys = new[] { "dialog.enemy.enemy_use_ticket.1", "dialog.enemy.enemy_use_ticket.2" }, duration = 2.3f },
            new DialogueSet { key = "player_last_breath", lineKeys = new[] { "dialog.enemy.player_last_breath.1", "dialog.enemy.player_last_breath.2" }, duration = 2.5f },
            new DialogueSet { key = "round_start", lineKeys = new[] { "dialog.enemy.round_start.1", "dialog.enemy.round_start.2" }, duration = 2.0f },
            new DialogueSet { key = "game_over", lineKeys = new[] { "dialog.enemy.game_over.1", "dialog.enemy.game_over.2" }, duration = 2.8f }
        };

        private readonly Dictionary<string, DialogueSet> _sets = new(StringComparer.OrdinalIgnoreCase);
        private Coroutine _sequenceRoutine;

        public void Initialize(Transform root, Camera worldCamera)
        {
            enemyRoot = root != null ? root : enemyRoot;
            RebuildDictionary();

            if (display == null)
            {
                display = GetComponentInChildren<BCWorldSpaceDialogueDisplay>(true);
                if (display == null)
                {
                    GameObject displayObject = new GameObject("EnemyDialogueDisplay");
                    displayObject.transform.SetParent(transform, false);
                    display = displayObject.AddComponent<BCWorldSpaceDialogueDisplay>();
                }
            }

            display.Initialize(enemyRoot != null ? enemyRoot : transform, worldCamera, placeBelowEnemy);
        }

        public void Speak(string key)
        {
            if (!TryGetRandomLine(key, out string line, out float duration))
            {
                return;
            }

            display?.Show(line, duration);
        }

        public void SpeakLine(string line, float duration = 2.4f)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            display?.Show(line, duration);
        }

        public void SpeakLineKey(string lineKey, float duration = 2.4f)
        {
            SpeakLine(LocalizeLineKey(lineKey), duration);
        }

        public void PlaySequence(string key)
        {
            if (!TryGetLocalizedSet(key, out string[] lines, out float duration))
            {
                return;
            }

            if (_sequenceRoutine != null)
            {
                StopCoroutine(_sequenceRoutine);
            }

            _sequenceRoutine = StartCoroutine(PlaySequenceRoutine(lines, duration));
        }

        public bool TryGetLocalizedSet(string key, out string[] lines, out float duration)
        {
            lines = Array.Empty<string>();
            duration = 2.4f;

            if (!_sets.TryGetValue(key, out DialogueSet set))
            {
                return false;
            }

            duration = set.duration <= 0f ? 2.4f : set.duration;
            List<string> resolved = new List<string>();

            if (set.lineKeys != null)
            {
                for (int i = 0; i < set.lineKeys.Length; i++)
                {
                    string line = LocalizeLineKey(set.lineKeys[i]);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        resolved.Add(line);
                    }
                }
            }

            if (resolved.Count == 0 && set.legacyLines != null)
            {
                for (int i = 0; i < set.legacyLines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(set.legacyLines[i]))
                    {
                        resolved.Add(set.legacyLines[i]);
                    }
                }
            }

            lines = resolved.ToArray();
            return lines.Length > 0;
        }

        private void Awake()
        {
            RebuildDictionary();
        }

        private void RebuildDictionary()
        {
            _sets.Clear();
            if (scriptedSets == null)
            {
                return;
            }

            for (int i = 0; i < scriptedSets.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(scriptedSets[i].key))
                {
                    _sets[scriptedSets[i].key] = scriptedSets[i];
                }
            }
        }

        private bool TryGetRandomLine(string key, out string line, out float duration)
        {
            line = null;
            duration = 2.4f;

            if (!TryGetLocalizedSet(key, out string[] lines, out duration))
            {
                return false;
            }

            line = lines[UnityEngine.Random.Range(0, lines.Length)];
            return !string.IsNullOrWhiteSpace(line);
        }

        private string LocalizeLineKey(string lineKey)
        {
            if (string.IsNullOrWhiteSpace(lineKey))
            {
                return string.Empty;
            }

            string localized = BCLocalization.Get(lineKey);
            return string.Equals(localized, lineKey, StringComparison.Ordinal) ? string.Empty : localized;
        }

        private IEnumerator PlaySequenceRoutine(IReadOnlyList<string> lines, float duration)
        {
            float lineDuration = duration <= 0f ? 2.4f : duration;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                display?.Show(line, lineDuration);
                yield return new WaitForSeconds(lineDuration + Mathf.Max(sequenceGap, 0f));
            }

            _sequenceRoutine = null;
        }
    }
}
#pragma warning restore CS0649

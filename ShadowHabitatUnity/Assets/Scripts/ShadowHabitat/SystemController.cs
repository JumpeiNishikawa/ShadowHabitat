using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowHabitat
{
    /// <summary>
    /// Handles the background-learning handshake initiated by Python.
    ///
    /// OSC in (consumed via uOSC.uOscServer on the same GameObject):
    ///   /system/learn_start &lt;duration:float&gt;  -> hide agents + show solid white
    ///   /system/learn_end                       -> restore normal rendering
    ///
    /// Also auto-ends after the requested duration in case learn_end is dropped.
    /// </summary>
    [RequireComponent(typeof(uOSC.uOscServer))]
    public class SystemController : MonoBehaviour
    {
        [Tooltip("Renderers / GameObjects to disable during background learning (the agents).")]
        public List<GameObject> hideDuringLearn = new ();

        [Tooltip("Optional: a full-surface white quad to enable during learning. Stays disabled normally.")]
        public GameObject learnFlashSurface;

        [Tooltip("Safety cap if learn_end is never received.")]
        public float maxLearnSeconds = 30f;

        bool _learning;
        Coroutine _autoEndCo;

        void Awake()
        {
            var srv = GetComponent<uOSC.uOscServer>();
            Debug.Log($"[SystemController:{name}] Awake.  uOscServer={(srv != null ? "OK" : "NULL")}  " +
                      $"hideDuringLearn.Count={hideDuringLearn.Count}  " +
                      $"learnFlashSurface={(learnFlashSurface != null ? learnFlashSurface.name : "NULL")}");
            for (int i = 0; i < hideDuringLearn.Count; i++)
            {
                var go = hideDuringLearn[i];
                Debug.Log($"  hideDuringLearn[{i}] = " +
                          (go == null ? "NULL  <-- empty slot, drag CircleAgent here!"
                                      : $"{go.name}  active={go.activeSelf}"));
            }
            if (srv != null) srv.onDataReceived.AddListener(OnDataReceived);
            SetLearningVisible(false);
        }

        void OnDataReceived(uOSC.Message msg)
        {
            // Only log /system/* to keep the console readable.
            if (msg.address != null && msg.address.StartsWith("/system/"))
            {
                Debug.Log($"[SystemController] received OSC {msg.address}  " +
                          $"values.Length={(msg.values != null ? msg.values.Length : 0)}");
            }
            switch (msg.address)
            {
                case "/system/learn_start":
                    float duration = (msg.values != null && msg.values.Length >= 1)
                        ? msg.values[0].GetFloat()
                        : 5f;
                    BeginLearning(duration);
                    break;
                case "/system/learn_end":
                    EndLearning();
                    break;
            }
        }

        public void BeginLearning(float seconds)
        {
            _learning = true;
            SetLearningVisible(true);
            if (_autoEndCo != null) StopCoroutine(_autoEndCo);
            _autoEndCo = StartCoroutine(AutoEndAfter(Mathf.Min(seconds + 1f, maxLearnSeconds)));
            Debug.Log($"[SystemController] BeginLearning {seconds:F1}s");
        }

        public void EndLearning()
        {
            if (!_learning) return;
            _learning = false;
            SetLearningVisible(false);
            if (_autoEndCo != null) { StopCoroutine(_autoEndCo); _autoEndCo = null; }
            Debug.Log("[SystemController] EndLearning");
        }

        IEnumerator AutoEndAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_learning)
            {
                Debug.LogWarning("[SystemController] Auto-ending learn (no learn_end received in time).");
                EndLearning();
            }
        }

        void SetLearningVisible(bool learning)
        {
            int toggled = 0, nulled = 0;
            foreach (var go in hideDuringLearn)
            {
                if (go == null) { nulled++; continue; }
                go.SetActive(!learning);
                toggled++;
            }
            if (learnFlashSurface != null) learnFlashSurface.SetActive(learning);
            Debug.Log($"[SystemController] SetLearningVisible({learning})  " +
                      $"toggled={toggled}  null_entries={nulled}  " +
                      $"flashSurface={(learnFlashSurface != null ? (learning ? "shown" : "hidden") : "<none>")}");
        }
    }
}

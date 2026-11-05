// Split out of BlindsideUIAnim.cs: Unity only serializes one MonoScript "main class" per file
// reliably, so every MonoBehaviour that gets AddComponent'd (and, above all, ever saved into a
// prefab) needs its own file, or its component reference silently rots into "Missing (Mono Script)"
// on the next reload. Body copied verbatim from the original file.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    // ------------------------------------------------------------------
    //  Staggered entrance (roster cards, command menu, squad list, logo letters)
    // ------------------------------------------------------------------
    /// <summary>
    /// Put on a container. On enable every active child slides in with a delay step.
    /// If children are driven by a LayoutGroup, keep animateInner = true and give each
    /// child a single inner visual object (the layout moves the child, we move the inner).
    /// </summary>
    public class UIStaggerIn : MonoBehaviour
    {
        public Vector2 fromOffset = new Vector2(-18f, 0f);   // cmdIn: (-18,0), slideInL: (-40,0), slideInR: (40,0)
        public float duration = .3f;
        public float step = .035f;                           // cmdIn .035, roster .06, logo letters .045
        public float startDelay = 0f;
        public bool animateInner = true;
        readonly Dictionary<Transform, Vector3> basePos = new Dictionary<Transform, Vector3>();

        void OnEnable() { Play(); }
        public void Play()
        {
            if (!Application.isPlaying) return;   // edit-mode previews show the children where they rest
            StopAllCoroutines();
            int i = 0;
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                Transform target = animateInner && child.childCount > 0 ? child.GetChild(0) : child;
                if (!basePos.ContainsKey(target)) basePos[target] = target.localPosition;
                var cg = UIUtil.GetOrAdd<CanvasGroup>(target.gameObject);
                StartCoroutine(UIAnim.SlideFade(target, cg, basePos[target], fromOffset, duration, startDelay + step * i));
                i++;
            }
        }
    }
}

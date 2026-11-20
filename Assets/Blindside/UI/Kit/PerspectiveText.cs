// The perspective of a CardPerspective card on a TMP text's glyph quads: every time TMP has made the text's mesh afresh
// (TMPro_EventManager TEXT_CHANGED_EVENT) its vertices are moved once and uploaded again (UpdateVertexData). Only a fresh mesh is
// moved — moving the same vertices twice would push the text off its place. Nothing per frame.
using TMPro;
using UnityEngine;

namespace Blindside.UI
{
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways]   // (the previews build the card in edit mode)
    public class PerspectiveText : MonoBehaviour
    {
        TMP_Text text;
        CardPerspective card;
        bool warping;

        void OnEnable()
        {
            text = GetComponent<TMP_Text>();
            card = GetComponentInParent<CardPerspective>();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            text.ForceMeshUpdate();     // (a fresh mesh: TEXT_CHANGED warps it once — no second Warp here, that would turn it twice)
        }

        void OnDisable() { TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged); }

        void OnTextChanged(Object obj) { if (obj == text) Warp(); }

        void Warp()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (warping || text == null) return;
            if (card == null) card = GetComponentInParent<CardPerspective>(true);   // (found lazily: at OnEnable it may not be reachable yet)
            if (card == null || !card.enabled) return;
            warping = true;
            var info = text.textInfo;
            var toCard = card.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            var back = toCard.inverse;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var verts = info.meshInfo[m].vertices;
                int n = info.meshInfo[m].vertexCount;
                if (verts == null) continue;
                for (int i = 0; i < n && i < verts.Length; i++) verts[i] = back.MultiplyPoint3x4(card.Project(toCard.MultiplyPoint3x4(verts[i])));
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            warping = false;
        }
    }
}

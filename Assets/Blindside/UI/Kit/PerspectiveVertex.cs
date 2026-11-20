// The turn of a CardPerspective card on one Graphic's vertices (added after the shape effects — SlantedGraphic, CutCornerGraphic — so it
// runs last). Runs only when the graphic's mesh is rebuilt; nothing per frame.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class PerspectiveVertex : BaseMeshEffect
    {
        CardPerspective card;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            if (card == null) card = GetComponentInParent<CardPerspective>(true);
            if (card == null || !card.enabled) return;
            var toCard = card.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            var back = toCard.inverse;
            UIVertex v = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position = back.MultiplyPoint3x4(card.Project(toCard.MultiplyPoint3x4(v.position)));
                vh.SetUIVertex(v, i);
            }
        }
    }
}

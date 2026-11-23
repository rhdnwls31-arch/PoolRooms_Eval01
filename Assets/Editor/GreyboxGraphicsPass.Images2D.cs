using UnityEditor;
using UnityEngine;

// 2026-11-23 VARCO 2D 이미지(GPT 이미지)로 그레이박스 더미 그림을 바꾼다.
// 이미지는 방 폴더 안의 2D 폴더에 둔다: Assets/VARCO/<방>/2D/VAR_<방>_<이름>.png.
// 여러 디자인이 한 장에 있는 시트(대화상자·창)는 2D/Sheets에 원본을 두고, 쓸 것만 잘라 2D/VAR_<방>_<이름>_Cut.png로 쓴다.
// 이미지가 없으면 null을 돌려주고, 부르는 쪽은 예전 그림(코드로 만든 텍스처)을 쓴다.
public static partial class GreyboxGraphicsPass
{
    /// <summary>방 2D 폴더의 이미지. 없으면 null.</summary>
    static Texture2D Img2D(string room, string name) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/VARCO/{room}/2D/VAR_{room}_{name}.png");

    /// <summary>이미지 가로/세로 비율(원본 픽셀 기준).</summary>
    static float Img2DAspect(Texture2D t)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t));
        if (imp != null) { imp.GetSourceTextureWidthAndHeight(out int w, out int h); if (h > 0) return w / (float)h; }
        return t.width / (float)t.height;
    }

    // ---------------------------------------------------------------- 금 간 복도: 벽을 가리키는 화살표
    // 씬에 손으로 놓은 회색 판(GB_02_Crack/Placeholder_Props/Sign_Arrow_ToWall, 0.9×0.3, 남쪽 벽)이다. 빌더가 만들지 않으므로
    // Apply 끝에서 판을 이미지 비율(16:9)의 받침으로 줄이고 앞(복도 쪽 = +Z)에 그림 판을 붙인다. 다시 실행해도 같은 결과.
    const string ArrowFaceName = "Sign_Arrow_ToWall_Face";
    static void ApplyCrackArrowImage()
    {
        var tex = Img2D("Crack", "ArrowSign");
        var sign = GameObject.Find("GB_02_Crack/Placeholder_Props/Sign_Arrow_ToWall");
        if (tex == null || sign == null) return;
        var parent = sign.transform.parent;
        var old = parent.Find(ArrowFaceName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        float h = 0.34f, w = h * Img2DAspect(tex);
        sign.transform.localScale = new Vector3(w + 0.03f, h + 0.03f, 0.03f); // 받침(테두리 1.5cm)
        var r = sign.GetComponent<MeshRenderer>();
        var mat = Lit("GB_Crack_ArrowSign_Img", m => { Plain(m, Color.white, 0.5f); m.SetTexture("_BaseColorMap", tex); });
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = ArrowFaceName;
        q.transform.SetParent(parent, true);
        q.transform.SetPositionAndRotation(sign.transform.position + Vector3.forward * 0.017f, Quaternion.LookRotation(Vector3.back)); // 보이는 면(-Z)이 복도(+Z)를 본다
        q.transform.localScale = new Vector3(w, h, 1f);
        var qr = q.GetComponent<MeshRenderer>();
        qr.sharedMaterial = mat; qr.renderingLayerMask = r.renderingLayerMask;
        EditorUtility.SetDirty(sign);
    }
}

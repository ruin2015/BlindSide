using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// (Blindside: the intro's OPERATION BLINDSIDE — IntroSequence.TitleOn adds this and plays it with Resources/Intro/IntroOperationTitle.png.
//  ITimedEffect.Evaluate(t) holds one moment, for checks; Play() runs it again on the unscaled clock.)
[ExecuteAlways]
public sealed class BlindsideTitle : MonoBehaviour, Blindside.UI.ITimedEffect
{
    private float posedTime = -1;
    /// <summary>この時刻で止めて描画する（Play() で解除）。</summary>
    public void Evaluate(float t) { posedTime = t; running = false; }
    [Tooltip("背景が透明な、白い文字やロゴの画像を入れてね。")]
    public Texture2D titleTexture;
    [Range(.2f, 1f)] public float widthFraction = .48f;
    public float verticalCenter = .4f;
    [Min(0f)] public float settleSeconds = .87f;
    public bool playOnEnable = true;

    [System.Serializable]
    public class Pulse
    {
        [Min(0)] public float startSeconds, endSeconds;
        [Min(0)] public float strength;
        [Tooltip("各破片の左右の向きを反転")] public bool mirrorX;
        [Tooltip("各破片の上下の向きを反転")] public bool mirrorY;
        /// <summary>1回分のグリッチの表示区間、強さ、左右反転を初期化する。</summary>
        /// <param name="from">開始時刻（秒）。</param>
        /// <param name="until">終了時刻（秒）。</param>
        /// <param name="amount">破片の飛距離や色ずれに掛ける倍率。</param>
        /// <param name="mirror">左右の移動方向を反転するか。</param>
        public Pulse(float from, float until, float amount, bool mirror)
        {
            startSeconds=from; endSeconds=until; strength=amount; mirrorX=mirror;
        }
        /// <summary>指定した演出時刻がこのグリッチの表示区間に入るか判定する。</summary>
        /// <param name="t">演出開始からの秒数。</param>
        /// <returns>開始秒以上、終了秒未満なら true。</returns>
        public bool Contains(float t) => t >= startSeconds && t < endSeconds;
    }

    [Tooltip("開始秒～終了秒。重なる場合は先の番号を優先。")]
    public Pulse firstPulse = new Pulse(.9f, 1.2f, .5f, false);
    public Pulse secondPulse = new Pulse(1.5f, 1.63f, 1, true);
    public Pulse thirdPulse = new Pulse(1.63f, 1.92f, 1, false);
    [Range(1,32)] public int columns=12;
    [Range(1,12)] public int rows=4;
    [Tooltip("飛距離の倍率。Xは横、Yは縦。1が元の距離。")]
    public Vector2 travelScale=Vector2.one;
    [Min(0), Tooltip("破片内の横帯のずれ幅の倍率")] public float tearWidth=1;
    [Range(0,1), Tooltip("短い帯の長さ。通常の帯に対する割合。")] public float shortBandLength=2f/3f;
    [Range(1,16), Tooltip("強いグリッチで何本に1本を短くするか")] public int shortBandEvery=4;
    public Color firstColor=new Color32(240,35,81,255);
    [Tooltip("色のずれ幅。基準幅に対する%。X正で右、Y正で下。")]
    public Vector2 firstColorOffsetPercent=new Vector2(-.77f,0);
    public Color secondColor=new Color32(57,186,255,255);
    public Vector2 secondColorOffsetPercent=new Vector2(.91f,0);
    private double start;
    private bool running;
#if UNITY_EDITOR
    [System.NonSerialized] internal float previewTime = -1;
#endif
    private float OutroDelay => Mathf.Max(0, Mathf.Max(firstPulse.endSeconds, Mathf.Max(secondPulse.endSeconds,thirdPulse.endSeconds))-2.64f);
    /// <summary>消灯までを含む演出全体の長さ（秒）。最後のグリッチを遅らせると延長される。</summary>
    public float Duration => 3 + OutroDelay;
    /// <summary>本番の初回描画からの経過秒数。開始前は0。終了後も経過時間を返す。</summary>
    public double Elapsed => start < 0 ? 0 : Time.unscaledTimeAsDouble - start;

    /// <summary>有効化時に再生時計をリセットし、自動再生設定を反映する。</summary>
    /// <returns>戻り値なし。</returns>
    private void OnEnable()
    {
        start = -1;
        running = playOnEnable;
    }

    /// <summary>次の描画からタイトル演出を先頭から再生する。</summary>
    /// <returns>戻り値なし。</returns>
    public void Play()
    {
        start = -1;
        running = true;
        posedTime = -1;
    }

    /// <summary>編集時のプレビューまたはゲームの再生時計から、描画する時刻を選ぶ。</summary>
    /// <returns>戻り値なし。</returns>
    private void OnGUI()
    {
        if (!titleTexture || Event.current.type != EventType.Repaint) return;
        #if UNITY_EDITOR
        if (!Application.IsPlaying(gameObject))
        {
            // 編集時はスライダーの時刻だけで描画し、本番の再生時計は進めない。
            if (previewTime >= 0 && previewTime < Duration) DrawTitle(previewTime);
            return;
        }
        #endif
        if (!running && posedTime >= 0 && posedTime < Duration) { DrawTitle(posedTime); return; }
        if (!running || Elapsed >= Duration) return;
        if (start < 0) start = Time.unscaledTimeAsDouble;
        DrawTitle((float)Elapsed);
    }

    /// <summary>指定時刻の拡大率と透明度を求め、通常表示とグリッチ表示を切り替える。</summary>
    /// <param name="t">演出開始からの秒数。</param>
    /// <returns>戻り値なし。</returns>
    private void DrawTitle(float t)
    {
        // 冒頭で大きさを保つ時間も比例させ、短い設定でも同じリズムで縮小する。
        float holdEnd = .24f * (settleSeconds / .87f);
        float arrival = settleSeconds <= 0f ? 1f : Mathf.SmoothStep(1.14f, 1f, Mathf.InverseLerp(holdEnd, settleSeconds, t));
        float width = Screen.width * widthFraction * .86f * arrival;
        float height = width * titleTexture.height / titleTexture.width;
        // 最後のグリッチが消灯開始より後なら、消灯演出全体を同じ時間だけ後ろへずらす。
        float exitTime=t-OutroDelay;
        width *= 1 - .22f * Mathf.InverseLerp(2.89f, 2.98f, exitTime);
        height *= 1 - .962f * Mathf.InverseLerp(2.73f, 2.89f, exitTime);
        Rect bounds = new Rect((Screen.width-width)/2, Screen.height*verticalCenter-height/2, width, height);
        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;
        GUI.depth = -100;
        Pulse pulse=firstPulse.Contains(t) ? firstPulse : secondPulse.Contains(t) ? secondPulse : thirdPulse.Contains(t) ? thirdPulse : null;
        if (pulse!=null)
            DrawShards(bounds, t, pulse);
        else
        {
            float alpha = Mathf.Lerp(.65f, 1, Mathf.SmoothStep(0,1,t/.87f)) * (1 - Mathf.InverseLerp(2.89f, 2.98f, exitTime));
            if ((exitTime >= 2.64f && exitTime < 2.67f) || (exitTime >= 2.70f && exitTime < 2.72f)) alpha *= .32f;
            GUI.color = new Color(1,1,1,alpha);
            GUI.DrawTexture(bounds, titleTexture);
        }
        // 後続のGUIにタイトルの色や描画順が引き継がれないように元に戻す。
        GUI.color = oldColor;
        GUI.depth = oldDepth;
    }

    /// <summary>タイトル画像を帯に分け、破片の移動と色ずれを重ねて描く。</summary>
    /// <param name="box">タイトルの画面上の描画範囲。</param>
    /// <param name="time">演出開始からの秒数。</param>
    /// <param name="pulse">この時点で適用するグリッチ設定。</param>
    /// <returns>戻り値なし。</returns>
    private void DrawShards(Rect box, float time, Pulse pulse)
    {
        bool weak = pulse == firstPulse;
        float strength = pulse.strength;
        float side = pulse.mirrorX ? -1 : 1;
        float phase = (time-pulse.startSeconds)*(weak ? 1.5f : 1);
        float unit = box.width / .86f;
        for (int x=0; x<columns; x++)
        for (int y=0; y<rows; y++)
        {
            // 各破片に固定番号を割り当てる。同じ破片を次の発でも追えるため、左右の切り返しが揃う。
            int k = x*rows+y;
            float distance = weak ? 1 : .72f + ((pulse==secondPulse ? k*31+9 : k*17+4)%47)/46f*.54f;
            float dx = Mathf.Round(Mathf.Sin(k*13+1)*6)*.005f*unit*strength*distance*side*travelScale.x;
            float dy = Mathf.Round(Mathf.Cos(k*7+2)*8)*.005f*unit*strength*distance*travelScale.y*(pulse.mirrorY ? -1 : 1);
            float edge = .08f+(k%6)*.01f;
            float local = phase + k%5*.018f;
            // 段階ごとにずれ幅を切り替え、帯が滑らず瞬時に飛ぶ動きを作る。
            float tear = local < .089f ? 1 : local < .179f ? -1 : local < .239f ? .6f : local < .329f ? 1 : -1;
            for (int band=0; band<3; band++)
            {
                float top = (y*.25f + (band==0 ? 0 : band==1 ? edge : edge+.04f))*(4f/rows);
                float h = (band==0 ? edge : band==1 ? .04f : .21f-edge)*(4f/rows);
                float w = !weak && (x+3*y+band)%shortBandEvery==0 ? shortBandLength/columns : 1f/columns;
                float split = (band==0 ? 0 : band==1 ? 1 : -.38f)*(k%2==0 ? -1 : 1)*(.13f+k%4*.045f)*unit*.07f*strength*side*tear*tearWidth;
                Rect dst = new Rect(box.x+box.width*x/columns+dx+split,box.y+box.height*top+dy,box.width*w,box.height*h);
                Rect uv = new Rect(x/(float)columns,1-top-h,w,h);
                DrawBand(dst, uv, firstColorOffsetPercent*.01f*unit*strength, firstColor);
                DrawBand(dst, uv, secondColorOffsetPercent*.01f*unit*strength, secondColor);
                DrawBand(dst, uv, Vector2.zero, Color.white);
            }
        }
    }

    /// <summary>画像の指定領域を、画面上でずらして指定色で描く。</summary>
    /// <param name="dst">画面上の描画先。</param>
    /// <param name="uv">画像から切り出す正規化座標の範囲。</param>
    /// <param name="offset">画面上の移動量（ピクセル）。</param>
    /// <param name="color">描画に掛ける色。</param>
    /// <returns>戻り値なし。</returns>
    private void DrawBand(Rect dst, Rect uv, Vector2 offset, Color color)
    {
        dst.position += offset;
        GUI.color = color;
        GUI.DrawTextureWithTexCoords(dst,titleTexture,uv,true);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BlindsideTitle))]
internal sealed class BlindsideTitleInspector : Editor
{
    private BlindsideTitle Title => (BlindsideTitle)target;
    private bool playingPreview, timing, fragments, colors;
    private double origin;
    private float cursor;
    /// <summary>Inspectorを開いたときに、プレビューの更新を登録する。</summary>
    /// <returns>戻り値なし。</returns>
    private void OnEnable() => EditorApplication.update += Tick;
    /// <summary>更新の登録を解除し、Inspectorを閉じた後にプレビューが残らないようにする。</summary>
    /// <returns>戻り値なし。</returns>
    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        if (target && Title.previewTime >= 0) HidePreview();
    }
    /// <summary>プレビューを終了し、Gameビューをシーン本来の表示に戻す。</summary>
    /// <returns>戻り値なし。</returns>
    private void HidePreview()
    {
        playingPreview=false;
        Title.previewTime=-1;
        RefreshView();
    }
    /// <summary>編集モードの更新と再描画を要求し、操作結果をGameビューに反映する。</summary>
    /// <returns>戻り値なし。</returns>
    private static void RefreshView()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
    /// <summary>指定秒を演出の範囲に収めて、プレビューの表示位置を更新する。</summary>
    /// <param name="seconds">演出の先頭からの秒数。</param>
    /// <returns>戻り値なし。</returns>
    private void Seek(float seconds)
    {
        cursor=Mathf.Clamp(seconds,0,Title.Duration);
        Title.previewTime=cursor;
        RefreshView();
    }
    /// <summary>エディターの時計に合わせてプレビューを進め、終端で停止する。</summary>
    /// <returns>戻り値なし。</returns>
    private void Tick()
    {
        if (!playingPreview) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { HidePreview(); return; }
        Seek((float)(EditorApplication.timeSinceStartup-origin));
        if (cursor >= Title.Duration) playingPreview=false;
    }
    /// <summary>シリアライズされた設定を、日本語ラベル付きの編集欄として表示する。</summary>
    /// <param name="key">対象のシリアライズフィールド名。</param>
    /// <param name="label">Inspectorに表示するラベル。</param>
    /// <returns>戻り値なし。</returns>
    private void Field(string key,string label) =>
        EditorGUILayout.PropertyField(serializedObject.FindProperty(key),new GUIContent(label),true);
    /// <summary>設定の効果や操作のヒントを説明欄に表示する。</summary>
    /// <param name="message">表示する説明文。</param>
    /// <returns>戻り値なし。</returns>
    private static void Note(string message) =>
        EditorGUILayout.HelpBox("⚠ 説明　" + message, MessageType.None);
    /// <summary>1回分のグリッチ設定を折りたたみ表示する。</summary>
    /// <param name="key">対象のシリアライズフィールド名。</param>
    /// <param name="label">Inspectorに表示するラベル。</param>
    /// <returns>戻り値なし。</returns>
    private void PulseFields(string key,string label)
    {
        var pulse=serializedObject.FindProperty(key);
        pulse.isExpanded=EditorGUILayout.Foldout(pulse.isExpanded,label,true);
        if (!pulse.isExpanded) return;
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(pulse.FindPropertyRelative("startSeconds"),new GUIContent("開始（秒）"));
            EditorGUILayout.PropertyField(pulse.FindPropertyRelative("endSeconds"),new GUIContent("終了（秒）"));
            EditorGUILayout.PropertyField(pulse.FindPropertyRelative("strength"),new GUIContent("強さ"));
            EditorGUILayout.PropertyField(pulse.FindPropertyRelative("mirrorX"),new GUIContent("破片の左右を反転"));
            EditorGUILayout.PropertyField(pulse.FindPropertyRelative("mirrorY"),new GUIContent("破片の上下を反転"));
        }
    }
    /// <summary>押すとプレビューを一時停止して指定秒へ移動するボタンを表示する。</summary>
    /// <param name="label">Inspectorに表示するラベル。</param>
    /// <param name="seconds">演出の先頭からの秒数。</param>
    /// <returns>戻り値なし。</returns>
    private void Jump(string label,float seconds)
    {
        if (!GUILayout.Button(label)) return;
        playingPreview=false;
        Seek(seconds);
    }
    /// <summary>編集時にはプレビュー操作を、ゲーム再生中には再生し直すボタンを表示する。</summary>
    /// <returns>戻り値なし。</returns>
    private void Preview()
    {
        EditorGUILayout.LabelField("Gameビューでプレビュー",EditorStyles.boldLabel);
        if (Application.IsPlaying(Title.gameObject))
        {
            Note("「先頭から再生」を押すと、タイトル演出を最初から見られるよ。");
            if (GUILayout.Button("先頭から再生")) Title.Play();
            return;
        }
        if (!Title.titleTexture || !Title.isActiveAndEnabled)
        {
            Note("下の「タイトル画像」に画像を入れて、オブジェクトとコンポーネントを有効にすると試せるよ。");
            return;
        }
        Note("Gameビューを開くと、シーンを再生しなくても動きを見られるよ。スライダーで好きな瞬間に止めたり、「2発目」「3発目」で切り返しを見比べたりできるよ。");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(playingPreview ? "一時停止" : "プレビュー再生"))
        {
            if (playingPreview) playingPreview=false;
            else
            {
                Seek(cursor>=Title.Duration ? 0 : cursor);
                origin=EditorApplication.timeSinceStartup-cursor;
                playingPreview=true;
            }
        }
        using (new EditorGUI.DisabledScope(Title.previewTime<0))
            if (GUILayout.Button("表示を終了")) HidePreview();
        EditorGUILayout.EndHorizontal();
        EditorGUI.BeginChangeCheck();
        float next=EditorGUILayout.Slider("再生位置（秒）",cursor,0,Title.Duration);
        if(EditorGUI.EndChangeCheck()){playingPreview=false;Seek(next);}
        EditorGUILayout.BeginHorizontal();
        Jump("先頭",0);
        Jump("1発目",Title.firstPulse.startSeconds);
        Jump("2発目",Title.secondPulse.startSeconds);
        Jump("3発目",Title.thirdPulse.startSeconds);
        EditorGUILayout.EndHorizontal();
    }
    /// <summary>プレビューと演出設定を描画し、変更を対象コンポーネントへ反映する。</summary>
    /// <returns>戻り値なし。</returns>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) Preview();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("タイトルと再生",EditorStyles.boldLabel);
        if (!serializedObject.FindProperty("playOnEnable").boolValue)
            Note("自動再生がオフのときは、ゲーム側で Play() を呼ぶと始まるよ。途中でもう一度呼ぶと、先頭から再生するよ。");
        Field("titleTexture","タイトル画像");
        Field("playOnEnable","有効時に自動再生");
        Field("widthFraction","表示幅");
        Field("verticalCenter","縦位置");
        Note("演出の先頭から、文字が元の大きさに戻るまでの時間だよ。短くすると早く引いて、0秒なら最初から元の大きさだよ。");
        Field("settleSeconds","元のサイズに戻るまで（秒）");
        EditorGUILayout.Space();
        timing=EditorGUILayout.Foldout(timing,"タイミング・強さ・方向",true);
        if(timing){
            Note("開始と終了は、演出の先頭から数えた秒数だよ。時間が重なると番号の小さい方が優先されるよ。左右・上下の反転は、その回の破片にだけ効くよ。");
            PulseFields("firstPulse","1発目");PulseFields("secondPulse","2発目");PulseFields("thirdPulse","3発目");
        }
        fragments=EditorGUILayout.Foldout(fragments,"破片の細かさ・飛距離",true);
        if(fragments){
            Note("「破片の飛距離」を上げると、破片が文字から遠くへ飛ぶよ。「帯の横ずれ」を上げると、破片の中の細い線が大きくずれるよ。どちらも1が元の幅だよ。");
            Field("columns","横の分割数");Field("rows","縦の分割数");Field("travelScale","破片の飛距離");Field("tearWidth","帯の横ずれ");
            Field("shortBandLength","短い帯の長さ");Field("shortBandEvery","何本ごとに短くするか");
        }
        colors=EditorGUILayout.Foldout(colors,"色とずれの方向",true);
        if(colors){
            Note("色がはみ出す向きは、Xが＋で右、－で左。Yが＋で下、－で上だよ。たとえば、色 A の赤を右に出したいなら、Xをプラスにしてみてね。");
            Field("firstColor","色 A");Field("firstColorOffsetPercent","色 A のずれ（%）");Field("secondColor","色 B");Field("secondColorOffsetPercent","色 B のずれ（%）");
        }
        if(serializedObject.ApplyModifiedProperties() && Title.previewTime>=0) Seek(cursor);
    }
}
#endif

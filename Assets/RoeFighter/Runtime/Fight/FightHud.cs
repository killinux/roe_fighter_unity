using UnityEngine;
using UnityEngine.UI;

namespace RoeFighter.Fight
{
    /// <summary>
    /// Health bars, timer, round marks, special gauges and the big messages, built in code with
    /// uGUI.  Screen-space overlay in play mode; when the editor films a match the canvas is drawn
    /// by the fight camera (screen-space camera), so it ends up in the pictures.
    /// </summary>
    public class FightHud : MonoBehaviour
    {
        public bool drawnByCamera;
        public bool showHelp = true;

        Canvas canvas;
        RectTransform[] hpFill = new RectTransform[2], hpLag = new RectTransform[2], meterFill = new RectTransform[2];
        Image[] meterImage = new Image[2];
        Text[] names = new Text[2], meterText = new Text[2];
        Image[,] roundMarks = new Image[2, 3];
        Text timer, message, help, notice;
        float[] lag = { 1f, 1f };
        Font font;

        static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
        static readonly Color HpColor = new Color(1f, 0.78f, 0.2f);
        static readonly Color LagColor = new Color(0.85f, 0.15f, 0.12f);
        static readonly Color Dark = new Color(0.05f, 0.05f, 0.08f, 0.75f);
        static readonly Color MeterColor = new Color(0.25f, 0.65f, 1f);

        public void Build(FightGame game)
        {
            var old = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in transform)
                old.Add(child.gameObject);
            foreach (var go in old)
                DestroyAny(go);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            if (drawnByCamera && game.cam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = game.cam;
                canvas.planeDistance = 0.5f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            for (int i = 0; i < 2; i++)
            {
                bool left = i == 0;
                // health bar
                var frame = Box($"hp{i}", Dark, left ? new Vector2(0.03f, 0.905f) : new Vector2(0.555f, 0.905f),
                                left ? new Vector2(0.445f, 0.945f) : new Vector2(0.97f, 0.945f));
                hpLag[i] = Box("lag", LagColor, Vector2.zero, Vector2.one, frame).rectTransform;
                hpFill[i] = Box("fill", HpColor, Vector2.zero, Vector2.one, frame).rectTransform;
                Inset(hpLag[i]);
                Inset(hpFill[i]);
                names[i] = Label($"name{i}", game.rigs[i].displayName, 34, left ? TextAnchor.LowerLeft : TextAnchor.LowerRight,
                                 left ? new Vector2(0.03f, 0.948f) : new Vector2(0.555f, 0.948f),
                                 left ? new Vector2(0.445f, 0.995f) : new Vector2(0.97f, 0.995f));
                for (int r = 0; r < 3; r++)
                {
                    float x0 = left ? 0.405f - r * 0.02f : 0.58f + r * 0.02f;
                    roundMarks[i, r] = Box($"round{i}{r}", Dark, new Vector2(x0, 0.875f), new Vector2(x0 + 0.014f, 0.897f));
                }
                // special gauge
                var gauge = Box($"meter{i}", Dark, left ? new Vector2(0.03f, 0.045f) : new Vector2(0.72f, 0.045f),
                                left ? new Vector2(0.28f, 0.07f) : new Vector2(0.97f, 0.07f));
                var fill = Box("fill", MeterColor, Vector2.zero, Vector2.one, gauge);
                meterImage[i] = fill;
                meterFill[i] = fill.rectTransform;
                Inset(meterFill[i]);
                meterText[i] = Label($"meterText{i}", "", 26, left ? TextAnchor.LowerLeft : TextAnchor.LowerRight,
                                     left ? new Vector2(0.03f, 0.072f) : new Vector2(0.72f, 0.072f),
                                     left ? new Vector2(0.28f, 0.11f) : new Vector2(0.97f, 0.11f));
            }
            timer = Label("timer", "60", 64, TextAnchor.MiddleCenter, new Vector2(0.455f, 0.875f), new Vector2(0.545f, 0.985f));
            message = Label("message", "", 120, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.62f));
            message.color = Gold;
            help = Label("help", "1P: A/D move  W/S side step  J K U I = A B C D  L O P = specials (or 236 / 214 / 236236 + J or U)   F1/F2: CPU on/off   F3: motions   F4: cloth   F5: skirt",
                         20, TextAnchor.LowerCenter, new Vector2(0.24f, 0.0f), new Vector2(0.76f, 0.04f));
            notice = Label("notice", "", 26, TextAnchor.MiddleCenter, new Vector2(0.15f, 0.79f), new Vector2(0.85f, 0.86f));
            help.gameObject.SetActive(showHelp && !drawnByCamera);
            Refresh(game);
        }

        static void Inset(RectTransform r)
        {
            r.offsetMin = new Vector2(3, 3);
            r.offsetMax = new Vector2(-3, -3);
        }

        Image Box(string name, Color color, Vector2 min, Vector2 max, Image parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent != null ? parent.transform : transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text Label(string name, string text, int size, TextAnchor anchor, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public void Refresh(FightGame game)
        {
            if (timer == null || game.f[0] == null)
                return;
            for (int i = 0; i < 2; i++)
            {
                var x = game.f[i];
                float hp = Mathf.Clamp01(x.hp / (float)x.maxHp);
                lag[i] = Mathf.Max(hp, Mathf.MoveTowards(lag[i], hp, 0.012f));
                SetFill(hpFill[i], hp, i == 0);
                SetFill(hpLag[i], lag[i], i == 0);
                float m = Mathf.Clamp01(x.meter / 100f);
                SetFill(meterFill[i], m, i == 0);
                meterImage[i].color = m >= 1f ? Gold : MeterColor;
                meterText[i].text = m >= 1f ? "MAX" : "";
                for (int r = 0; r < 3; r++)
                {
                    roundMarks[i, r].gameObject.SetActive(r < game.roundsToWin);
                    roundMarks[i, r].color = r < game.wins[i] ? Gold : Dark;
                }
            }
            timer.text = Mathf.CeilToInt(game.timer).ToString();
            message.text = game.message;
            if (notice != null)
                notice.text = game.Notice;
        }

        static void SetFill(RectTransform r, float amount, bool fromLeft)
        {
            r.anchorMin = new Vector2(fromLeft ? 0f : 1f - amount, 0f);
            r.anchorMax = new Vector2(fromLeft ? amount : 1f, 1f);
        }

        static void DestroyAny(GameObject go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }
    }
}

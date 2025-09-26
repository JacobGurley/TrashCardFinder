using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[BepInPlugin("you.weedshop3.trashcardfinder", "Trash NFT Card Finder", "1.7.1")]
public class TrashCardFinder : BaseUnityPlugin
{
    private const string TargetName       = "Trash_NFT_Card";
    private const float  ReadyMinDelay    = 1.5f;
    private const float  ReadyMaxWait     = 20f;
    private const float  ScanWindowSecs   = 10f;
    private const float  ScanIntervalSecs = 2f;
    private const int    DialogWindowId   = 987654;

    private static readonly string NoCardMessage =
        "No Lil' Pothead card spawned in this save.\nSave, exit to menu, and reload to try again.";

    private GameObject _target;
    private Camera _cam;
    private bool _showDialog;
    private bool _scanning;
    private Coroutine _scanCo;

    private void Awake()
    {
        try { DontDestroyOnLoad(gameObject); } catch {}
        SceneManager.sceneLoaded += OnSceneLoaded;
        Logger.LogInfo("[TrashCardFinder] Loaded");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        BeginSceneScan();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _showDialog = false;
        _target = null;
        BeginSceneScan();
    }

    private void BeginSceneScan()
    {
        if (_scanCo != null) StopCoroutine(_scanCo);
        _scanCo = StartCoroutine(ScanRoutineForScene());
    }

    private IEnumerator ScanRoutineForScene()
    {
        _scanning = true;
        yield return WaitForSceneReady();

        if (_cam == null) _cam = Camera.main;
        if (_cam == null)
        {
            Logger.LogInfo("[TrashCardFinder] No camera after ready wait; idle");
            _scanning = false;
            yield break;
        }

        float endAt = Time.realtimeSinceStartup + ScanWindowSecs;

        while (Time.realtimeSinceStartup < endAt)
        {
            TryFindCard();
            if (_target != null)
            {
                Logger.LogInfo($"[TrashCardFinder] Found \"{_target.name}\" @ {_target.transform.position} (scene={_target.scene.name})");
                _scanning = false;
                yield break;
            }

            yield return new WaitForSecondsRealtime(ScanIntervalSecs);
        }

        Logger.LogInfo("[TrashCardFinder] No card found in window; idle");
        _showDialog = true;
        _scanning = false;
    }

    private IEnumerator WaitForSceneReady()
    {
        float start = Time.realtimeSinceStartup;

        while (!SceneManager.GetActiveScene().isLoaded || SceneManager.GetActiveScene().rootCount == 0)
        {
            if (Time.realtimeSinceStartup - start > ReadyMaxWait) yield break;
            yield return null;
        }

        while ((_cam = Camera.main) == null)
        {
            if (Time.realtimeSinceStartup - start > ReadyMaxWait) yield break;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(ReadyMinDelay);
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;

        if (!_scanning && _target != null && (!_target || !_target.activeInHierarchy))
        {
            _target = null;
            Logger.LogInfo("[TrashCardFinder] Card collected; idle");
        }

        if (_showDialog && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return)))
            _showDialog = false;
    }

    private void TryFindCard()
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject best = null;
        float bestDist = float.MaxValue;

        foreach (var go in all)
        {
            if (!go) continue;
            var n = go.name;
            if (string.IsNullOrEmpty(n)) continue;
            if (n.IndexOf(TargetName, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!go.activeInHierarchy) continue;

            float d = _cam ? Vector3.Distance(_cam.transform.position, go.transform.position) : 0f;
            if (d < bestDist) { best = go; bestDist = d; }
        }

        if (best != null) _target = best;
    }

    private void OnGUI()
    {
        if (_showDialog)
        {
            const float w = 420f, h = 140f;
            Rect r = new((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.ModalWindow(DialogWindowId, r, id =>
            {
                GUILayout.Space(8);
                GUILayout.Label(NoCardMessage, GUILayout.ExpandHeight(true));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("OK", GUILayout.Height(28))) _showDialog = false;
            }, "Trash NFT Card Finder");
        }

        if (_cam == null || _target == null || !_target.activeInHierarchy) return;

        Vector3 wpos = _target.transform.position + Vector3.up * 0.6f;
        Vector3 sp = _cam.WorldToScreenPoint(wpos);

        if (sp.z > 0f)
        {
            float x = sp.x, y = Screen.height - sp.y;
            if (x >= 0 && x <= Screen.width && y >= 0 && y <= Screen.height)
                DrawLabel(new Rect(x - 60, y - 18, 120, 22), "LIL POTHEAD CARD");
            else
                DrawEdgeArrowAndDistance(wpos);
        }
        else
        {
            DrawEdgeArrowAndDistance(wpos);
        }
    }

    private void DrawLabel(Rect r, string text)
    {
        GUI.Box(r, GUIContent.none);
        GUI.Label(r, text);
    }

    private void DrawEdgeArrowAndDistance(Vector3 worldPos)
    {
        if (_cam == null) return;

        Vector3 sp = _cam.WorldToScreenPoint(worldPos);
        Vector2 center = new(Screen.width / 2f, Screen.height / 2f);
        Vector2 p = new(sp.x, Screen.height - sp.y);
        Vector2 dir = p - center;
        if (dir.sqrMagnitude < 0.01f) dir = new Vector2(0f, -1f);
        dir.Normalize();

        float pad = 20f;
        Vector2 tip = new(
            Mathf.Clamp(center.x + dir.x * 10000f, pad, Screen.width - pad),
            Mathf.Clamp(center.y + dir.y * 10000f, pad, Screen.height - pad)
        );

        Vector2 perp = new(-dir.y, dir.x);
        float size = 14f;
        Vector2 a = tip - dir * size;
        Vector2 l = a + perp * (size * 0.6f);
        Vector2 r = a - perp * (size * 0.6f);

        DrawLine(tip, l, 3f);
        DrawLine(tip, r, 3f);
        DrawLine(l, r, 3f);

        float dist = _cam ? Vector3.Distance(_cam.transform.position, worldPos) : 0f;
        DrawLabel(new Rect(tip.x - 60, tip.y + 6, 120, 22), $"{dist:0.0} m → CARD");
    }

    private void DrawLine(Vector2 a, Vector2 b, float width)
    {
        Matrix4x4 m = GUI.matrix; Color c = GUI.color;
        GUI.color = Color.white;
        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        float len = Vector2.Distance(a, b);
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, len, width), Texture2D.whiteTexture);
        GUI.matrix = m; GUI.color = c;
    }
}
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class GlassBridgeManager : MonoBehaviour
{
    [System.Serializable] public class Platform {
        public Transform root;
        public GameObject normal;
        public GameObject broken;        // hijo desactivado
        public GameObject brokenPrefab;  // o prefab
        public GameObject highlight;     // opcional: objeto visual para parpadear

        [HideInInspector] public BoxCollider col;
        [HideInInspector] public bool brokenOnce;

        // Fallback de highlight por materiales (cache para restaurar exacto)
        [HideInInspector] public Renderer[] renderers;
        [HideInInspector] public Color[] baseColors;
        [HideInInspector] public Color[] emissionColors;
        [HideInInspector] public bool[] emissionEnabled;
    }

    [System.Serializable] public class Pair {
        public Platform left;
        public Platform right;
        [Tooltip("Si está activo, la falsa del par es la IZQUIERDA.")]
        public bool leftIsFake = true;
    }

    [Header("Pares A..L en orden")]
    public List<Pair> pairs = new List<Pair>(12);

    [Header("Randomización")]
    public bool randomizeOnStart = true;
    [Range(1, 3)] public int maxConsecutiveSameSide = 3;
    public bool useSeed = false;
    public int seed = 0;

    [Header("Revelar patrón al inicio")]
    public bool revealPatternAtStart = true;
    public int revealRepeats = 3;
    public float revealOnTime = 1f;
    public float revealOffTime = 0.5f;

    [Header("Integración jugador")]
    public GlassJumpController player;

    void Awake()
    {
        foreach (var p in pairs) { Setup(p.left); Setup(p.right); }
    }

    void Start()
    {
        if (randomizeOnStart) RandomizePattern();
        if (revealPatternAtStart) StartCoroutine(Co_RevealThenEnable());
        else player?.SetInputEnabled(true);
    }

    void Setup(Platform p)
    {
        if (p == null || p.root == null) return;

        p.col = p.root.GetComponent<BoxCollider>();
        if (p.normal) p.normal.SetActive(true);
        if (p.broken) p.broken.SetActive(false);
        if (p.col) p.col.enabled = true;
        p.brokenOnce = false;

        if (p.highlight) p.highlight.SetActive(false);

        // Cache de materiales para restaurar luego
        var source = p.normal != null ? p.normal : p.root.gameObject;
        p.renderers = source.GetComponentsInChildren<Renderer>(true);

        int n = p.renderers.Length;
        p.baseColors = new Color[n];
        p.emissionColors = new Color[n];
        p.emissionEnabled = new bool[n];

        for (int i = 0; i < n; i++)
        {
            var mat = p.renderers[i].material;
            p.baseColors[i] = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
            p.emissionEnabled[i] = mat.IsKeywordEnabled("_EMISSION");
            p.emissionColors[i] = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
        }
    }

    public void TryBreak(int pairIndex, bool landedLeft)
    {
        if (pairIndex < 0 || pairIndex >= pairs.Count) return;

        var pair = pairs[pairIndex];
        bool shouldBreak = landedLeft ? pair.leftIsFake : !pair.leftIsFake;
        if (!shouldBreak) return;

        var plat = landedLeft ? pair.left : pair.right;
        if (plat == null || plat.brokenOnce) return;

        if (plat.normal) plat.normal.SetActive(false);
        if (plat.brokenPrefab)
            Instantiate(plat.brokenPrefab, plat.root.position, plat.root.rotation, plat.root);
        else if (plat.broken)
            plat.broken.SetActive(true);

        if (plat.col) plat.col.enabled = false;
        plat.brokenOnce = true;

        if (plat.col) plat.col.enabled = false;
        plat.brokenOnce = true;

        player?.OnTrapOpened();
    }

    [ContextMenu("Randomize Pattern Now")]
    public void RandomizePattern()
    {
        System.Random rng = useSeed ? new System.Random(seed) : new System.Random();
        if (pairs.Count == 0) return;

        bool lastLeft = rng.NextDouble() < 0.5;
        int run = 1;
        pairs[0].leftIsFake = lastLeft;

        for (int i = 1; i < pairs.Count; i++)
        {
            bool choice = (run >= maxConsecutiveSameSide) ? !lastLeft : rng.NextDouble() < 0.5;
            pairs[i].leftIsFake = choice;

            if (choice == lastLeft) run++; else { lastLeft = choice; run = 1; }
        }

        var sb = new StringBuilder();
        foreach (var pr in pairs) sb.Append(pr.leftIsFake ? 'L' : 'R');
        Debug.Log($"Patrón falso: {sb}  (máx {maxConsecutiveSameSide} seguidos)");
    }

    IEnumerator Co_RevealThenEnable()
    {
        player?.SetInputEnabled(false);

        for (int k = 0; k < revealRepeats; k++)
        {
            // Encender seguras
            for (int i = 0; i < pairs.Count; i++)
            {
                var safe = pairs[i].leftIsFake ? pairs[i].right : pairs[i].left;
                SetHighlight(safe, true);
            }
            yield return new WaitForSeconds(revealOnTime);

            // Apagar seguras
            for (int i = 0; i < pairs.Count; i++)
            {
                var safe = pairs[i].leftIsFake ? pairs[i].right : pairs[i].left;
                SetHighlight(safe, false);
            }

            if (k < revealRepeats - 1)
                yield return new WaitForSeconds(revealOffTime);
        }

        // Asegura TODO apagado
        TurnAllHighlights(false);

        player?.SetInputEnabled(true);
    }

    void TurnAllHighlights(bool on)
    {
        foreach (var pair in pairs)
        {
            SetHighlight(pair.left, on);
            SetHighlight(pair.right, on);
        }
    }

    void SetHighlight(Platform p, bool on)
    {
        if (p == null) return;

        // Si tienes un hijo "highlight", usamos eso.
        if (p.highlight)
        {
            p.highlight.SetActive(on);
            return;
        }

        // Fallback por materiales (con restauración exacta)
        for (int i = 0; i < p.renderers.Length; i++)
        {
            var r = p.renderers[i];
            if (!r) continue;
            var mat = r.material;

            if (on)
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", p.baseColors[i] * 1.2f);
                else mat.color = p.baseColors[i] * 1.2f;

                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", new Color(0.7f, 1f, 1f) * 2f);
            }
            else
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", p.baseColors[i]);
                else mat.color = p.baseColors[i];

                if (p.emissionEnabled[i]) mat.EnableKeyword("_EMISSION");
                else mat.DisableKeyword("_EMISSION");

                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", p.emissionColors[i]);
            }
        }
    }
}

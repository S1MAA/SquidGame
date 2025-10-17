using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GlassJumpController : MonoBehaviour
{
    [System.Serializable]
    public struct PlatformPair { public Transform left; public Transform right; }

    [Header("Pares (A..L en orden)")]
    public List<PlatformPair> pairs = new List<PlatformPair>(12);

    [Header("Salto")]
    public float jumpTime = 0.6f;

    [Header("Suelo")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.3f;
    public LayerMask groundMask;   // SOLO la capa de plataformas

    [Header("Bridge Manager")]
    public GlassBridgeManager bridge;

    [Header("Animación")]
    public Animator anim;
    public string jumpBool     = "isJumping"; // aire = true
    public string fallTrigger  = "fall";      // trigger para caída
    public string victoryTrigger = "victory"; // trigger para victoria

    [Header("Gameplay")]
    public bool startLocked = true;
    bool inputEnabled;
    bool isJumping = false;
    bool lockFallingAnim = false; // no tocar isJumping mientras está en Falling
    bool hasLost = false;
    bool hasWon  = false;

    Rigidbody rb;
    int currentPairIndex = -1;
    bool lastJumpLeft = true;

    // cache para ignorar colliders propios en grounded
    Collider[] selfCols;
    static readonly Collider[] hits = new Collider[8];

    public void SetInputEnabled(bool enabled)
    {
        if (hasLost || hasWon) return; // no re-habilitar si terminó
        inputEnabled = enabled;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        selfCols = GetComponentsInChildren<Collider>();
        inputEnabled = !startLocked;
    }

    void Update()
    {
        if (hasLost || hasWon) return;

        bool grounded = IsGrounded();

        // Única booleana del animator para salto/aire
        if (anim && !lockFallingAnim)
            anim.SetBool(jumpBool, !grounded);

        if (!inputEnabled) return;
        if (isJumping) return;
        if (!grounded) return;

        if (Input.GetKeyDown(KeyCode.A)) { lastJumpLeft = true;  JumpToNextPair(true);  }
        else if (Input.GetKeyDown(KeyCode.D)) { lastJumpLeft = false; JumpToNextPair(false); }
    }

    void JumpToNextPair(bool left)
    {
        int next = currentPairIndex + 1;
        if (next < 0 || next >= pairs.Count) return;

        Transform t = left ? pairs[next].left : pairs[next].right;
        if (t == null) return;

        StartCoroutine(BallisticJump(t.position, next));
    }

    IEnumerator BallisticJump(Vector3 target, int nextIndex)
    {
        isJumping = true;

        Vector3 start = rb.position;
        float t = Mathf.Max(0.15f, jumpTime);
        Vector3 disp = target - start;
        Vector3 dispXZ = new Vector3(disp.x, 0f, disp.z);

        float g = Physics.gravity.y;
        Vector3 velocity = new Vector3(
            dispXZ.x / t,
            (disp.y - 0.5f * g * t * t) / t,
            dispXZ.z / t
        );

        rb.linearVelocity = velocity;

        // Deja avanzar la física un paso para salir del suelo
        yield return new WaitForFixedUpdate();

        // Espera a volver a tocar plataforma
        while (!IsGrounded()) yield return null;

        currentPairIndex = nextIndex;
        bridge?.TryBreak(nextIndex, lastJumpLeft);

        isJumping = false;
    }

    bool IsGrounded()
    {
        if (!groundCheck) return true;

        int count = Physics.OverlapSphereNonAlloc(
            groundCheck.position, groundCheckRadius, hits, groundMask, QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            var c = hits[i];
            bool isSelf = false;
            for (int j = 0; j < selfCols.Length; j++)
                if (c == selfCols[j]) { isSelf = true; break; }
            if (!isSelf) return true;
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }

    // ======= LLAMADO DESDE EL MANAGER CUANDO SE ABRE LA TRAMPA (piso falso) =======
    public void OnTrapOpened()
    {
        if (hasLost || hasWon) return;

        hasLost = true;
        inputEnabled = false;   // bloquea input definitivamente
        lockFallingAnim = true; // no toques isJumping mientras cae

        if (anim)
        {
            if (!string.IsNullOrEmpty(jumpBool)) anim.SetBool(jumpBool, false);
            if (!string.IsNullOrEmpty(fallTrigger))
            {
                anim.ResetTrigger(fallTrigger);
                anim.SetTrigger(fallTrigger); // entra una sola vez a Falling
            }
        }
    }

    // ======= VICTORIA: al tocar un trigger con tag "final" =======
    void OnTriggerEnter(Collider other)
    {
        if (hasLost || hasWon) return;

        if (other.CompareTag("final"))
        {
            hasWon = true;
            inputEnabled = false;

            // Detén movimiento físico
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Forzar salida de otras anims y disparar victoria
            lockFallingAnim = true;
            if (anim)
            {
                if (!string.IsNullOrEmpty(jumpBool)) anim.SetBool(jumpBool, false);
                if (!string.IsNullOrEmpty(fallTrigger)) anim.ResetTrigger(fallTrigger);
                if (!string.IsNullOrEmpty(victoryTrigger)) anim.SetTrigger(victoryTrigger);
            }
        }
    }
}

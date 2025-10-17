using UnityEngine;

public class CubeJump : MonoBehaviour
{
    public float jumpForce = 5f; // fuerza del salto
    private Rigidbody rb;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Saltar con espacio, solo si está en el suelo
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }
    }

    // Detectar si toca el suelo u otros objetos
    private void OnCollisionEnter(Collision collision)
    {
        isGrounded = true;
    }
}

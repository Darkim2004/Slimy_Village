using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;

    // questo ora è solo l'input grezzo, non ancora moltiplicato per la velocità
    private Vector2 inputDir;

    [SerializeField] private float moveSpeed = 2.0f;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Leggo l'input come direzione (da -1 a 1)
        float moveH = Input.GetAxisRaw("Horizontal");
        float moveV = Input.GetAxisRaw("Vertical");

        inputDir = new Vector2(moveH, moveV);
    }

    private void FixedUpdate()
    {
        Vector2 dir = inputDir;

        // Se c'è input, normalizzo per evitare diagonale più veloce
        if (dir.sqrMagnitude > 1e-5f)
        {
            dir = dir.normalized; // lunghezza = 1 anche in diagonale
        }

        // Se usi Unity 6 / nuova fisica:
        rb.linearVelocity = dir * moveSpeed;

        // Se fossi su versioni vecchie:
        // rb.velocity = dir * moveSpeed;

        Debug.Log($"Rigidbody2D velocity: {rb.linearVelocity}");
    }
}

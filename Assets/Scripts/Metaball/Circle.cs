using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Circle : MonoBehaviour
{
    CircleCollider2D circleCollider;
    Rigidbody2D rigidBody;
    float speed;
    float radius;
    
    public float Radius => radius;
    
    void Awake()
    {
        circleCollider = GetComponent<CircleCollider2D>();

        rigidBody = GetComponent<Rigidbody2D>();
        rigidBody.isKinematic = true;
        rigidBody.useFullKinematicContacts = true;
    }

    void Start()
    {
        rigidBody.velocity = Random.insideUnitCircle.normalized;
        speed = Random.Range(4.0f, 10.0f);
        radius = Random.Range(5.0f, 10.0f);
        
        circleCollider.radius = radius;
    }

    void Update()
    {
        rigidBody.MovePosition(rigidBody.position + Time.deltaTime * speed * rigidBody.velocity);
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("CircleBound"))
        {
            rigidBody.velocity = Vector2.Reflect(rigidBody.velocity, other.GetContact(0).normal);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
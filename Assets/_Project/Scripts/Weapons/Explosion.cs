using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class Explosion : MonoBehaviour
{
    private const int MAX_AFFECTED_COLLIDERS = 32;

    [SerializeField] private float radius = 6f;
    [SerializeField] private float force = 12f;
    [SerializeField] private float upwardsModifier = 1f;
    [SerializeField] private LayerMask affectedLayers = ~0;
    [SerializeField] private ParticleSystem[] particleSystems;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip explosionClip;
    [SerializeField] private Light flashLight;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private float flashIntensity = 8f;
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private float lifetime = 4f;

    private readonly Collider[] overlapBuffer = new Collider[MAX_AFFECTED_COLLIDERS];
    private readonly HashSet<Rigidbody> pushedBodies = new HashSet<Rigidbody>();

    void Start()
    {
        PlayParticles();
        if (audioSource != null && explosionClip != null)
        {
            audioSource.PlayOneShot(explosionClip);
        }

        if (flashLight != null)
        {
            StartCoroutine(Flash());
        }

        ApplyForce();
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }

        Destroy(gameObject, lifetime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }

    private void PlayParticles()
    {
        if (particleSystems == null)
        {
            return;
        }

        foreach (ParticleSystem particle in particleSystems)
        {
            if (particle != null)
            {
                particle.Play(true);
            }
        }
    }

    private IEnumerator Flash()
    {
        flashLight.enabled = true;
        float elapsedTime = 0f;
        while (elapsedTime < flashDuration)
        {
            flashLight.intensity = Mathf.Lerp(flashIntensity, 0f, elapsedTime / flashDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        flashLight.enabled = false;
    }

    // Moi Rigidbody chi day 1 lan du co nhieu collider; bo qua kinematic (luu dan dang cam, chot chua roi)
    private void ApplyForce()
    {
        Vector3 center = transform.position;
        int hitCount = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, affectedLayers, QueryTriggerInteraction.Ignore);
        pushedBodies.Clear();
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Rigidbody body = overlapBuffer[hitIndex].attachedRigidbody;
            if (body == null || body.isKinematic || !pushedBodies.Add(body))
            {
                continue;
            }

            body.AddExplosionForce(force, center, radius, upwardsModifier, ForceMode.VelocityChange);
        }
    }
}

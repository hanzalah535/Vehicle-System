using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFPS.Runtime.Vehicles
{
    [RequireComponent(typeof(AudioSource))]
    public class bl_CarWheel : MonoBehaviour
    {

        public Transform SkidTrailPrefab;
        public WheelCollider wheelCollider;
        public static Transform skidTrailsDetachedParent;
        public ParticleSystem skidParticles;

        public bool skidding { get; private set; }
        public bool PlayingAudio { get; private set; }
        public GameObject wheelModel { get; set; }

        private AudioSource m_AudioSource;
        private Transform m_SkidTrail;
        private Transform m_Transform;

        /// <summary>
        /// 
        /// </summary>
        private void Start()
        {
            m_Transform = transform;
            if (skidParticles == null)
            {
             //   Debug.LogWarning(" no particle system found on car to generate smoke particles");
            }
            else
            {
                skidParticles.Stop();
            }

            m_AudioSource = GetComponent<AudioSource>();
            PlayingAudio = false;

            if (skidTrailsDetachedParent == null)
            {
                skidTrailsDetachedParent = new GameObject("Skid Trails - Detached").transform;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void EmitTyreSmoke()
        {
            if (skidParticles == null) return;

            skidParticles.transform.position = m_Transform.position - m_Transform.up * wheelCollider.radius;
            skidParticles.Emit(1);
            if (!skidding && SkidTrailPrefab != null)
            {
                StartSkidTrail();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void PlayAudio()
        {
            m_AudioSource.Play();
            PlayingAudio = true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void StopAudio()
        {
            m_AudioSource.Stop();
            PlayingAudio = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public void StartSkidTrail()
        {
            skidding = true;
            if(m_SkidTrail == null) m_SkidTrail = Instantiate(SkidTrailPrefab);

            m_SkidTrail.gameObject.SetActive(true);
            m_SkidTrail.parent = m_Transform;
            m_SkidTrail.localPosition = -Vector3.up * wheelCollider.radius;
        }

        /// <summary>
        /// 
        /// </summary>
        public void EndSkidTrail()
        {
            if (!skidding || m_SkidTrail == null)
            {
                return;
            }
            skidding = false;
            m_SkidTrail.parent = skidTrailsDetachedParent;
            m_SkidTrail.gameObject.SetActive(false);
        }
    }
}
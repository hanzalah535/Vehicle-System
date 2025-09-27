using UnityEngine;
using Photon.Pun;

namespace MFPS.Runtime.Vehicles
{
    /// <summary>
    /// Network transform sync
    /// With Interpolation, Extrapolation, Lag compensation and Position Smoothness.
    /// </summary>
    public class bl_VehicleTransformView : MonoBehaviourPun, IPunObservable
    {
        [LovattoToogle] public bool moveUsingRigidbody = false;
        public float positionSmoothness = 12;
        public float rotationSmoothness = 16;

        /// <summary>
        /// 
        /// </summary>
        public bool IsSynchronizing
        {
            get;
            set;
        } = true;

        #region Private members
        private bool firstPackage = false;
        private float deltaPosition;
        private float deltaRotation;
        private Vector3 projectedDirection;
        private Vector3 networkPosition;
        private Vector3 cachedPosition;
        private Quaternion networkRotation;
        private Transform m_transform;
        private Vector3 nextPosition;
        private Quaternion nextRotation;
        private Rigidbody m_rigidBody;
        public Rigidbody Rigidbody
        {
            get
            {
                if (m_rigidBody == null) TryGetComponent(out m_rigidBody);
                return m_rigidBody;
            }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void Awake()
        {
            m_transform = transform;
            cachedPosition = m_transform.localPosition;
            networkPosition = Vector3.zero;
            networkRotation = Quaternion.identity;

            if (!photonView.ObservedComponents.Exists(x => x == this))
            {
                photonView.ObservedComponents.Add(this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        void OnEnable()
        {
            firstPackage = true;
        }

        /// <summary>
        /// 
        /// </summary>
        void FixedUpdate()
        {
            if (!photonView.IsMine)
            {
                nextPosition = Vector3.MoveTowards(m_transform.localPosition, networkPosition, deltaPosition * Time.deltaTime * PhotonNetwork.SerializationRate);
                nextRotation = Quaternion.RotateTowards(m_transform.localRotation, networkRotation, deltaRotation * Time.deltaTime * PhotonNetwork.SerializationRate);

                if (!moveUsingRigidbody)
                {
                    m_transform.localPosition = Vector3.Lerp(m_transform.localPosition, nextPosition, Time.fixedDeltaTime * positionSmoothness);
                    m_transform.localRotation = Quaternion.Lerp(m_transform.localRotation, nextRotation, Time.fixedDeltaTime * rotationSmoothness);
                }
                else
                {
                    Rigidbody.MovePosition(Vector3.Lerp(m_transform.localPosition, nextPosition, Time.fixedDeltaTime * positionSmoothness));
                    Rigidbody.MoveRotation(Quaternion.Lerp(m_transform.localRotation, nextRotation, Time.fixedDeltaTime * rotationSmoothness));
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // Calculate the projected direction;
                projectedDirection = m_transform.localPosition - cachedPosition;
                // Use local position instead of position for optimization
                // But that means that vehicles could never been placed under a parent in the scene hierarchy.
                cachedPosition = m_transform.localPosition;

                stream.SendNext(cachedPosition);
                stream.SendNext(projectedDirection);
                stream.SendNext(m_transform.localRotation);
            }
            else
            {
                if (!IsSynchronizing) return;

                networkPosition = (Vector3)stream.ReceiveNext();
                projectedDirection = (Vector3)stream.ReceiveNext();
                networkRotation = (Quaternion)stream.ReceiveNext();

                // If this is the first package for this view
                // Extrapolate the vehicle to the given position.
                if (firstPackage)
                {               
                    if (!moveUsingRigidbody)
                    {
                        m_transform.localPosition = networkPosition;
                    }
                    else
                    {
                        Rigidbody.MovePosition(networkPosition);
                    }
                    deltaPosition = 0f;
                }
                else
                {
                    // Calculate the time that toke since the message was sent from the peer creator
                    float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
                    // Compensate lag position based in the last projected direction.
                    networkPosition += projectedDirection * lag;
                    // Calculate the delta distance to move towards
                    deltaPosition = bl_MathUtility.Distance(m_transform.localPosition, networkPosition);
                }
                // If this is the first package for this view
                // Extrapolate the vehicle to the given rotation.
                if (firstPackage)
                {
                    deltaRotation = 0f;
                    if (!moveUsingRigidbody)
                    {
                        m_transform.localRotation = networkRotation;
                    }
                    else
                    {
                        Rigidbody.MoveRotation(networkRotation);
                    }
                }
                else
                {
                    // Calculate the delta distance/angle to rotate towards
                    deltaRotation = Quaternion.Angle(m_transform.localRotation, networkRotation);
                }

                if (firstPackage) firstPackage = false;
            }
        }

        /// <summary>
        /// Active or Disable the synchronization
        /// This doesn't actually stop sending data but prevent to applied the data received.
        /// </summary>
        public void SetSynchronizationActive(bool active)
        {
            IsSynchronizing = active;
            if (!active)
            {
                firstPackage = true;
            }
        }
    }
}
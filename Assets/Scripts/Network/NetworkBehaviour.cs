using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Network
{
    public class NetworkBehaviour : MonoBehaviour
    {
        public bool IsOwnedByClient => (OwnedId == NetworkMgr.Instance.playerId) || !NetworkMgr.Instance.IsConnectedToRoom;
        

        public string OwnedId
        {
            get;
            protected internal set;
        }
        
        public string ObjectId { get; protected internal set; }
        public int PrefabIndex { get; protected internal set; }

        internal Vector3 _interpolPosition;
        internal Quaternion _interpolRotation;
        [SerializeField] internal Vector3 velocity;
        
        public static float UpdateCooldown = 0.1f;
        public static float InterpolationTime = 10.0f;
        
        private float _updateTimer;
        protected Dictionary<object,object> extraData = new Dictionary<object, object>();

        private void Start()
        {
            if (!IsOwnedByClient)
            {
                Destroy(GetComponent<Rigidbody>());
            }
        }

        internal virtual void OnNetworkUpdate(Dictionary<object, object> packet)
        {
            var pos = packet["position"] as object[];
            var rot = packet["rotation"] as object[];
            var vel = packet["velocity"] as object[];

            _interpolPosition = new Vector3(float.Parse(pos[0].ToString()), float.Parse(pos[1].ToString()), float.Parse(pos[2].ToString()));
            velocity = new Vector3(float.Parse(vel[0].ToString()), float.Parse(vel[1].ToString()), float.Parse(vel[2].ToString()));
            _interpolRotation = new Quaternion(float.Parse(rot[0].ToString()), float.Parse(rot[1].ToString()), float.Parse(rot[2].ToString()),float.Parse(rot[3].ToString()));

            extraData = packet["extra_data"] as Dictionary<object, object>;
        }

        internal virtual async Task<Dictionary<string, object>> Serialize()
        {
            return new Dictionary<string, object>
            {
                { "object_id", ObjectId },
                { "position", transform.position },
                { "rotation", transform.rotation },
                { "velocity", velocity },
                { "extra_data", extraData },
            };
        }

        internal virtual void OnDisconnect()
        {
            
        }

        private void OnDestroy()
        {
            if (IsOwnedByClient)
            {
                _ = NetworkMgr.Instance.Destroy(this);
            }
        }

        private void LateUpdate()
        {
            if (IsOwnedByClient)
            {
                _updateTimer += Time.deltaTime;

                if (_updateTimer >= UpdateCooldown && NetworkMgr.Instance.IsConnectedToRoom)
                {
                    _updateTimer = 0;
                    NetworkMgr.Instance.UpdateNetworkObject(this);
                }
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, _interpolPosition, Time.deltaTime * InterpolationTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, _interpolRotation, Time.deltaTime * InterpolationTime);
            }
        }
    }
}
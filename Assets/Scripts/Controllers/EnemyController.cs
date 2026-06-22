using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using External;
using Network;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class EnemyController : NetworkBehaviour
{
    private NavMeshAgent _agent;

    [Range(1,35)]  public int DetectionSubdivisons = 3;
    public float DetectionAngle = 45;
    public float DetectionRange = 10;
    public float DetectionRadius = 1;
    public float PeripheralRadius = 1;
    public Vector3 DetectionOffset;

    private int DetectionIndex = -1;
    
    private Vector3 LastRememberedPosition;
    private Vector3 LastRememberedDirection;
     [SerializeField] private PlayerController FocusedPlayer;

    [SerializeField] private float RememberingTime = 0;
    private MapController _mapController;

    private Material _fearMaterial;
    [SerializeField] private float _fearValue = 0;

    private bool _isTargetingCurrentPlayer => _trackingId == NetworkMgr.Instance.playerId;
    [SerializeField] private int _trackingState;
    [SerializeField] private string _trackingId;

    private float _attackTimer = 0;
    private float _attackDelay = 1.0f;

    public float AttackDistance = 5.0f;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _mapController = FindAnyObjectByType<MapController>();

        if (!IsOwnedByClient)
        {
            Destroy(_agent);
            _agent = null;
        }
        else
        {
            transform.position = _mapController.transform.Find("EnemySpawnPoint").position;
        }
    }



    internal override void OnNetworkUpdate(Dictionary<object, object> packet)
    {
        base.OnNetworkUpdate(packet);
        
        if (extraData.ContainsKey("current_target"))
        {
            var currentTargetId = extraData["current_target"].ToString();


            
            _trackingId = currentTargetId;

            _trackingState = int.Parse(extraData["tracking_state"].ToString());

        }
    }

    internal override Task<Dictionary<string, object>> Serialize()
    {

        if (IsOwnedByClient)
        {
            extraData["current_target"] = _trackingId;
            extraData["tracking_state"] = _trackingState;
        }

        var packet = base.Serialize();

        
        return packet;
    }


    // Update is called once per frame
    async void Update()
    {
        if (IsOwnedByClient)
        {
            _attackTimer += Time.deltaTime;
            CheckView();

            float timer = 5;
            float minDistance = _agent.stoppingDistance + 0.1f;
            float speed = 3;
            
            if (RememberingTime > timer && Vector3.Distance(transform.position, _agent.destination) < minDistance)
            {

                bool found = false;

                var targetPos = Vector3.zero;
                while (!found)
                {
                    Vector3 randomPos = Random.insideUnitSphere * 15f;
                    randomPos.y = 0;
                    
                    randomPos += transform.position+transform.forward;
                    
                    found = NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 15f, NavMesh.AllAreas);

                    if (found)
                    {
                        targetPos = hit.position;
                    }
                }
                
                
                
                _agent.SetDestination(targetPos);
                speed = 1;


                _trackingState = 0;
            }
            else if (RememberingTime < timer)
            {
                _agent.SetDestination(LastRememberedPosition);
                speed = 3.5f;

                _trackingState = 1;
            }

            if (FocusedPlayer)
            {
                speed = 5f;
                _agent.SetDestination(LastRememberedPosition);
                _trackingState = 2;

                float distance = Vector3.Distance(transform.position, FocusedPlayer.transform.position);

                if (distance <= AttackDistance)
                {

                    if (_attackTimer >= _attackDelay)
                    {
                        _attackTimer = 0;
                        await NetworkMgr.Instance.SendPlayerDamage(0.125f, FocusedPlayer, this);
                    }
                }
            }

            _trackingId = FocusedPlayer ? FocusedPlayer.OwnedId : "none";

            _agent.speed = Mathf.Lerp(_agent.speed, speed, Time.deltaTime * 3f);
        }
        float value = 0;

        
        if (_isTargetingCurrentPlayer)
        {
            if (_trackingState == 0)
                value = 0;
            else if (_trackingState == 1)
                value = 1f/16f;
            else if (_trackingState == 2)
            {
                float distance = Vector3.Distance(transform.position,PlayerController.Instance.transform.position)/25f;
                distance = 1.0f - Mathf.Clamp(distance, 0,1);

                value = distance;
            }

            PlayerController.Instance.Fear = value;
        }

        

    }

    private void FixedUpdate()
    {
        if (IsOwnedByClient)
        {
            if (FocusedPlayer == null)
            {
                RememberingTime += Time.fixedDeltaTime;
            }
            else
            {
                RememberingTime = 0;
            }
        }
    }

    public void CheckView()
    {
        if (FocusedPlayer && FocusedPlayer.Health <= 0)
        {
            FocusedPlayer = null;
        } 
        
        Collider[] colliders = new Collider[4];
        float opposite = DetectionRadius + DetectionRange * Mathf.Tan((DetectionAngle) * Mathf.Deg2Rad);
        float hypotenuse = DetectionRange / Mathf.Cos((DetectionAngle) * Mathf.Deg2Rad);
        
        int divisions = DetectionSubdivisons;
        float range;
        Vector3 root = (transform.position + DetectionOffset);
        DetectionIndex = -1;
        bool found = false;
        for (int i = 0; i <= divisions; i++)
        {
            found = false;
            float radius = opposite;
            Vector3 end = root + (transform.forward * hypotenuse);
            int count = Physics.OverlapSphereNonAlloc(end,
                radius,
                colliders,
                LayerMask.GetMask("Player"));

            if (count > 0)
            {
                foreach (var collider1 in colliders)
                {
                    if (!collider1)
                        continue;
                    
                    bool _hit = Physics.Raycast(root, collider1.transform.position - root , out RaycastHit hit);
                    if (_hit)
                    {
                        if (hit.collider != collider1)
                        {
                            
                        }
                        else
                        {
                            DetectionIndex = i;
                            LastRememberedPosition = collider1.transform.position;
                            LastRememberedDirection = collider1.transform.forward;
                            if (FocusedPlayer != null && FocusedPlayer.gameObject != collider1.gameObject ||
                                !FocusedPlayer)
                            {
                                FocusedPlayer = collider1.GetComponent<PlayerController>();
                                if (FocusedPlayer.Health <= 0)
                                {
                                    FocusedPlayer = null;
                                    continue;
                                }
                            }

                            found = true;
                            break;
                        }
                    }
                }
                if (found)
                    break;
            }

            range = Mathf.Lerp(DetectionRange,DetectionRadius,(float)i/(float)divisions);
            
            opposite = DetectionRadius + range * Mathf.Tan((DetectionAngle) * Mathf.Deg2Rad);
            hypotenuse = range / Mathf.Cos((DetectionAngle) * Mathf.Deg2Rad);
            
            
            
        }

        if (Physics.OverlapSphereNonAlloc(transform.position+(transform.forward*(PeripheralRadius)), PeripheralRadius, colliders, LayerMask.GetMask("Player")) > 0)
        {
            foreach (var collider1 in colliders)
            {
                if (!collider1)
                    continue;
                
                bool _hit = Physics.Raycast(root, collider1.transform.position - root , out RaycastHit hit);
                if (_hit && hit.collider == collider1)
                {
                    LastRememberedPosition = collider1.transform.position;
                    LastRememberedDirection = collider1.transform.forward;
                    if (FocusedPlayer != null && FocusedPlayer.gameObject != collider1.gameObject || !FocusedPlayer)
                    {
                        FocusedPlayer = collider1.GetComponent<PlayerController>();
                        if (FocusedPlayer.Health <= 0)
                        {
                            FocusedPlayer = null;
                            continue;
                        }
                    }

                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            if (FocusedPlayer != null)
                LastRememberedPosition += FocusedPlayer.velocity;
            FocusedPlayer = null;
        }
        
    }
    
    private void OnDrawGizmos()
    {

        Gizmos.DrawWireSphere(transform.position+(transform.forward*(PeripheralRadius)), PeripheralRadius);
        Gizmos.DrawWireSphere(LastRememberedPosition, 1f);

        ExtraGizmos.DrawGizmosCircle(transform.position+DetectionOffset, transform.forward, DetectionRadius,32);
        
        // sine = o/h
        // cosine = a/h
        // tangent = o/a

        // cosecant = h/o
        // secant = h/a
        // cotangent = a/o
        
        // trying to find the length of o and h
        // a = detectionRange
        // Θ = detectionAngle
        
        // angle is given
        // adjacent is given
        
        // hypotenuse is needed
        // opposite is needed
        
        
        float opposite = DetectionRadius + DetectionRange * Mathf.Tan((DetectionAngle) * Mathf.Deg2Rad);
        float hypotenuse = DetectionRange / Mathf.Cos((DetectionAngle) * Mathf.Deg2Rad);


        
        Vector3 CalcStart(float angle)
        {
            Vector3 start = new Vector3(); 
            start.x = DetectionRadius * Mathf.Cos(angle);
            start.y = DetectionRadius * Mathf.Sin(angle);

            start = transform.localToWorldMatrix.MultiplyVector(start);
            start += transform.position+DetectionOffset;
            
            return start;
        }         
        
        Vector3 CalcEnd(float angle)
        {
            Vector3 end = new Vector3(); 
            end.x = opposite * Mathf.Cos(angle);
            end.y = opposite * Mathf.Sin(angle);
            end.z = hypotenuse;

            end = transform.localToWorldMatrix.MultiplyVector(end);
            end += transform.position+DetectionOffset;
            
            return end;
        }         
        

        for (int i = 0; i < 4; i++)
        {
            float angle = (i*Mathf.PI)/2;
            
            Gizmos.DrawLine(CalcStart(angle),CalcEnd(angle));
        }

        Vector3 end = transform.position + (transform.forward * hypotenuse) + DetectionOffset;
        
        ExtraGizmos.DrawGizmosCircle(end, transform.forward, opposite,32);

        return;
        
        int divisions = DetectionSubdivisons;
        float range = DetectionRange;
        for (int i = 0; i <= divisions; i++)
        {
            float radius = opposite;
            end = transform.position + (transform.forward * hypotenuse) + DetectionOffset;

            if (DetectionIndex >= i)
            {
                
                Gizmos.color = Color.green;
            }
            else
            {
                Gizmos.color = Color.red;
            }
            
            Gizmos.DrawWireSphere(end,radius);

            range = Mathf.Lerp(DetectionRange,DetectionRadius,(float)i/(float)divisions);
            
            opposite = DetectionRadius + range * Mathf.Tan((DetectionAngle) * Mathf.Deg2Rad);
            hypotenuse = range / Mathf.Cos((DetectionAngle) * Mathf.Deg2Rad);
            
            
            
        }
    }
}
